using System;
using System.Collections.Generic;
using System.Linq;

namespace TradeFinder.Core
{
    /// <summary>設定と、いまキーを押しているか。</summary>
    public sealed class TradeOptions
    {
        public ShowWhen Buy { get; set; } = ShowWhen.Always;

        public ShowWhen Sell { get; set; } = ShowWhen.Always;

        /// <summary>まだ取引できない所（町の店を建てていない・商人の段階が上がると扱う）。</summary>
        public ShowWhen Unavailable { get; set; } = ShowWhen.Always;

        public ShowWhen Place { get; set; } = ShowWhen.Always;

        public ShowWhen Price { get; set; } = ShowWhen.Always;

        public ShowWhen Happiness { get; set; } = ShowWhen.Always;

        public bool KeyHeld { get; set; }

        public static bool Shows(ShowWhen when, bool keyHeld) => when == ShowWhen.Always || (when == ShowWhen.WhileKey && keyHeld);

        /// <summary>どれかがキーを押している間だけ出す（キーを押す・離すとツールチップを組み直す）。</summary>
        public bool UsesKey => new[] { Buy, Sell, Unavailable, Place, Price, Happiness }.Contains(ShowWhen.WhileKey);
    }

    /// <summary>「買える」か「売れる」の、ひとまとまりの行。</summary>
    public sealed class TradeGroup
    {
        public TradeGroup(TradeSide side, List<TradeOffer> offers)
        {
            Side = side;
            Offers = offers;
        }

        public TradeSide Side { get; }

        public List<TradeOffer> Offers { get; }
    }

    /// <summary>出す行を選んで並べる。「買える」→「売れる」の順。</summary>
    public static class TradeLayout
    {
        /// <summary>
        /// 向きごとに、いま取引できる所を先に（買えるは安い順・売れるは高い順、値段が同じなら名前の順）、
        /// そのあとに町の店を建てていない所、段階が上がると扱う所（段階の低い順）。行の無い向きは出さない。
        /// </summary>
        public static List<TradeGroup> Arrange(IEnumerable<TradeOffer> offers, TradeOptions options)
        {
            options = options ?? new TradeOptions();
            var groups = new List<TradeGroup>();
            List<TradeOffer> all = (offers ?? Enumerable.Empty<TradeOffer>()).Where(o => o != null).ToList();
            bool unavailable = TradeOptions.Shows(options.Unavailable, options.KeyHeld);
            foreach (TradeSide side in new[] { TradeSide.Buy, TradeSide.Sell })
            {
                if (!TradeOptions.Shows(side == TradeSide.Buy ? options.Buy : options.Sell, options.KeyHeld))
                {
                    continue;
                }

                List<TradeOffer> rows = all
                    .Where(o => o.Side == side && (o.IsNow || unavailable))
                    .OrderBy(o => (int)o.State)
                    .ThenBy(o => o.IsNow ? PriceKey(o.Price, side) : o.Tier)
                    .ThenBy(o => o.Name ?? string.Empty, StringComparer.Ordinal)
                    .ToList();
                if (rows.Count > 0)
                {
                    groups.Add(new TradeGroup(side, rows));
                }
            }

            return groups;
        }

        // 買えるは安い順・売れるは高い順。値段の分からない所（0）はどちらも後ろに回す
        private static long PriceKey(int price, TradeSide side)
        {
            if (price <= 0)
            {
                return long.MaxValue;
            }

            return side == TradeSide.Buy ? price : -(long)price;
        }
    }
}
