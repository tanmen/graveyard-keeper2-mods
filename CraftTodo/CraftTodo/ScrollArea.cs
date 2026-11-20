using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CraftTodo
{
    /// <summary>
    /// 何も描かずに、マウスのホイールだけを受け取る面（制作メニューの中身のスクロール用）。
    /// uGUI の GraphicRaycaster は Graphic にしか当たらないので Graphic を継ぎ、メッシュは空にする。
    /// 中身があふれていないときは raycastTarget を切る（板の上でゲームの左クリックの攻撃などを止めないため。
    /// ゲームは UI の上ではクリックを止める: MouseAimHelper.IsPointerOverUI）。
    /// </summary>
    internal sealed class ScrollArea : Graphic, IScrollHandler
    {
        /// <summary>ホイールの 1 目盛り（上が正）。</summary>
        public Action<float> Scrolled;

        public void OnScroll(PointerEventData eventData)
        {
            Vector2 delta = eventData.scrollDelta;
            float amount = Mathf.Abs(delta.x) > Mathf.Abs(delta.y) ? delta.x : delta.y;
            if (amount != 0f)
            {
                Scrolled?.Invoke(amount);
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
        }
    }
}
