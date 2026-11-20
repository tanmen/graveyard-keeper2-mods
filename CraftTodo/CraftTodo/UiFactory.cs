using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CraftTodo
{
    /// <summary>
    /// uGUI の部品をコードで組む小道具（ゲームの UISteamWorkshopCreatorWindow と同じ組み方）。
    /// 制作メニューは並べ方を自分で計算して置く（ドット絵の部品を整数の位置に置くため。LayoutGroup の中央揃えは半端な位置になる）。
    /// 位置はどれも親の左上から右へ x・下へ y（UI の単位 = 絵の 1 画素）。
    /// </summary>
    internal static class UiFactory
    {
        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>親の左上から (x, y) に w x h で置く。mirror なら左右を反転して同じ場所に置く。</summary>
        public static void Place(RectTransform rect, float x, float y, float w, float h, bool mirror = false)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(w, h);
            // 左右反転は左上を軸に裏返るので、右端の位置に置く
            rect.localScale = mirror ? new Vector3(-1f, 1f, 1f) : Vector3.one;
            rect.anchoredPosition = new Vector2(mirror ? x + w : x, -y);
        }

        /// <summary>親いっぱいに広げ、4 辺を内側へ寄せる（左・下・右・上）。</summary>
        public static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>
        /// 絵の Image。縁（border）のある絵は Sliced（縁は等倍のまま、中だけ伸ばす）、無い絵は Simple。
        /// 絵が無ければ <paramref name="plainColor"/> の単色にする。
        /// </summary>
        public static Image CreateImage(string name, Transform parent, Sprite sprite, Color plainColor)
        {
            RectTransform rect = CreateRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            SetSprite(image, sprite, plainColor);
            return image;
        }

        public static void SetSprite(Image image, Sprite sprite, Color plainColor)
        {
            image.sprite = sprite;
            image.pixelsPerUnitMultiplier = 1f;
            if (sprite == null)
            {
                image.type = Image.Type.Simple;
                image.color = plainColor;
                return;
            }

            image.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.fillCenter = true;
            image.color = Color.white;
        }

        /// <summary>絵の画素数どおりの大きさ（UI の単位）。Image.SetNativeSize と同じ考え方。</summary>
        public static Vector2 NativeSize(Sprite sprite, float referencePixelsPerUnit)
        {
            if (sprite == null || sprite.pixelsPerUnit <= 0f)
            {
                return Vector2.zero;
            }

            float scale = referencePixelsPerUnit / sprite.pixelsPerUnit;
            return new Vector2(Mathf.Round(sprite.rect.width * scale), Mathf.Round(sprite.rect.height * scale));
        }

        public static TextMeshProUGUI CreateLabel(Transform parent, string name, LabelStyler styler, TextRole role, TextAlignmentOptions alignment)
        {
            RectTransform rect = CreateRect(name, parent);
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            styler.Apply(label, role);
            label.alignment = alignment;
            return label;
        }

        /// <summary>文字の幅（UI の単位。切り上げ）。書体が当たってから呼ぶ。</summary>
        public static int TextWidth(TMP_Text label, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            return Mathf.CeilToInt(label.GetPreferredValues(text).x);
        }
    }
}
