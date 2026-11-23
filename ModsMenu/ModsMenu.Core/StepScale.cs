using System;
using System.Globalization;

namespace ModsMenu.Core
{
    /// <summary>
    /// 数の行の刻み。ゲームの音量の行（UISlider）は − ＋ で中のつまみを 1 ずつ動かす作りなので、つまみは 0〜<see cref="Steps"/> の番号で持ち、
    /// 値は min ＋ 番号 × step にする。範囲の外の値（設定ファイルを手で書いたなど）が入っていても、つまみはその値で止めずに端に寄せるだけで、値は書き換えない。
    /// </summary>
    internal readonly struct StepScale
    {
        public StepScale(double min, double max, double step)
        {
            if (double.IsNaN(min) || double.IsNaN(max) || double.IsNaN(step))
            {
                throw new ArgumentException("範囲と刻みに NaN は使えない");
            }

            if (max < min)
            {
                (min, max) = (max, min);
            }

            Min = min;
            Max = max;
            Step = step > 0 ? step : 1;
        }

        public double Min { get; }

        public double Max { get; }

        public double Step { get; }

        /// <summary>つまみの番号の最大（0 からこの値まで）。</summary>
        public int Steps => (int)Math.Round((Max - Min) / Step);

        /// <summary>値に近いつまみの番号（範囲の外なら端）。</summary>
        public int IndexOf(double value)
        {
            if (double.IsNaN(value))
            {
                return 0;
            }

            int index = (int)Math.Round((value - Min) / Step);
            return index < 0 ? 0 : (index > Steps ? Steps : index);
        }

        /// <summary>つまみの番号の値。刻みの誤差（0.1 × 3 = 0.30000000000000004）は刻みの桁で丸める。</summary>
        public double ValueAt(int index)
        {
            index = index < 0 ? 0 : (index > Steps ? Steps : index);
            double value = Min + index * Step;
            return Math.Round(value, Decimals);
        }

        /// <summary>刻みの小数の桁（1 → 0、0.5 → 1、0.05 → 2）。</summary>
        public int Decimals
        {
            get
            {
                for (int d = 0; d < 6; d++)
                {
                    double scaled = Step * Math.Pow(10, d);
                    if (Math.Abs(scaled - Math.Round(scaled)) < 1e-9)
                    {
                        return d;
                    }
                }

                return 6;
            }
        }

        /// <summary>欄に出す文字（刻みの桁まで。ロケールに依らず小数点は「.」）。</summary>
        public string Format(double value) => value.ToString("F" + Decimals, CultureInfo.InvariantCulture);
    }
}
