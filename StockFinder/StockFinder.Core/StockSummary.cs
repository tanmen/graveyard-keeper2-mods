using System.Collections.Generic;
using System.Linq;

namespace StockFinder.Core
{
    /// <summary>
    /// ツールチップに出す在庫のまとめ。合計は所持品と宝箱の全部。並びは、所持品が先頭、そのあと場所ごと
    /// （場所の合計の多い順。同じなら見つけた順）に、場所の中は宝箱の数の多い順（同じなら見つけた順）。数が 0 の所は出さない。
    /// 行が上限を超えたら、上限の 1 つ手前までを出し、残りは「ほか N か所」の 1 行にまとめる。
    /// 同じ場所の同じ種類の宝箱は、まとめずに 1 つずつの行にする。
    /// </summary>
    public sealed class StockSummary
    {
        private StockSummary(int total, List<StockPlace> rows, int hiddenPlaces, int hiddenCount)
        {
            Total = total;
            Rows = rows;
            HiddenPlaces = hiddenPlaces;
            HiddenCount = hiddenCount;
        }

        public int Total { get; }

        /// <summary>出す行（上の順）。</summary>
        public IReadOnlyList<StockPlace> Rows { get; }

        /// <summary>上限で出さなかった所の数（0 なら「ほか」の行は無い）。</summary>
        public int HiddenPlaces { get; }

        /// <summary>出さなかった所にある数の合計。</summary>
        public int HiddenCount { get; }

        /// <param name="places">所持品と宝箱（見つけた順）。null と数が 0 以下のものは無視する。</param>
        /// <param name="maxRows">「ほか」の行も含めた行の上限（1 未満は 1 として扱う）。</param>
        public static StockSummary Create(IEnumerable<StockPlace> places, int maxRows)
        {
            List<StockPlace> found = (places ?? Enumerable.Empty<StockPlace>()).Where(p => p != null && p.Count > 0).ToList();
            int total = found.Sum(p => p.Count);

            var ordered = new List<StockPlace>();
            ordered.AddRange(found.Where(p => p.IsPlayer));
            IEnumerable<IGrouping<string, (StockPlace Place, int Index)>> zones = found
                .Select((p, i) => (Place: p, Index: i))
                .Where(x => !x.Place.IsPlayer)
                .GroupBy(x => x.Place.Zone)
                .OrderByDescending(g => g.Sum(x => x.Place.Count))
                .ThenBy(g => g.Min(x => x.Index));
            foreach (IGrouping<string, (StockPlace Place, int Index)> zone in zones)
            {
                ordered.AddRange(zone.OrderByDescending(x => x.Place.Count).ThenBy(x => x.Index).Select(x => x.Place));
            }

            if (maxRows < 1)
            {
                maxRows = 1;
            }

            if (ordered.Count <= maxRows)
            {
                return new StockSummary(total, ordered, 0, 0);
            }

            int shown = maxRows - 1;
            List<StockPlace> rest = ordered.Skip(shown).ToList();
            return new StockSummary(total, ordered.Take(shown).ToList(), rest.Count, rest.Sum(p => p.Count));
        }
    }
}
