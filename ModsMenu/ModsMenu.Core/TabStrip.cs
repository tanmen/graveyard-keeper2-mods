using System;
using System.Collections.Generic;

namespace ModsMenu.Core
{
    /// <summary>帯に並べるタブの範囲（先頭の位置と数）と、入りきらずに矢印を出すか。</summary>
    internal readonly struct TabWindow
    {
        public TabWindow(int first, int count, bool overflow)
        {
            First = first;
            Count = count;
            Overflow = overflow;
        }

        public int First { get; }

        public int Count { get; }

        /// <summary>全部は入らない（左右の矢印で送る）。</summary>
        public bool Overflow { get; }

        public bool Contains(int index) => index >= First && index < First + Count;

        public override string ToString() => $"{First}+{Count}{(Overflow ? "（あふれ）" : string.Empty)}";
    }

    /// <summary>
    /// タブの帯の詰め方。全部入るなら全部並べる。入らなければ両端に矢印の分を空け、選んでいるタブが見える範囲だけを並べる。
    /// 範囲はなるべく動かさない（前の先頭のままで選んだタブが見えるなら、そのまま）。
    /// </summary>
    internal static class TabStrip
    {
        /// <param name="widths">タブごとの幅。</param>
        /// <param name="available">帯に使える幅。</param>
        /// <param name="gap">タブの間（隣り合わせなら 0）。</param>
        /// <param name="arrowsWidth">あふれたときに両端の矢印で使う幅（左右の合計）。</param>
        /// <param name="active">選んでいるタブ。</param>
        /// <param name="previousFirst">前に並べたときの先頭。</param>
        public static TabWindow Fit(IReadOnlyList<float> widths, float available, float gap, float arrowsWidth, int active, int previousFirst)
        {
            if (widths == null || widths.Count == 0)
            {
                return new TabWindow(0, 0, false);
            }

            int n = widths.Count;
            active = Clamp(active, 0, n - 1);
            if (Span(widths, 0, n, gap) <= available + Epsilon)
            {
                return new TabWindow(0, n, false);
            }

            float room = Math.Max(0f, available - arrowsWidth);
            int first = Clamp(previousFirst, 0, active);

            // 選んだタブが右にはみ出すなら、見えるところまで先頭を右へ送る
            while (first < active && Span(widths, first, active - first + 1, gap) > room + Epsilon)
            {
                first++;
            }

            // 右の端まで見えていて左に空きがあるなら、先頭を左へ戻して詰める（最後の範囲が 1 つだけ、にならないように）
            while (first > 0 && Span(widths, first - 1, n - first + 1, gap) <= room + Epsilon)
            {
                first--;
            }

            return new TabWindow(first, Math.Max(1, Fitting(widths, first, room, gap)), true);
        }

        /// <summary>矢印で送る。<paramref name="step"/> が負なら左、正なら右。動いたあとの先頭を返す（右の端が見えていれば右へは送らない）。</summary>
        public static int Scroll(IReadOnlyList<float> widths, float available, float gap, float arrowsWidth, int first, int step)
        {
            if (widths == null || widths.Count == 0)
            {
                return 0;
            }

            int n = widths.Count;
            first = Clamp(first, 0, n - 1);
            float room = Math.Max(0f, available - arrowsWidth);
            if (step > 0 && first + Fitting(widths, first, room, gap) >= n)
            {
                return first;
            }

            return Clamp(first + step, 0, n - 1);
        }

        /// <summary>first から並べて <paramref name="room"/> に入る数。</summary>
        public static int Fitting(IReadOnlyList<float> widths, int first, float room, float gap)
        {
            float used = 0f;
            int count = 0;
            for (int i = first; i < widths.Count; i++)
            {
                float add = widths[i] + (count > 0 ? gap : 0f);
                if (used + add > room + Epsilon)
                {
                    break;
                }

                used += add;
                count++;
            }

            return count;
        }

        /// <summary>first から count 個並べたときの幅。</summary>
        public static float Span(IReadOnlyList<float> widths, int first, int count, float gap)
        {
            float total = 0f;
            for (int i = 0; i < count && first + i < widths.Count; i++)
            {
                total += widths[first + i] + (i > 0 ? gap : 0f);
            }

            return total;
        }

        private const float Epsilon = 0.01f;

        private static int Clamp(int value, int min, int max) => value < min ? min : (value > max ? max : value);
    }
}
