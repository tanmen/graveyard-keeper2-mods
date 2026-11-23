using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;

namespace TradeFinder
{
    // ゲームのツールチップ（UITooltip）に差し込む所。根拠は TradeFinder/docs/game-api-notes.md（Stock Finder と同じ差し込み方）。
    //
    // ツールチップは出すたびに中身の部品の並び（List<LazyWidgetDataBase>）を組み立てて ShowAtTargetAndCalculateOffsets に渡す。
    // 品の説明を足す AddItemWidgets と、品の効果を足す AddItemEffectWidgets で「この並びはこの品のもの」と覚えておき、
    // 出す直前に同じ並びなら売り買いの場所を足す。1 つの並びに品が 2 つ以上来たら、最初の品だけ。
    //
    // キー（既定 Shift）を押している間だけ出す項目があるときは、出したときの引数を覚えておき、キーを押す・離したら
    // 自分の部品だけを入れ替えて、同じ引数で出し直す（同じ相手に出し直すとゲームは音を鳴らさず、置き場所も測り直す）。

    internal static class TooltipHook
    {
        private const int LostWarnAt = 5;

        private sealed class Shown
        {
            public List<LazyWidgetDataBase> List;
            public ItemDef Item;
            public RectTransform Target;
            public UIBasicBubble.ForceCornerPosition Corner;
            public bool Follow;
            public Vector2 AppearOffset;
            public TooltipPlacementPriority Priority;
            public int Index;
            public List<LazyWidgetDataBase> Block;
            public bool KeyHeld;
        }

        private static List<LazyWidgetDataBase> pendingList;
        private static ItemDef pendingItem;
        private static Shown current;
        private static bool reshowing;
        private static int appended;
        private static int lost;
        private static int reshown;
        private static MethodInfo showMethod;
        private static bool showMethodResolved;

        public static void Remember(List<LazyWidgetDataBase> list, ItemDef item)
        {
            if (list == null || item == null || ReferenceEquals(list, pendingList))
            {
                return;
            }

            if (pendingList != null)
            {
                // 覚えた並びが出されないまま次の並びが来た。1 度も足せていないまま続くなら、出す所の差し込みが効いていない
                lost++;
                if (appended == 0 && lost == LostWarnAt)
                {
                    Plugin.Log.LogWarning($"品を {lost} 回覚えたのに、ツールチップを出す所で 1 度も足せていない（出す所の差し込みが効いていない可能性）");
                }
            }

            pendingList = list;
            pendingItem = item;
        }

        /// <summary>出す直前。覚えた並びなら売り買いの場所を足し、出し直しのために引数を覚える。</summary>
        public static void OnShow(List<LazyWidgetDataBase> list, RectTransform target, UIBasicBubble.ForceCornerPosition corner, bool follow,
            Vector2 appearOffset, TooltipPlacementPriority priority)
        {
            if (reshowing)
            {
                return;
            }

            current = null;
            if (list == null || !ReferenceEquals(list, pendingList))
            {
                return;
            }

            ItemDef item = pendingItem;
            pendingList = null;
            pendingItem = null;
            appended++;
            bool held = KeyWatcher.Held();
            var shown = new Shown
            {
                List = list,
                Item = item,
                Target = target,
                Corner = corner,
                Follow = follow,
                AppearOffset = appearOffset,
                Priority = priority,
                Index = list.Count,
                KeyHeld = held,
            };
            shown.Block = TradeSection.Build(item, held, NeedSeparator(list, list.Count));
            list.AddRange(shown.Block);
            current = shown;
        }

