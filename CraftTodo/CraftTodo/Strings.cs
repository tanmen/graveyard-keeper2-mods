namespace CraftTodo
{
    /// <summary>
    /// mod 自身の文言。ゲームの言語が日本語なら日本語、それ以外は英語。
    /// 記号はゲームの書体に字形がある前提のものだけ使う（英語の書体に × は無いことがあるので x にする）。
    /// 全角の空白は書体に字形があるか確かめていないので、区切りは半角の空白 2 つにする。
    /// </summary>
    internal static class Strings
    {
        private static bool Japanese => GameBridge.CurrentLang() == "ja";

        public static string Header => Japanese ? "制作メニュー" : "Craft menu";

        public static string GroupSuffix => Japanese ? "など" : " etc.";

        public static string Close => Japanese ? "×" : "x";

        public static string Times => Japanese ? "×" : "x";

        /// <summary>作る場所が分からないメモをまとめる区切りの名前。</summary>
        public static string NoStation => Japanese ? "制作機なし" : "No station";

        public const string EqualsSign = "=";

        public const string PlusSign = "+";

        /// <summary>1 件ごとの見出し板の文字:「制作物 ×回数  制作機」。そろったかは色で見せる（文字にしない）。</summary>
        public static string Title(string recipe, int multiplier, string station)
        {
            string text = recipe;
            if (multiplier > 1)
            {
                text += " " + Times + multiplier;
            }

            if (!string.IsNullOrEmpty(station))
            {
                text += "  " + station;
            }

            return text;
        }

        /// <summary>作る物のセルの右下（2 回以上のときだけ）。</summary>
        public static string Multiplier(int multiplier) => multiplier > 1 ? Times + multiplier : string.Empty;

        /// <summary>素材のツールチップの最後の行の見出し（ゲームの「作成場所: 」と同じく、半角のコロンと空白）。</summary>
        public static string OtherRecipesLabel => Japanese ? "ほかの作り方: " : "Other recipes: ";

        /// <summary>素材のツールチップの最後の行（出していない作り方の数）。</summary>
        public static string OtherRecipesCount(int count) => Japanese ? "あと " + count + " 通り" : count + " more";

        /// <summary>帯が出せないとき、板の中に書く見出し。</summary>
        public static string HeaderWithKey(string key)
        {
            return string.IsNullOrEmpty(key) ? Header : Header + "  [" + key + "]";
        }

        // ---- 同じ素材の積み上げ（F9 を押したとき。右下のゲームの知らせに出す。出せなければ帯に 2 秒出す）----

        public static string StackingNoticeOn => Japanese ? "制作メニュー: 同じ素材を積み上げて数える" : "Craft menu: stacking shared materials";

        public static string StackingNoticeOff => Japanese ? "制作メニュー: 素材をメモごとに数える" : "Craft menu: counting each memo";

        public static string StackingOn => Japanese ? "積み上げて数える" : "Stacking needs";

        public static string StackingOff => Japanese ? "件ごとに数える" : "Counting each";

        // ---- 設定の窓（ESC の窓の「制作メニュー」から開く。並びは 2026-11-21 に本人が図で決めた）----

        /// <summary>ESC の窓（ポーズ）に足すボタン。</summary>
        public static string PauseButton => Header;

        public static string SettingsTitle => Japanese ? "制作メニューの設定" : "Craft menu settings";

        // ⚠ ゲームの日本語の書体に無い字がある（0.7.0 で「・」と「縦」が描かれなかった。どちらもゲームの訳文に 1 度も出てこない字）。
        // 文言を足すときは、ゲームの訳文に出てくる字で書く
        public static string SectionCounting => Japanese ? "数え方と足し方" : "Counting and adding";


        public static string SectionDisplay => Japanese ? "表示" : "Display";

        public static string SectionKeys => Japanese ? "キー" : "Keys";

        public static string StackMaterials => Japanese ? "同じ素材を積み上げる" : "Stack shared materials";

        public static string AddOnBlockedClick => Japanese ? "足りない行を押したら足す" : "Add when blocked";

        public static string AddWhenCraftWindowOpens => Japanese ? "レシピを開いたら足す" : "Add on opening recipe";

        public static string Visible => Japanese ? "制作メニューを出す" : "Show craft menu";

        public static string MaxEntries => Japanese ? "メモの上限" : "Max entries";

        public static string AutoPosition => Japanese ? "HUD の下に置く" : "Place under HUD";

        public static string OffsetX => Japanese ? "横のずらし" : "Horizontal offset";

        public static string OffsetY => Japanese ? "上下のずらし" : "Vertical offset";

        public static string GameFrame => Japanese ? "ゲームの枠の絵" : "Game frame art";

        public static string ToggleKey => Japanese ? "表示の切り替え" : "Show / hide";

        public static string StackKey => Japanese ? "積み上げの切り替え" : "Toggle stacking";

        public static string RegisterKey => Japanese ? "登録" : "Add";

        public static string RemoveKey => Japanese ? "減らす" : "Remove";

        public static string ClearKey => Japanese ? "全部消す" : "Clear all";

        /// <summary>設定の窓の下の段の、OK の左のボタン。</summary>
        public static string ClearButton => Japanese ? "メモを全部消す" : "Clear all";

        /// <summary>「メモを全部消す」を 1 度押したとき（もう一度押すと消す）。ボタンの幅が変わらないよう、字数をそろえる。</summary>
        public static string ClearConfirm => Japanese ? "もう一度で消す" : "Press again";

        public static string PressKey => Japanese ? "キーを押す…（Esc でやめる）" : "Press a key... (Esc cancels)";

        public static string NoKey => Japanese ? "なし" : "None";

        /// <summary>キーの名前（設定の窓・帯の右端）。修飾キーを先に「+」でつなぐ。マウスのボタンは言葉にする。</summary>
        public static string KeyName(BepInEx.Configuration.KeyboardShortcut shortcut)
        {
            if (shortcut.MainKey == UnityEngine.KeyCode.None)
            {
                return NoKey;
            }

            string text = string.Empty;
            foreach (UnityEngine.KeyCode modifier in shortcut.Modifiers)
            {
                text += KeyName(modifier) + "+";
            }

            return text + KeyName(shortcut.MainKey);
        }

        public static string KeyName(UnityEngine.KeyCode key)
        {
            switch (key)
            {
                case UnityEngine.KeyCode.Mouse0: return Japanese ? "左クリック" : "Left click";
                case UnityEngine.KeyCode.Mouse1: return Japanese ? "右クリック" : "Right click";
                case UnityEngine.KeyCode.Mouse2: return Japanese ? "ホイール押し込み" : "Middle click";
                case UnityEngine.KeyCode.Mouse3: return Japanese ? "マウス 4" : "Mouse 4";
                case UnityEngine.KeyCode.Mouse4: return Japanese ? "マウス 5" : "Mouse 5";
                case UnityEngine.KeyCode.LeftShift:
                case UnityEngine.KeyCode.RightShift: return "Shift";
                case UnityEngine.KeyCode.LeftControl:
                case UnityEngine.KeyCode.RightControl: return "Ctrl";
                case UnityEngine.KeyCode.LeftAlt:
                case UnityEngine.KeyCode.RightAlt: return "Alt";
                default: return key.ToString();
            }
        }
    }
}
