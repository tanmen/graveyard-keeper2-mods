using System;
using LazyBearTechnology;

namespace StockFinder
{
    /// <summary>
    /// 文言。所持品・場所・宝箱の名前はゲームの訳文を使い、mod 自身の文言（合計・ほか N か所）はゲームの言語が日本語なら日本語、
    /// それ以外は英語。ゲームの書体に「×」「・」が無い（訳文に 1 度も出てこない）ので使わない。
    /// </summary>
    internal static class Strings
    {
        private static bool Japanese => CurrentLang() == "ja";

        public static string Total => Japanese ? "合計" : "Total";

        /// <summary>所持品の行の名前（ゲームの所持品の窓の見出しと同じ訳文 inventory）。</summary>
        public static string PlayerInventory => L("inventory", Japanese ? "所持品" : "Inventory");

        /// <summary>どの場所（ゾーン）にも入っていない宝箱の場所の名前。</summary>
        public static string Elsewhere => Japanese ? "ほかの場所" : "Elsewhere";

        public static string MorePlaces(int places) => Japanese ? "ほか " + places + " か所" : places + " more";

        /// <summary>Lines の並べ方で、名前と数の間に置く（ゲームの「作成場所: 」と同じく半角のコロンと空白）。</summary>
        public const string LinesSeparator = ": ";

        /// <summary>場所（ゾーン）の名前。右上の地名の枠と同じ訳文 wz_&lt;id&gt;。</summary>
        public static string ZoneName(string zoneId) => L("wz_" + zoneId, zoneId);

        /// <summary>置いてある物（宝箱）の名前。所持品の窓の見出しと同じく、定義の id の訳文。</summary>
        public static string ObjectName(string wgoId) => L(wgoId, wgoId);

        public static string CurrentLang()
        {
            try
            {
                return LLBase.CurrentLang;
            }
            catch (Exception)
            {
                return "en";
            }
        }

        private static string L(string id, string fallback)
        {
            if (string.IsNullOrEmpty(id))
            {
                return fallback ?? string.Empty;
            }

            try
            {
                return LLBase.HasL(id) ? LLBase.L(id) : fallback;
            }
            catch (Exception)
            {
                return fallback;
            }
        }
    }
}
