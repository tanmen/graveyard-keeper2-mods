using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine.UI;

namespace StockFinder
{
    /// <summary>
    /// ゲームのツールチップ（UITooltip）の private のもの: 窓（static の instance）と、窓が持つ文字の形（TextStyle）。
    /// 1 つでも引けなければ、ツールチップには何も足さない（ゲームのツールチップはそのまま出る）。
    /// </summary>
    internal static class TooltipStyles
    {
        /// <summary>小さい文（946E44）。足す文字の部品の地の形。</summary>
        public const string Small = "smallDescriptionTextStyle";

        /// <summary>小さい文の濃い方（784F26）。ゲームの「作成場所: 」の見出しと同じ。場所の名前と数に使う。</summary>
        public const string Selected = "selectedSmallDescriptionTextStyle";

        /// <summary>太字（4E3012）。ゲームのレシピの見出しの要るレベルと同じ。合計の数に使う。</summary>
        public const string Bold = "headerBoldTextStyle";

        private static readonly string[] Names = { Small, Selected, Bold };
        private static readonly Dictionary<string, FieldInfo> Fields = new Dictionary<string, FieldInfo>();
        private static FieldInfo instanceField;
        private static bool resolved;
        private static bool usable;
        private static bool loggedWidth;

        public static UITooltip Instance() => Resolve() ? instanceField.GetValue(null) as UITooltip : null;

        /// <summary>
        /// 中身の部品が使える幅の上限。窓（UITooltip と同じ物）の OverrideLayoutElement が推奨幅を maxWidth（200）で抑え、
        /// 縦並びの左右の余白（14 ずつ）を引いた残り（172）を超える文字の部品は折り返す。上限が無ければ +∞。
        /// </summary>
        public static float ContentWidth(UITooltip tooltip)
        {
            OverrideLayoutElement limit = tooltip.GetComponent<OverrideLayoutElement>();
            LayoutGroup group = tooltip.GetComponent<LayoutGroup>();
            float width = limit != null && limit.useMaxWidth && limit.maxWidth > 0f
                ? limit.maxWidth - (group != null ? group.padding.horizontal : 0)
                : float.PositiveInfinity;

            if (!loggedWidth)
            {
                loggedWidth = true;
                Plugin.Log.LogInfo(float.IsPositiveInfinity(width)
                    ? "ツールチップの窓に幅の上限が無い"
                    : $"ツールチップの窓の幅の上限 {limit.maxWidth:0.#}・中身 {width:0.#}（これを超える表は折り返すので警告を出す）");
            }

            return width;
        }

        public static TextStyle Style(UITooltip tooltip, string name)
        {
            TextStyle style = Fields.TryGetValue(name, out FieldInfo field) ? field.GetValue(tooltip) as TextStyle : null;
            if (style == null)
            {
                throw new InvalidOperationException("ゲームのツールチップの文字の形 " + name + " が空");
            }

            return style;
        }

        private static bool Resolve()
        {
            if (resolved)
            {
                return usable;
            }

            resolved = true;
            var missing = new List<string>();
            try
            {
                Type type = typeof(UITooltip);
                instanceField = AccessTools.Field(type, "instance");
                if (instanceField == null || !instanceField.IsStatic)
                {
                    missing.Add("instance");
                }

                foreach (string name in Names)
                {
                    FieldInfo field = AccessTools.Field(type, name);
                    if (field == null || field.FieldType != typeof(TextStyle))
                    {
                        missing.Add(name);
                    }
                    else
                    {
                        Fields[name] = field;
                    }
                }
            }
            catch (Exception e)
            {
                missing.Add(e.GetType().Name + ": " + e.Message);
            }

            usable = missing.Count == 0;
            if (usable)
            {
                Plugin.Log.LogInfo($"ゲームのツールチップの部品を引いた（文字の形 {Fields.Count}）");
            }
            else
            {
                Plugin.Log.LogWarning("ゲームのツールチップの部品が引けないので、ツールチップに在庫を足さない: " + string.Join(", ", missing));
            }

            return usable;
        }
    }
}
