using System.Collections.Generic;
using CraftTodo.Core;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CraftTodo
{
    /// <summary>
    /// 登録キー・減らすキー（既定はホイールの押し込み・左 Shift＋ホイール）を押したとき、マウスの下にあるレシピを引く。
    /// ゲームの UI のレイキャスト（EventSystem.RaycastAll）で当たった部品を手前から順に見て、
    /// 最初にレシピとして読めたものを返す。作れるかどうかは問わない（作れるものも登録できる）。
    /// 返すメモの回数が、1 回押したときに足す（減らす）数になる。
    ///
    /// | 当たった部品 | 返すもの（回数） |
    /// | --- | --- |
    /// | 建物の窓の行（UIBuildingWidget。素材のセルも含む） | その建物・拡張を 1 つ |
    /// | 町の建物の窓の行（UITownBuildingWidget。住居の「大きな家を修理する」など。素材のセルも含む） | その建物を 1 つ |
    /// | 作業台のレシピのアイコン（UICraftPreviewItemCell） | そのレシピを 1 回分 |
    /// | 作業台のキューの行（UICraftQueueElementWidget） | そのレシピを、キューの回数分 |
    /// | 製作の行（UICraftWidget） | そのレシピを、行で選んだ回数分 |
    /// | 回数を選ぶ窓（UIBaseCraftSelectionWindow）のどこか | そのレシピを、窓で選んだ回数分 |
    /// </summary>
    internal static class TodoPicker
    {
        private static readonly List<RaycastResult> Hits = new List<RaycastResult>();

        public static TodoEntry PickUnderPointer()
        {
            EventSystem events = EventSystem.current;
            if (events == null)
            {
                return null;
            }

            var pointer = new PointerEventData(events) { position = Input.mousePosition };
            Hits.Clear();
            events.RaycastAll(pointer, Hits);
            try
            {
                foreach (RaycastResult hit in Hits)
                {
                    TodoEntry entry = hit.gameObject == null ? null : FromObject(hit.gameObject);
                    if (entry != null)
                    {
                        return entry;
                    }
                }

                return null;
            }
            finally
            {
                Hits.Clear();
            }
        }

        private static TodoEntry FromObject(GameObject hit)
        {
            UIBuildingWidget building = hit.GetComponentInParent<UIBuildingWidget>();
            if (building != null)
            {
                BuildData build = DataOf<UIBuildingWidgetData>(building)?.BuildData;
                bool isRecipe = build?.Definition != null && !build.IsMoveSelection && !build.IsMovePlacement && build.BuildingMode != BuildingDef.BuildingMode.Remove;
                return isRecipe ? GameBridge.FromBuild(build, GameBridge.StationOfBuildingWidget(building)) : null;
            }

            UITownBuildingWidget town = hit.GetComponentInParent<UITownBuildingWidget>();
            if (town != null)
            {
                TownBuildingDef def = DataOf<UITownBuildingWidgetData>(town)?.TownBuildingDef;
                return def == null ? null : GameBridge.FromTownBuilding(def, GameBridge.StationOfTownBuildingWidget(town));
            }

            UICraftPreviewItemCell cell = hit.GetComponentInParent<UICraftPreviewItemCell>();
            if (cell != null)
            {
                // タブ（拡張の切り替え）と、まだ覚えていないレシピ（中身が見えない）は登録しない
                UICraftPreviewItemCellData data = DataOf<UICraftPreviewItemCellData>(cell);
                bool isRecipe = data != null && !data.IsTab && data.CraftDef != null && !data.IsUnknown;
                return isRecipe ? GameBridge.FromCraftDef(data.CraftDef, data.WgoData, 1) : null;
            }

            UICraftQueueElementWidget queued = hit.GetComponentInParent<UICraftQueueElementWidget>();
            if (queued != null)
            {
                UICraftQueueElementWidgetData data = DataOf<UICraftQueueElementWidgetData>(queued);
                CraftElementBase element = data?.CraftQueueElement;
                return element?.Def == null ? null : GameBridge.FromCraftDef(element.Def, data.WgoData, element.Count);
            }

            UICraftWidget widget = hit.GetComponentInParent<UICraftWidget>();
            if (widget != null)
            {
                UIBaseCraftWidgetData data = DataOf<UIBaseCraftWidgetData>(widget);
                return data?.CraftDefinition == null ? null : GameBridge.FromCraftDef(data.CraftDefinition, data.WgoData, data.CraftsCount);
            }

            UIBaseCraftSelectionWindow window = hit.GetComponentInParent<UIBaseCraftSelectionWindow>();
            return window == null ? null : GameBridge.FromCraft(GameBridge.DataOf(window));
        }

        /// <summary>LazyWidget&lt;T&gt; の protected な data を読む。</summary>
        private static T DataOf<T>(Component widget) where T : class
        {
            return Traverse.Create(widget).Field("data").GetValue<T>();
        }
    }
}
