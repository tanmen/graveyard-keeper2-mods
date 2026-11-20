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
    }
}
