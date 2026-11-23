using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using LazyBearTechnology;

namespace TradeFinder
{
    /// <summary>
    /// ゲームのツールチップ（UITooltip）の private のもの: 窓（static の instance）と、窓が持つ文字の形（TextStyle）。
    /// 1 つでも引けなければ、ツールチップには何も足さない（ゲームのツールチップはそのまま出る）。
    /// </summary>
    internal static class TooltipStyles
    {
        /// <summary>小さい文（946E44）。足す文字の部品の地の形と、商人の名前・値段。</summary>
        public const string Small = "smallDescriptionTextStyle";

        /// <summary>小さい文の濃い方（784F26）。ゲームの「作成場所: 」の見出しと同じ。「買える: 」「売れる: 」に使う。</summary>
        public const string Selected = "selectedSmallDescriptionTextStyle";

        private static readonly string[] Names = { Small, Selected };
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
                Plugin.Log.LogWarning("ゲームのツールチップの部品が引けないので、ツールチップに売り買いの場所を足さない: " + string.Join(", ", missing));
            }

            return usable;
        }
    }
}
