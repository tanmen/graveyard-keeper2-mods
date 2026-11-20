using System;
using System.Collections.Generic;
using CraftTodo.Core;
using HarmonyLib;
using LazyBearTechnology;

namespace CraftTodo
{
    // ゲームの窓に差し込む所。どこに差し込むかの根拠は CraftTodo/docs/game-api-notes.md。
    // 1 つの差し込みが落ちてもゲームを止めないよう、本体は全部 Hooks.Guard の中で動かす。

    /// <summary>
    /// 建物の窓の行を押したとき（UIBuildingWidget.OnPress）。設置を伴わない建物（BuildingMode.Script）は押した時点で建つので
    /// メモを 1 減らす（設置するものは BuildPlacedPatch が設置の確定で減らす）。
    /// 足りなければ、設定 AddOnBlockedClick のときだけメモへ。
    /// </summary>
    [HarmonyPatch(typeof(UIBuildingWidget), "OnPress", new Type[0])]
    internal static class BuildingWidgetPressPatch
    {
        private static void Prefix(UIBuildingWidget __instance, UIBuildingWidgetData ___data)
        {
            Hooks.Guard("建物の行の押下", () => Hooks.OnBuildingPressed(__instance, ___data));
        }
    }

    /// <summary>建物を設置したとき（BuildPointer.TryBuildActionInput が true を返した）。そのメモを 1 減らす。</summary>
    [HarmonyPatch(typeof(BuildPointer), nameof(BuildPointer.TryBuildActionInput), new Type[0])]
    internal static class BuildPlacedPatch
    {
        private static void Postfix(bool __result, BuildPointerObject ___pointerObject)
        {
            if (__result)
            {
                Hooks.Guard("建物の設置", () => Hooks.OnBuildingPlaced(___pointerObject));
            }
        }
    }

    /// <summary>製作の窓ができたとき、「作る」ボタンの「押せないときのクリック」を聞く。</summary>
    [HarmonyPatch(typeof(UIBaseCraftSelectionWindow), nameof(UIBaseCraftSelectionWindow.Init), new Type[0])]
    internal static class CraftWindowInitPatch
    {
        private static void Postfix(UIBaseCraftSelectionWindow __instance, LazyButton ___startCraftButton)
        {
            Hooks.Guard("製作の窓の準備", () => Hooks.HookStartButton(__instance, ___startCraftButton));
        }
    }

    /// <summary>ゲームパッド・キーの「作る」（GameKey.StartCraft）。ボタンが押せないときだけメモへ。</summary>
    [HarmonyPatch(typeof(UIBaseCraftSelectionWindow), "OnStartCraft", new Type[0])]
    internal static class CraftWindowStartKeyPatch
    {
        private static void Prefix(UIBaseCraftSelectionWindow __instance, LazyButton ___startCraftButton)
        {
            Hooks.Guard("製作の窓の「作る」キー", () =>
            {
                if (___startCraftButton != null && !___startCraftButton.interactable && ___startCraftButton.gameObject.activeInHierarchy)
                {
                    Hooks.OnCraftBlocked(__instance);
                }
            });
        }
    }

    /// <summary>
    /// 作業台が 1 回分を作り始めたとき（素材を引いた直後。WgoData.OnCraftStart）。そのレシピのメモを 1 減らす。
    /// 窓の「作る」・「キューに追加する」・キューの続き・ゾンビの作業のどれで始まっても、ここを 1 回分ずつ通る
    /// （CraftComponent.Start と、キューを通らない製作の ProcessInstantCraft）。
    /// 0.4.1 までは窓の「作る」（OnStartCraftPressed）で減らしていたので、キューから作ると減らなかった。
    /// </summary>
    [HarmonyPatch(typeof(WgoData), nameof(WgoData.OnCraftStart), new[] { typeof(CraftElementBase) })]
    internal static class CraftUnitStartedPatch
    {
        private static void Postfix(WgoData __instance, CraftElementBase ce)
        {
            Hooks.Guard("製作の開始", () => Hooks.OnCraftUnitStarted(__instance, ce));
        }
    }

