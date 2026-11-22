using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace QuickStash
{
    /// <summary>
    /// ロックのキー（既定はホイールの押し込み）を押したとき、マウスの下の所持品のセルの品のロックを切り替える。
    /// ゲームの UI のレイキャスト（EventSystem.RaycastAll）で当たった部品を手前から見て、最初に当たったアイテムのセル（UIItemCell）だけで決める。
    /// そのセルが所持品の窓の部品（InventoryWidget の在庫が所持品）でなければ何もしない（宝箱・制作の窓・袋の中のセルなど）。
    /// ゲーム自身はホイールの押し込みを使っていない（逆コンパイルで GetMouseButton(2) も InputButton.Middle も 0 件）。
    /// Craft Todo の登録キー（同じホイール）はレシピの部品にしか反応しないので、所持品のセルでは重ならない。
    /// </summary>
    internal static class LockPicker
    {
        private static readonly List<RaycastResult> Hits = new List<RaycastResult>();

        public static bool TryToggleUnderPointer(LockStorage storage)
        {
            if (storage.SlotRoot == null || InputGuard.IsTyping())
            {
                return false;
            }

            UIItemCell cell = CellUnderPointer();
            if (cell == null)
            {
                return false;
            }

            InventoryWidget widget = cell.GetComponentInParent<InventoryWidget>();
            if (widget == null || !GameBridge.IsPlayerInventory(widget.Data?.Inventory))
            {
                return false;
            }

            Item item = cell.DisplayingItem;
            if (item == null || item.IsEmpty || string.IsNullOrEmpty(item.id))
            {
                return false;
            }

            bool locked = storage.Toggle(item.id);
            LockMarks.Refresh(widget);
            GameBridge.PlaySound("gui_click");
            Plugin.Log.LogInfo($"{(locked ? "ロックした" : "ロックを外した")}: {item.id}（{GameBridge.ItemName(item)}）・セーブ枠 {storage.SlotRoot} のロック {storage.Current.Count} 種");
            return true;
        }

        private static UIItemCell CellUnderPointer()
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
                    UIItemCell cell = hit.gameObject == null ? null : hit.gameObject.GetComponentInParent<UIItemCell>();
                    if (cell != null)
                    {
                        return cell;
                    }
                }

                return null;
            }
            finally
            {
                Hits.Clear();
            }
        }
    }
}
