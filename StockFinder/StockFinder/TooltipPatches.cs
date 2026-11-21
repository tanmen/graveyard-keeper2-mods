using System;
using System.Collections.Generic;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;

namespace StockFinder
{
    // ゲームのツールチップ（UITooltip）に差し込む所。根拠は StockFinder/docs/game-api-notes.md。
    //
    // ツールチップは出すたびに中身の部品の並び（List<LazyWidgetDataBase>）を組み立てて ShowAtTargetAndCalculateOffsets に渡す。
    // 品の説明を足す AddItemWidgets（所持品・宝箱・出来上がりのセル・技術ツリー・注文など）と、品の効果を足す AddItemEffectWidgets
    // （制作の窓のレシピ・キュー。AddItemWidgets の中からも呼ばれる）で「この並びはこの品のもの」と覚えておき、
    // 出す直前に同じ並びなら一番下に在庫を足す。並びは参照で照らすので、組み立てて出さなかった並びの品が別のツールチップに混ざらない。
    // 1 つの並びに品が 2 つ以上来たら、最初の品だけ。

    internal static class TooltipHook
    {
        private const int LostWarnAt = 5;

        private static List<LazyWidgetDataBase> pendingList;
        private static ItemDef pendingItem;
        private static int appended;
        private static int lost;

        public static void Remember(List<LazyWidgetDataBase> list, ItemDef item)
        {
            if (list == null || item == null || ReferenceEquals(list, pendingList))
            {
                return;
            }

            if (pendingList != null)
            {
                // 覚えた並びが出されないまま次の並びが来た。ふだんは起きない（組み立てた並びはすぐ出す）ので、
                // 1 度も足せていないまま続くなら、出す所の差し込みが効いていない（ゲームの更新で呼び方が変わった等）
                lost++;
                if (appended == 0 && lost == LostWarnAt)
                {
                    Plugin.Log.LogWarning($"品を {lost} 回覚えたのに、ツールチップを出す所で 1 度も在庫を足せていない（出す所の差し込みが効いていない可能性）");
                }
            }

            pendingList = list;
            pendingItem = item;
        }

        /// <summary>出す直前。覚えた並びなら在庫を足して true。</summary>
        public static bool AppendIfRemembered(List<LazyWidgetDataBase> list)
        {
            if (list == null || !ReferenceEquals(list, pendingList))
            {
                return false;
            }

            ItemDef item = pendingItem;
            pendingList = null;
            pendingItem = null;
            appended++;
            StockSection.Append(list, item);
            return true;
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
        private static void Prefix(List<LazyWidgetDataBase> __0, out bool __state)
        {
            bool appended = false;
            TooltipHook.Guard("ツールチップを出す前", () => appended = TooltipHook.AppendIfRemembered(__0));
            __state = appended;
        }

        private static void Postfix(UITooltip __instance, bool __state)
        {
            if (__state)
            {
                TooltipHook.Guard("ツールチップを出したあと", () => StockSection.CheckRendered(__instance));
            }
        }
    }
}
