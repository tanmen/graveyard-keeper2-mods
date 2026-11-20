using System;
using HarmonyLib;
using LazyBearTechnology;
using TMPro;
using UnityEngine;

namespace CraftTodo
{
    /// <summary>文字の役目。どのゲームの文字の形を写すかが変わる。</summary>
    internal enum TextRole
    {
        /// <summary>見出し（帯の「制作メニュー」・1 件ごとの見出し板・記号）。所持品の窓の見出し板の文字。</summary>
        Header,

        /// <summary>セルの右下の数。所持品のセルの数の文字。</summary>
        Count,
    }

    /// <summary>
    /// 制作メニューの文字を、ゲームの所持品の窓と同じ書体・色・大きさにする。
    /// 書体は言語ごとに違う（日本語は別の書体）ので、書体そのものではなくゲームの TextStyle を当てる
    /// （TextStyle.ApplyStyle がいまの言語の書体と材質を選ぶ）。
    /// 所持品の窓の文字が引けなければ建物の窓の行の文字、それも無ければ HUD の文字から書体を写す。
    /// </summary>
    internal sealed class LabelStyler
    {
        private TextStyle headerStyle;
        private TextStyle countStyle;
        private TMP_Text source;

        public float HeaderSize { get; private set; }

        public float CountSize { get; private set; }

        public bool UsesGameStyles => headerStyle != null && countStyle != null;

        public string Description { get; private set; } = string.Empty;

        public static LabelStyler Resolve(HUD hud, MenuParts parts)
        {
            var styler = new LabelStyler();
            string headerFrom = "-";
            string countFrom = "-";
            try
            {
                if (parts?.HeaderSource != null)
                {
                    Traverse t = Traverse.Create(parts.HeaderSource);
                    styler.headerStyle = t.Field("headerActiveStyle").GetValue<TextStyle>();
                    TextMeshProUGUI label = t.Field("header").GetValue<TextMeshProUGUI>();
                    if (styler.headerStyle != null)
                    {
                        headerFrom = "所持品の見出し";
                        styler.HeaderSize = label != null ? label.fontSize : 0f;
                    }
                }

                if (parts?.CellSource != null)
                {
                    Traverse t = Traverse.Create(parts.CellSource);
                    styler.countStyle = t.Field("countLabelNormal").GetValue<TextStyle>();
                    TextMeshProUGUI label = t.Field("countLabel").GetValue<TextMeshProUGUI>();
                    if (styler.countStyle != null)
                    {
                        countFrom = "所持品のセルの数";
                        styler.CountSize = label != null ? label.fontSize : 0f;
                    }
                }

                if (styler.headerStyle == null || styler.countStyle == null)
                {
                    // 所持品の窓が引けなければ、建物の窓の行の文字（0.3.0 までの写し元）で代える
                    foreach (UIBuildingWidget widget in Resources.FindObjectsOfTypeAll<UIBuildingWidget>())
                    {
                        Traverse t = Traverse.Create(widget);
                        TextStyle title = t.Field("canBuildStyle").GetValue<TextStyle>();
                        if (title == null)
                        {
                            continue;
                        }

                        TextMeshProUGUI name = t.Field("nameLabel").GetValue<TextMeshProUGUI>();
                        if (styler.headerStyle == null)
                        {
                            styler.headerStyle = title;
                            styler.HeaderSize = name != null ? name.fontSize : 0f;
                            headerFrom = "建物の行の名前";
                        }

                        if (styler.countStyle == null)
                        {
                            styler.countStyle = t.Field("descriptionDefaultStyle").GetValue<TextStyle>() ?? title;
                            TextMeshProUGUI description = t.Field("descriptionLabel").GetValue<TextMeshProUGUI>();
                            styler.CountSize = description != null ? description.fontSize : styler.HeaderSize;
                            countFrom = "建物の行の説明";
                        }

                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"ゲームの文字の形を引けなかった（HUD の文字で代える）: {e.Message}");
            }

            if (styler.headerStyle == null || styler.countStyle == null)
            {
                styler.source = FindSourceLabel(hud);
            }

            if (styler.HeaderSize <= 0f)
            {
                styler.HeaderSize = styler.source != null ? styler.source.fontSize : 16f;
            }

            if (styler.CountSize <= 0f)
            {
                styler.CountSize = styler.HeaderSize;
            }

            styler.Description = $"見出し {headerFrom}（{styler.HeaderSize:0}）・数 {countFrom}（{styler.CountSize:0}）";
            return styler;
        }

        public void Apply(TMP_Text label, TextRole role)
        {
            TextStyle style = role == TextRole.Header ? headerStyle : countStyle;
            if (style != null)
            {
                style.ApplyStyle(label);
            }
            else if (source != null)
            {
                label.font = source.font;
                label.fontSharedMaterial = source.fontSharedMaterial;
                label.color = source.color;
            }

            label.fontSize = role == TextRole.Header ? HeaderSize : CountSize;
            label.enableAutoSizing = false;
#pragma warning disable CS0618 // TMP の版によっては textWrappingMode に置き換わっている
            label.enableWordWrapping = false;
#pragma warning restore CS0618
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
        }

        private static TMP_Text FindSourceLabel(HUD hud)
        {
            try
            {
                if (hud != null)
                {
                    TextMeshProUGUI happiness = hud.GetHudLabel("happiness");
                    if (happiness != null)
                    {
                        return happiness;
                    }
                }

                foreach (TextMeshProUGUI label in UnityEngine.Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None))
                {
                    if (label.font != null && label.GetComponent<StaticFontLabel>() == null)
                    {
                        return label;
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"写す元の文字が見つからない: {e.Message}");
            }

            return null;
        }
    }
}
