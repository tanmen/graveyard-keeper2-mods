using BepInEx.Configuration;
using UnityEngine;

namespace ModsMenu
{
    /// <summary>
    /// Mods Menu 自身の文言（タブと行の文言は各 mod が渡す）。ゲームの言語が日本語なら日本語、それ以外は英語。
    /// ⚠ ゲームの日本語の書体に無い字がある（Craft Todo 0.7.0 で「・」と「縦」が描かれなかった）。文言はゲームの訳文に出てくる字で書く。
    /// </summary>
    internal static class Strings
    {
        private static bool Japanese => GameBridge.CurrentLang() == "ja";

        /// <summary>ESC の窓のボタン（本人の求めで日本語でも「Mods」）。</summary>
        public const string PauseButton = "Mods";

        /// <summary>窓の題（木のタブが引けなかったときだけ見出しの帯に出す）。</summary>
        public const string Title = "Mods";

        /// <summary>木のタブが引けなかったときの、いちばん上の切り替えの行の名前。</summary>
        public const string ModLabel = "Mod";

        public static string PressKey => Japanese ? "キーを押す…（Esc でやめる）" : "Press a key... (Esc cancels)";

        public static string NoKey => Japanese ? "なし" : "None";

        /// <summary>キーの名前。修飾キーを先に「+」でつなぐ。マウスのボタンは言葉にする。</summary>
        public static string KeyName(KeyboardShortcut shortcut)
        {
            if (shortcut.MainKey == KeyCode.None)
            {
                return NoKey;
            }

            string text = string.Empty;
            foreach (KeyCode modifier in shortcut.Modifiers)
            {
                text += KeyName(modifier) + "+";
            }

            return text + KeyName(shortcut.MainKey);
        }

        public static string KeyName(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Mouse0: return Japanese ? "左クリック" : "Left click";
                case KeyCode.Mouse1: return Japanese ? "右クリック" : "Right click";
                case KeyCode.Mouse2: return Japanese ? "ホイール押し込み" : "Middle click";
                case KeyCode.Mouse3: return Japanese ? "マウス 4" : "Mouse 4";
                case KeyCode.Mouse4: return Japanese ? "マウス 5" : "Mouse 5";
                case KeyCode.LeftShift:
                case KeyCode.RightShift: return "Shift";
                case KeyCode.LeftControl:
                case KeyCode.RightControl: return "Ctrl";
                case KeyCode.LeftAlt:
                case KeyCode.RightAlt: return "Alt";
                default: return key.ToString();
            }
        }
    }
}
