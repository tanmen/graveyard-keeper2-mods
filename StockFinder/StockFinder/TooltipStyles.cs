using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using LazyBearTechnology;

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

        public static UITooltip Instance() => Resolve() ? instanceField.GetValue(null) as UITooltip : null;

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
