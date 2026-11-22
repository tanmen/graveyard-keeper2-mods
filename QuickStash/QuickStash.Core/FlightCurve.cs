using System;

namespace QuickStash.Core
{
    /// <summary>
    /// しまった品のアイコンが、自分の体から宝箱へ飛ぶ動き。t は 0（出だし）〜1（宝箱に入る）。
    /// 体と宝箱を結ぶ線の上を、ゆっくり出て速く入るように進み（smoothstep）、上へ弧を描く。出だしで小さい所から膨らみ、宝箱の手前で縮みながら消える。
    /// 距離と高さの単位は UI の単位（ゲームの UI は 1 単位 = 絵の 1 画素）。
    /// </summary>
    public static class FlightCurve
    {
        /// <summary>1 つのアイコンが飛ぶ時間（秒）。</summary>
        public const float Duration = 0.55f;

        /// <summary>続けて飛ばすアイコンの間（秒）。</summary>
        public const float Stagger = 0.07f;

        /// <summary>1 回に飛ばすアイコンの上限（多いときは先頭から）。</summary>
        public const int MaxFlights = 16;

        private const float MinArc = 16f;
        private const float MaxArc = 72f;
        private const float ArcPerDistance = 0.35f;
        private const float GrowUntil = 0.12f;
        private const float ShrinkFrom = 0.72f;
        private const float FadeFrom = 0.85f;
        private const float SmallScale = 0.5f;

        /// <summary>i 番目（0 から）のアイコンが飛び始めるまでの時間（秒）。</summary>
        public static float Delay(int index) => Math.Max(0, index) * Stagger;

        /// <summary>線の上をどこまで進んだか（0〜1）。</summary>
        public static float Progress(float t)
        {
            t = Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>弧の高さ（体と宝箱の画面の上の距離から。近くても少しは跳ね、遠くても高くなりすぎない）。</summary>
        public static float ArcHeight(float distance) => Math.Min(MaxArc, Math.Max(MinArc, distance * ArcPerDistance));

        /// <summary>t での上への持ち上げ（0〜arcHeight。真ん中で一番高い）。</summary>
        public static float Lift(float t, float arcHeight)
        {
            t = Clamp01(t);
            return 4f * t * (1f - t) * arcHeight;
        }

        /// <summary>t での大きさ（1 が等倍）。</summary>
        public static float Scale(float t)
        {
            t = Clamp01(t);
            if (t < GrowUntil)
            {
                return Lerp(SmallScale, 1f, t / GrowUntil);
            }

            if (t > ShrinkFrom)
            {
                return Lerp(1f, SmallScale, (t - ShrinkFrom) / (1f - ShrinkFrom));
            }

            return 1f;
        }

        /// <summary>t での不透明度（1 が不透明）。</summary>
        public static float Alpha(float t)
        {
            t = Clamp01(t);
            return t > FadeFrom ? Lerp(1f, 0f, (t - FadeFrom) / (1f - FadeFrom)) : 1f;
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);

        private static float Clamp01(float t) => t < 0f ? 0f : t > 1f ? 1f : t;
    }
}