    /// <summary>設定 AddWhenCraftWindowOpens が true のとき、作れないレシピの窓を開いた時点でメモへ。</summary>
    [HarmonyPatch(typeof(UIBaseCraftSelectionWindow), nameof(UIBaseCraftSelectionWindow.Open), new[] { typeof(UIBaseCraftSelectionWindowData) })]
    internal static class CraftWindowOpenPatch
    {
        private static void Postfix(UIBaseCraftSelectionWindow __instance)
        {
            Hooks.Guard("製作の窓を開く", () => Hooks.OnCraftWindowOpened(__instance));
        }
    }

    internal static class Hooks
    {
        private static readonly HashSet<int> HookedButtons = new HashSet<int>();
        private static readonly HashSet<string> ReportedFailures = new HashSet<string>();

        /// <summary>差し込んだ処理の例外をゲームに漏らさない。同じ場所の失敗は 1 回だけログに出す。</summary>
        public static void Guard(string where, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                if (ReportedFailures.Add(where))
                {
                    Plugin.Log.LogError($"{where}で失敗した（以後この場所の失敗は出さない）: {e}");
                }
            }
        }

        public static void OnBuildingPressed(UIBuildingWidget widget, UIBuildingWidgetData data)
        {
            BuildData build = data?.BuildData;
            if (build?.Definition == null || build.IsMoveSelection || build.IsMovePlacement || build.BuildingMode == BuildingDef.BuildingMode.Remove)
            {
                return;
            }

            TodoEntry entry = GameBridge.FromBuild(build, GameBridge.StationOfBuildingWidget(widget));
            if (entry == null)
            {
                return;
            }

            if (data.CanBuild(data.GetCurrentNeedItems()))
            {
                if (build.BuildingMode == BuildingDef.BuildingMode.Script)
                {
                    Plugin.Instance.CompleteTodo(entry.Key, 1);
                }
            }
            else if (Plugin.Instance.AddOnBlockedClick.Value)
            {
                Plugin.Instance.AddTodo(entry);
            }
        }

        public static void OnBuildingPlaced(BuildPointerObject pointer)
        {
            BuildData build = pointer == null ? null : pointer.BuildData;
            if (build?.Definition == null || build.IsMoveSelection || build.IsMovePlacement
                || build.BuildingMode == BuildingDef.BuildingMode.Remove || build.BuildingMode == BuildingDef.BuildingMode.Move)
            {
                return;
            }

            string key = TodoEntry.MakeKey(TodoKind.Build, build.Definition.id, GameBridge.StationIdOf(GameBridge.CurrentBuildDesk()));
            Plugin.Instance.CompleteTodo(key, 1);
        }

        public static void HookStartButton(UIBaseCraftSelectionWindow window, LazyButton button)
        {
            if (button == null || !HookedButtons.Add(button.GetInstanceID()))
            {
                return;
            }

            button.onNotInteractableClick.AddListener(() => Guard("灰色の「作る」ボタンの押下", () => OnCraftBlocked(window)));
        }

        /// <summary>灰色の「作る」（マウスでもキーでも）。設定 AddOnBlockedClick のときだけメモへ。</summary>
        public static void OnCraftBlocked(UIBaseCraftSelectionWindow window)
        {
            if (Plugin.Instance.AddOnBlockedClick.Value)
            {
                Plugin.Instance.AddTodo(GameBridge.FromCraft(GameBridge.DataOf(window)));
            }
        }

        /// <summary>作業台が 1 回分を作り始めた。メモは「レシピ＠作業台」で持っているので、同じ作業台の同じレシピだけ減る。</summary>
        public static void OnCraftUnitStarted(WgoData station, CraftElementBase craft)
        {
            CraftDefBase def = craft?.Def;
            if (station == null || def == null || string.IsNullOrEmpty(def.id))
            {
                return;
            }

            string key = TodoEntry.MakeKey(TodoKind.Craft, def.id, GameBridge.StationIdOf(station));
            Plugin.Instance.CompleteTodo(key, 1);
        }

        public static void OnCraftWindowOpened(UIBaseCraftSelectionWindow window)
        {
            if (!Plugin.Instance.AddWhenCraftWindowOpens.Value)
            {
                return;
            }

            UIBaseCraftSelectionWindowData data = GameBridge.DataOf(window);
            if (data != null && !data.CanStartCraft)
            {
                Plugin.Instance.AddTodo(GameBridge.FromCraft(data));
            }
        }
    }
}
