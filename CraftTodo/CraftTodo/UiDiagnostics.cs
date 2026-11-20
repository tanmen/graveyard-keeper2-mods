using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace CraftTodo
{
    /// <summary>
    /// 見た目を合わせ直すときの手がかり。ゲームの UI にある枠らしい Image（Sliced・Tiled か、縁の幅を持つ sprite）を
    /// 1 回だけログに並べる（パス・sprite 名・縁・倍率・色・大きさ）。どの部品の背景を写すかを選び直すときに読む。
    /// </summary>
    internal static class UiDiagnostics
    {
        private const int MaxLines = 60;

        private static bool logged;

        public static void LogFrameCandidates(params (string label, Component root)[] roots)
        {
            if (logged)
            {
                return;
            }

            logged = true;
            var sb = new StringBuilder("ゲームの UI の枠らしい Image（見た目を合わせ直すときの手がかり）:");
            int lines = 0;
            var seen = new HashSet<int>();
            foreach ((string label, Component root) in roots)
            {
                if (root == null)
                {
                    sb.Append("\n  [").Append(label).Append("] 見つからない");
                    continue;
                }

                foreach (Image image in root.GetComponentsInChildren<Image>(true))
                {
                    if (lines >= MaxLines)
                    {
                        break;
                    }

                    if (!LooksLikeFrame(image) || !seen.Add(image.GetInstanceID()))
                    {
                        continue;
                    }

                    sb.Append("\n  [").Append(label).Append("] ").Append(Describe(image, root.transform));
                    lines++;
                }
            }

            Plugin.Log.LogInfo(sb.ToString());
        }

        public static bool LooksLikeFrame(Image image)
        {
            if (image == null || image.sprite == null)
            {
                return false;
            }

            return image.type == Image.Type.Sliced || image.type == Image.Type.Tiled || image.sprite.border != Vector4.zero;
        }

        public static string Describe(Image image, Transform relativeTo)
        {
            Sprite sprite = image.sprite;
            Rect rect = ((RectTransform)image.transform).rect;
            Color c = image.color;
            return string.Format(
                "{0}: sprite={1} tex={2} type={3} border={4} ppu={5} ppum={6} color=({7:0.00},{8:0.00},{9:0.00},{10:0.00}) size={11:0}x{12:0} active={13}",
                PathOf(image.transform, relativeTo),
                sprite == null ? "-" : sprite.name,
                sprite == null || sprite.texture == null ? "-" : sprite.texture.name,
                image.type,
                sprite == null ? "-" : sprite.border.ToString("0"),
                sprite == null ? 0f : sprite.pixelsPerUnit,
                image.pixelsPerUnitMultiplier,
                c.r, c.g, c.b, c.a,
                rect.width, rect.height,
                image.gameObject.activeInHierarchy);
        }

        public static string PathOf(Transform t, Transform relativeTo)
        {
            var parts = new List<string>();
            for (Transform cur = t; cur != null && cur != relativeTo; cur = cur.parent)
            {
                parts.Add(cur.name);
            }

            parts.Reverse();
            return parts.Count == 0 ? t.name : string.Join("/", parts.ToArray());
        }
    }
}
