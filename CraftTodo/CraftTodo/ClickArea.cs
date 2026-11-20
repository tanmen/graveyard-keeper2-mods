using System;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CraftTodo
{
    /// <summary>
    /// 何も描かずに、左クリックだけを受け取る面（制作メニューの札を押してセルの段を出し入れする用）。
    /// ScrollArea と同じく Graphic を継いでメッシュを空にする。ホイールは受けないので、上の親（Viewport の ScrollArea）へ伝わる。
    /// ⚠ 当たる面なので、この上ではゲームの左クリック（攻撃など）が止まる（ゲームは UI の上ではクリックを止める: MouseAimHelper.IsPointerOverUI）。
    /// </summary>
    internal sealed class ClickArea : Graphic, IPointerClickHandler
    {
        public Action Clicked;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                Clicked?.Invoke();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
        }
    }
}
