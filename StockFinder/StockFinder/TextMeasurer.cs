using System;
using System.Reflection;
using HarmonyLib;
using LazyBearTechnology;
using TMPro;
using UnityEngine;

namespace StockFinder
{
    /// <summary>
    /// 表の列の位置を決めるために、ツールチップの文字と同じ書体・大きさで文字の幅を測る。
    /// 測る部品は自前の TextMeshProUGUI（Canvas の外に置くので描かれない）で、大きさ・字間はツールチップの文字の部品
    /// （UITooltipTextWidget の label。MainScene に常駐）から写す。書体は足す文字と同じ TextStyle を当てる
    /// （TextStyle.ApplyStyle は書体・材質・色・行間だけを替え、大きさは部品の値のまま）。
    /// 足す文字は全部 TextStyle.ApplyStyleToString で書体の書式を付けているので、測るのは書式ごと。
    /// </summary>
    internal static class TextMeasurer
    {
        private const float Unbounded = 100000f;

        private static TextMeshProUGUI label;
        private static bool failed;
        private static string styledLang;
        private static TextStyle styledWith;
        private static FieldInfo widgetLabelField;

        /// <summary>測る関数。用意できなければ false（そのときは列にそろえず Lines の形で出す）。</summary>
        public static bool TryGet(TextStyle baseStyle, out Func<string, float> measure)
        {
            measure = null;
            if (!Ensure(baseStyle))
            {
                return false;
            }

            measure = Measure;
            return true;
        }

        public static float Measure(string text) => label == null || string.IsNullOrEmpty(text) ? 0f : label.GetPreferredValues(text, Unbounded, Unbounded).x;

        /// <summary>ツールチップの文字の部品（UITooltipTextWidget の label）。見つからなければ null。</summary>
        public static TMP_Text WidgetLabel(UITooltipTextWidget widget)
        {
            if (widget == null)
            {
                return null;
            }

            if (widgetLabelField == null)
            {
                widgetLabelField = AccessTools.Field(typeof(UITooltipTextWidget), "label");
            }

            return widgetLabelField?.GetValue(widget) as TMP_Text;
        }

        private static bool Ensure(TextStyle baseStyle)
        {
            if (failed)
            {
                return false;
            }

            try
            {
                if (label == null)
                {
                    Create();
                }

                string lang = Strings.CurrentLang();
                if (baseStyle != null && (styledWith != baseStyle || styledLang != lang))
                {
                    baseStyle.ApplyStyle(label);
                    styledWith = baseStyle;
                    styledLang = lang;
                }

                return true;
            }
            catch (Exception e)
            {
                failed = true;
                Plugin.Log.LogError("文字の幅を測る部品を作れなかった。表は列にそろえず、行ごとに中央揃えで出す: " + e);
                return false;
            }
        }

        private static void Create()
        {
            TMP_Text template = FindTemplate();
            var go = new GameObject("StockFinder.TextMeasurer", typeof(RectTransform));
            go.transform.SetParent(Plugin.Instance.transform, false);
            label = go.AddComponent<TextMeshProUGUI>();
            label.raycastTarget = false;
            label.richText = true;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.margin = Vector4.zero;
            if (template != null)
            {
                label.fontSize = template.fontSize;
                label.characterSpacing = template.characterSpacing;
                label.wordSpacing = template.wordSpacing;
                label.fontStyle = template.fontStyle;
                Plugin.Log.LogInfo($"文字の幅を測る部品を作った（ツールチップの文字の部品から写した: 大きさ {template.fontSize}・字間 {template.characterSpacing}）");
            }
            else
            {
                label.fontSize = 16f;
                Plugin.Log.LogWarning("ツールチップの文字の部品が見つからないので、文字の大きさを 16 として測る（列がずれるかもしれない）");
            }
        }

        private static TMP_Text FindTemplate()
        {
            foreach (UITooltipTextWidget widget in Resources.FindObjectsOfTypeAll<UITooltipTextWidget>())
            {
                TMP_Text text = WidgetLabel(widget);
                if (text != null)
                {
                    return text;
                }
            }

            return null;
        }
    }
}
