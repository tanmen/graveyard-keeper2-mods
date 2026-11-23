using System;
using System.Collections.Generic;
using System.Text;

namespace TradeFinder.Core
{
    /// <summary>文言と文字の形（mod 本体がゲームの訳文と TextStyle で埋める。テストは素の文字で埋める）。</summary>
    public sealed class TradeWords
    {
        /// <summary>「買える」「売れる」。</summary>
        public Func<TradeSide, string> Label { get; set; } = side => side == TradeSide.Buy ? "Buy" : "Sell";

        /// <summary>見出しと名前の間（「: 」）。</summary>
        public string LabelSeparator { get; set; } = ": ";

        /// <summary>名前に添える括弧（「（町）」）。</summary>
        public Func<string, string> Paren { get; set; } = inner => " (" + inner + ")";

        public string NotBuilt { get; set; } = "not built";

        /// <summary>「ステージ II から」。</summary>
        public Func<int, string> FromTier { get; set; } = tier => "from tier " + tier;

        /// <summary>括弧の中で 2 つを並べる（「未建設、ステージ II から」）。</summary>
        public string StateJoin { get; set; } = ", ";

        /// <summary>値段（ゲームのお金のアイコン付き）。</summary>
        public Func<int, string> Money { get; set; } = price => price.ToString();

        /// <summary>幸福のアイコン。</summary>
        public string HappinessIcon { get; set; } = "<3";

        /// <summary>見出しの文字の形（濃い）。</summary>
        public Func<string, string> Heading { get; set; } = s => s;

        /// <summary>いま取引できる所の文字の形。</summary>
        public Func<string, string> Plain { get; set; } = s => s;

        /// <summary>まだ取引できない所の文字の形（薄い）。</summary>
        public Func<string, string> Dim { get; set; } = s => s;
    }

    /// <summary>
    /// ツールチップの行（1 行 1 部品）: 向きごとに、1 行目は「買える: 名前（場所） 値段」、2 行目からは「名前（場所） 値段」。
    /// まだ取引できない所は薄い形で「名前（未建設）」「名前（ステージ II から）」。売ると幸福が入る所は最後に幸福のアイコン。
    /// </summary>
    public static class TradeText
    {
        public static List<string> Lines(IEnumerable<TradeGroup> groups, TradeOptions options, TradeWords words)
        {
            options = options ?? new TradeOptions();
            words = words ?? new TradeWords();
            bool place = TradeOptions.Shows(options.Place, options.KeyHeld);
            bool price = TradeOptions.Shows(options.Price, options.KeyHeld);
            bool happiness = TradeOptions.Shows(options.Happiness, options.KeyHeld);
            var lines = new List<string>();
            if (groups == null)
            {
                return lines;
            }

            foreach (TradeGroup group in groups)
            {
                for (int i = 0; i < group.Offers.Count; i++)
                {
                    var line = new StringBuilder();
                    if (i == 0)
                    {
                        line.Append(words.Heading(words.Label(group.Side) + words.LabelSeparator));
                    }

                    line.Append(Entry(group.Offers[i], place, price, happiness, words));
                    lines.Add(line.ToString());
                }
            }

            return lines;
        }

        public static string Entry(TradeOffer offer, bool place, bool price, bool happiness, TradeWords words)
        {
            var text = new StringBuilder(offer.Name ?? offer.VendorId ?? string.Empty);
            string note = Note(offer, place, words);
            if (!string.IsNullOrEmpty(note))
            {
                text.Append(words.Paren(note));
            }

            string body = offer.IsNow ? words.Plain(text.ToString()) : words.Dim(text.ToString());
            var line = new StringBuilder(body);
            if (offer.IsNow && price && offer.Price > 0)
            {
                line.Append(' ').Append(words.Plain(words.Money(offer.Price)));
            }

            if (happiness && offer.Happiness)
            {
                line.Append(' ').Append(offer.IsNow ? words.HappinessIcon : words.Dim(words.HappinessIcon));
            }

            return line.ToString();
        }

        /// <summary>括弧の中: いまなら場所（設定で出すときだけ）、まだなら「未建設」「ステージ II から」。</summary>
        private static string Note(TradeOffer offer, bool place, TradeWords words)
        {
            switch (offer.State)
            {
                case TradeState.Now:
                    return place ? offer.Place : null;
                case TradeState.NotBuilt:
                    return offer.Tier > 1 ? words.NotBuilt + words.StateJoin + words.FromTier(offer.Tier) : words.NotBuilt;
                default:
                    return words.FromTier(offer.Tier);
            }
        }
    }
}
