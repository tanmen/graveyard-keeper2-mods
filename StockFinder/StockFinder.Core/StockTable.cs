using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace StockFinder.Core
{
    /// <summary>表の 1 行。どれもゲームの文字の形を当て済みの文字（TextMeshPro の書式入り）。</summary>
    public sealed class StockTableRow
    {
        public StockTableRow(string place, string container, string count)
        {
            Place = place ?? string.Empty;
            Container = container ?? string.Empty;
            Count = count ?? string.Empty;
        }

        /// <summary>左の列: 場所の名前（所持品・「ほか N か所」の行はここだけに書く）。</summary>
        public string Place { get; }

        /// <summary>中の列: 宝箱の名前（無ければ空）。</summary>
        public string Container { get; }

        /// <summary>右の列: 数（右揃え）。</summary>
        public string Count { get; }

        public bool HasContainer => Container.Length > 0;
    }

    /// <summary>
    /// 表の行ごとの文字を作る。ゲームのツールチップの文字の部品は中央揃えしかせず（揃え方は TextStyle が戻す）、部品ごとに
    /// 自分の幅で中央に置かれるので、列は TextMeshPro の <c>&lt;pos=…&gt;</c>（行の頭からの位置）で組み、数は
    /// 「数の列の位置＋数の幅の最大−その数の幅」に置く。こうするとどの行も幅が「数の列の位置＋数の幅の最大」になり、
    /// 1 行ずつ別の部品にして中央に置かれても、行の頭がそろう。
    /// 1 つの部品に改行で詰めないのは、ゲームの小さい書体の行送りが字の高さより狭く、行が重なるため（2026-11-22 に画面で見た）。
    /// 位置は、ゲームと同じ書体・大きさで測った幅（<paramref name="measure"/>）から決める。
    /// 宝箱の列の位置は宝箱のある行の場所の名前だけで決め、所持品・「ほか」の行の長い名前は数の列の位置だけに効かせる。
    /// 位置は整数に丸める（ドット絵の書体がにじまないように）。
    /// </summary>
    public static class StockTable
    {
        public static List<string> Compose(IReadOnlyList<StockTableRow> rows, Func<string, float> measure, float placeGap, float countGap)
        {
            var lines = new List<string>();
            if (rows == null || rows.Count == 0)
            {
                return lines;
            }

            var widths = rows.Select(r => (Place: measure(r.Place), Container: r.HasContainer ? measure(r.Container) : 0f, Count: measure(r.Count))).ToList();

            float containerX = 0f;
            bool anyContainer = false;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].HasContainer)
                {
                    anyContainer = true;
                    containerX = Math.Max(containerX, widths[i].Place);
                }
            }

            containerX = anyContainer ? (float)Math.Ceiling(containerX + placeGap) : 0f;

            float leftEnd = 0f;
            for (int i = 0; i < rows.Count; i++)
            {
                leftEnd = Math.Max(leftEnd, rows[i].HasContainer ? containerX + widths[i].Container : widths[i].Place);
            }

            float countX = (float)Math.Ceiling(leftEnd + countGap);
            float countWidth = widths.Max(w => w.Count);

            for (int i = 0; i < rows.Count; i++)
            {
                var text = new StringBuilder(rows[i].Place);
                if (rows[i].HasContainer)
                {
                    text.Append(Pos(containerX)).Append(rows[i].Container);
                }

                text.Append(Pos(countX + countWidth - widths[i].Count)).Append(rows[i].Count);
                lines.Add(text.ToString());
            }

            return lines;
        }

        /// <summary>行の頭からの位置（TextMeshPro の pos。単位なしは文字の部品の座標の単位）。</summary>
        public static string Pos(float x) => "<pos=" + Math.Round(x).ToString("0", CultureInfo.InvariantCulture) + ">";
    }
}
