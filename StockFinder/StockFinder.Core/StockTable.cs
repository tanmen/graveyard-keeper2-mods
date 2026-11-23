using System;
using System.Collections.Generic;
using System.Globalization;
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

        /// <summary>場所の名前（所持品・「ほか N か所」の行はここだけに書く）。</summary>
        public string Place { get; }

        /// <summary>宝箱の名前（無ければ空）。</summary>
        public string Container { get; }

        /// <summary>数（右揃え）。</summary>
        public string Count { get; }

        public bool HasContainer => Container.Length > 0;
    }

    /// <summary>
    /// 表の行ごとの文字を作る。宝箱のある行は、続けて並ぶ同じ場所をまとめて「場所の名前」だけの見出しの行にし、その下に
    /// 宝箱ごとの行（字下げした宝箱の名前と、右揃えの数）を並べる。所持品・「ほか」の行は名前と数の 1 行。
    /// 0.2.1 までは「場所  宝箱 … 数」を 1 行に並べていたが、場所と宝箱の名前が両方長いとツールチップの窓の幅の上限
    /// （中身 172）を超えて折り返して崩れた（2026-11-23、水）。0.2.1 は収まらないときだけこの形にし、0.2.2 からは本人の選択でいつもこの形。
    /// <para>
    /// ゲームのツールチップの文字の部品は中央揃えしかせず（揃え方は TextStyle が戻す）、部品ごとに自分の幅で中央に置かれるので、
    /// 列は TextMeshPro の <c>&lt;pos=…&gt;</c>（行の頭からの位置）で組み、どの行も幅を「数の右端」にそろえる
    /// （1 行ずつ別の部品にして中央に置かれても、行の頭がそろうように）。数は「右端−その数の幅」に置く。
    /// 見出しの行には数が無いので、右端に見えない詰め物（数と同じ形の字を透明にしたもの）を置いて幅を取る
    /// （TextMeshPro の推奨幅は、空白でない字の右端までしか数えない）。
    /// 1 つの部品に改行で詰めないのは、ゲームの小さい書体の行送りが字の高さより狭く、行が重なるため（2026-11-22 に画面で見た）。
    /// 位置は、ゲームと同じ書体・大きさで測った幅（measure）から決め、整数に丸める（ドット絵の書体がにじまないように）。
    /// </para>
    /// </summary>
    public static class StockTable
    {
        /// <param name="indent">宝箱の行の字下げ。</param>
        /// <param name="countGap">名前の右端の最大と、数の列の間。</param>
        /// <param name="pad">見出しの行の右端に置く見えない詰め物（書式入り）。</param>
        public static List<string> Compose(IReadOnlyList<StockTableRow> rows, Func<string, float> measure, float indent, float countGap, string pad)
        {
            var lines = new List<string>();
            if (rows == null || rows.Count == 0)
            {
                return lines;
            }

            // 見出しの行は Count が null
            var entries = new List<(float X, string Left, string Count)>();
            for (int i = 0; i < rows.Count; i++)
            {
                StockTableRow row = rows[i];
                if (!row.HasContainer)
                {
                    entries.Add((0f, row.Place, row.Count));
                    continue;
                }

                if (i == 0 || !rows[i - 1].HasContainer || rows[i - 1].Place != row.Place)
                {
                    entries.Add((0f, row.Place, null));
                }

                entries.Add(((float)Math.Ceiling(indent), row.Container, row.Count));
            }

            float padWidth = measure(pad);
            float leftEnd = 0f;
            float headerEnd = 0f;
            float countWidth = 0f;
            foreach ((float x, string left, string count) in entries)
            {
                if (count == null)
                {
                    headerEnd = Math.Max(headerEnd, measure(left));
                }
                else
                {
                    leftEnd = Math.Max(leftEnd, x + measure(left));
                    countWidth = Math.Max(countWidth, measure(count));
                }
            }

            // 数の右端。見出しが一番長ければ、見出しの右端（＋詰め物）まで広げる
            float right = Math.Max((float)Math.Ceiling(leftEnd + countGap) + countWidth, (float)Math.Ceiling(headerEnd) + padWidth);

            foreach ((float x, string left, string count) in entries)
            {
                var text = new StringBuilder();
                if (x > 0f)
                {
                    text.Append(Pos(x));
                }

                text.Append(left);
                if (count == null)
                {
                    text.Append(Pos(right - padWidth)).Append(pad);
                }
                else
                {
                    text.Append(Pos(right - measure(count))).Append(count);
                }

                lines.Add(text.ToString());
            }

            return lines;
        }

        /// <summary>行の頭からの位置（TextMeshPro の pos。単位なしは文字の部品の座標の単位）。</summary>
        public static string Pos(float x) => "<pos=" + Math.Round(x).ToString("0", CultureInfo.InvariantCulture) + ">";
    }
}
