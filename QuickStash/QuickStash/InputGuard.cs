using System;
using System.Collections.Generic;
using LazyBearTechnology;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace QuickStash
{
    /// <summary>
    /// しまうキーを受けてよい場面か。歩いているとき（窓が無く、操作を奪われていない）と、所持品の窓（人物の窓 CharacterWindow）・
    /// 宝箱の窓（UIBaseChestWindow）がいちばん上にあるときだけ。会話・演出・眠り・ESC の窓・制作の窓・文字の入力中は受けない。
    /// 受けなかった理由は、種類ごとに 1 度だけログに出す（キーが効かないと言われたときの手掛かり）。
    /// </summary>
    internal static class InputGuard
    {
        private static readonly HashSet<string> LoggedRefusals = new HashSet<string>();

        public static bool CanStash()
        {
            if (!GameBridge.TryGetSession(out _, out _))
            {
                return false;
            }

            if (IsTyping())
            {
                return Refuse("文字の入力中");
            }

            PlayerController controller = GameBridge.Controller;
            if (controller == null)
            {
                return Refuse("プレイヤーが見つからない");
            }

            LazyWidgetBase top = LazyWindowsStackController.ActiveWindow;
            if (top != null)
            {
                if (!(top is CharacterWindow) && !(top is UIBaseChestWindow))
                {
                    return Refuse("窓 " + top.GetType().Name + " がいちばん上にある");
                }

                // 窓が操作を奪っている（ByUI）のは当然なので、それ以外（会話・演出・眠りなど）で奪われていないかだけを見る
                return controller.IsControlsEnabledExcept(TakenControlType.ByUI) || Refuse("窓の奥で操作が奪われている");
            }

            return controller.IsControlsEnabled || Refuse("操作が奪われている（会話・演出・眠り・建築など）");
        }

        /// <summary>文字の入力欄にカーソルがあるか（名前を付ける窓など）。</summary>
        public static bool IsTyping()
        {
            try
            {
                GameObject selected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
                if (selected == null)
                {
                    return false;
                }

                TMP_InputField tmp = selected.GetComponent<TMP_InputField>();
                if (tmp != null && tmp.isFocused)
                {
                    return true;
                }

                InputField legacy = selected.GetComponent<InputField>();
                return legacy != null && legacy.isFocused;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool Refuse(string why)
        {
            if (LoggedRefusals.Add(why))
            {
                Plugin.Log.LogInfo($"しまうキーを受けなかった: {why}（同じ理由は 2 度目から出さない）");
            }

            return false;
        }
    }
}
