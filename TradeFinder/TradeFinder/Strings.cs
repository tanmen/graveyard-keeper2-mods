using System;
using LazyBearTechnology;
using TradeFinder.Core;

namespace TradeFinder
{
    /// <summary>
    /// 文言。商人・店・場所・段階の名前はゲームの訳文を使い、mod 自身の文言（買える・売れる・未建設）はゲームの言語が日本語なら日本語、
    /// それ以外は英語。字はゲームの日本語の訳文（lng_ja）に出てくる字だけ（2026-11-23 に数えた。「・」「×」は書体に無い）。
    /// </summary>
    internal static class Strings
    {
        public static bool Japanese => CurrentLang() == "ja";

        public static string Label(TradeSide side) => side == TradeSide.Buy ? (Japanese ? "買える" : "Buy at") : (Japanese ? "売れる" : "Sell at");

        /// <summary>見出しと名前の間（ゲームの「作成場所: 」と同じく半角のコロンと空白）。</summary>
        public const string LabelSeparator = ": ";

        public static string Paren(string inner) => Japanese ? "（" + inner + "）" : " (" + inner + ")";

        public static string NotBuilt => Japanese ? "未建設" : "not built";

        public static string StateJoin => Japanese ? "、" : ", ";

        /// <summary>「ステージ II から」。段階の名前は商人の窓の見出しと同じ訳文 ui_vendor_tier_N。</summary>
        public static string FromTier(int tier)
        {
            string name = L("ui_vendor_tier_" + tier, (Japanese ? "ステージ " : "Tier ") + tier);
            return Japanese ? name + " から" : "from " + name;
        }

        /// <summary>場所（ゾーン）の名前。右上の地名の枠と同じ訳文 wz_&lt;id&gt;。</summary>
        public static string ZoneName(string zoneId) => L("wz_" + zoneId, null);

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

        /// <summary>訳文。鍵が無ければ <paramref name="fallback"/>（ゲームの LLBase.L は鍵の文字をそのまま返すので、先に HasL で見る）。</summary>
        public static string L(string id, string fallback)
        {
            if (string.IsNullOrEmpty(id))
            {
                return fallback;
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