        /// <summary>いま出ているツールチップに足した品があり、キーの状態が出したときと違えば、自分の部品を入れ替えて出し直す。</summary>
        public static void ReshowIfKeyChanged(bool held)
        {
            Shown shown = current;
            if (shown == null || shown.KeyHeld == held)
            {
                return;
            }

            shown.KeyHeld = held;
            UITooltip tooltip = TooltipStyles.Instance();
            if (tooltip == null || !tooltip.gameObject.activeSelf || shown.Target == null)
            {
                current = null; // 消えたか、相手が無くなった
                return;
            }

            MethodInfo show = ShowMethod();
            if (show == null)
            {
                return;
            }

            foreach (LazyWidgetDataBase widget in shown.Block)
            {
                shown.List.Remove(widget);
            }

            int index = Math.Max(0, Math.Min(shown.Index, shown.List.Count));
            shown.Block = TradeSection.Build(shown.Item, held, NeedSeparator(shown.List, index));
            shown.List.InsertRange(index, shown.Block);
            reshowing = true;
            try
            {
                show.Invoke(tooltip, new object[] { shown.List, shown.Target, shown.Corner, shown.Follow, shown.AppearOffset, shown.Priority });
                reshown++;
                if (reshown == 1)
                {
                    Plugin.Log.LogInfo($"キーで出し直した（初めて）: {shown.Item.id}・キー {(held ? "押した" : "離した")}・足した部品 {shown.Block.Count}");
                }
            }
            finally
            {
                reshowing = false;
            }
        }

        private static bool NeedSeparator(List<LazyWidgetDataBase> list, int index) => index > 0 && !(list[index - 1] is UITooltipSeparatorWidgetData);

        private static MethodInfo ShowMethod()
        {
            if (!showMethodResolved)
            {
                showMethodResolved = true;
                showMethod = AccessTools.Method(typeof(UITooltip), "ShowAtTargetAndCalculateOffsets", new[]
                {
                    typeof(List<LazyWidgetDataBase>), typeof(RectTransform), typeof(UIBasicBubble.ForceCornerPosition), typeof(bool), typeof(Vector2),
                    typeof(TooltipPlacementPriority),
                });
                if (showMethod == null)
                {
                    Plugin.Log.LogWarning("ツールチップを出し直すメソッドが引けないので、キーを押しても出し直さない（次にツールチップを出すときから効く）");
                }
            }

            return showMethod;
        }

        public static void Guard(string where, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"{where}で止まった: {e}");
            }
        }
    }

    [HarmonyPatch(typeof(UITooltip), "AddItemWidgets", new[]
    {
        typeof(List<LazyWidgetDataBase>), typeof(ItemDef), typeof(bool), typeof(string), typeof(bool), typeof(bool), typeof(IReadOnlyList<ItemDef>),
    })]
    internal static class AddItemWidgetsPatch
    {
        private static void Postfix(List<LazyWidgetDataBase> __0, ItemDef __1)
        {
            TooltipHook.Guard("品の説明の組み立て", () => TooltipHook.Remember(__0, __1));
        }
    }

    [HarmonyPatch(typeof(UITooltip), "AddItemEffectWidgets", new[]
    {
        typeof(List<LazyWidgetDataBase>), typeof(ItemDef), typeof(IReadOnlyList<ItemDef>),
    })]
    internal static class AddItemEffectWidgetsPatch
    {
        private static void Postfix(List<LazyWidgetDataBase> __0, ItemDef __1)
        {
            TooltipHook.Guard("品の効果の組み立て", () => TooltipHook.Remember(__0, __1));
        }
    }

    [HarmonyPatch(typeof(UITooltip), "ShowAtTargetAndCalculateOffsets", new[]
    {
        typeof(List<LazyWidgetDataBase>), typeof(RectTransform), typeof(UIBasicBubble.ForceCornerPosition), typeof(bool), typeof(Vector2),
        typeof(TooltipPlacementPriority),
    })]
    internal static class ShowTooltipPatch
    {
        private static void Prefix(List<LazyWidgetDataBase> __0, RectTransform __1, UIBasicBubble.ForceCornerPosition __2, bool __3, Vector2 __4,
            TooltipPlacementPriority __5)
        {
            TooltipHook.Guard("ツールチップを出す前", () => TooltipHook.OnShow(__0, __1, __2, __3, __4, __5));
        }
    }
}
