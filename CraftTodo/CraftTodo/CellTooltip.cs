using System;
using CraftTodo.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CraftTodo
{
    /// <summary>
    /// 制作メニューのセル（作る物・素材）に乗ったらゲームのツールチップを出し、離れたら消す（0.6.0。TooltipBridge）。
    /// セルの地の Image を当たる面にして、同じ GameObject に付ける。
    /// ⚠ 札の当たる面（ClickArea）はセルの兄弟なので、セルが当たると札の左クリックが届かない → ここで受けて札の出し入れへ回す。
    /// ホイールは受けないので、上の親（Viewport の ScrollArea）へ伝わる。
    /// 並べ直し・札を畳む・制作メニューを隠すとセルが消えるので、そのとき自分に出ていれば消す（ゲームのツールチップは相手が消えても残る）。
    /// </summary>
    internal sealed class CellTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public TodoEntry Entry;

        /// <summary>負なら作る物、そうでなければ素材の番号。</summary>
        public int MaterialIndex = -1;

        public Action Clicked;

        private RectTransform Target => transform as RectTransform;

        public void OnPointerEnter(PointerEventData eventData)
        {
            TooltipBridge.Show(Entry, MaterialIndex, Target);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // ゲームの UIMouseTooltip と同じく、子へ移っただけのときは消さない（セルの子は当たる面を持たないが、念のため）
            if (eventData.fullyExited)
            {
                TooltipBridge.HideAt(Target, false);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                Clicked?.Invoke();
            }
        }

        private void OnDisable()
        {
            TooltipBridge.HideAt(Target, true);
        }
    }
}
