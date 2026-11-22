using System;
using LazyBearTechnology;
using TMPro;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;

namespace QuickStash
{
    /// <summary>
    /// 所持品の窓のセルのうち、ロックした品のセルの左上に南京錠（ゲームの絵 icon_lock。11x13）を重ねる。
    /// セルの右上は品質の星（item_star_N。48x48 の絵の右上 14x13）、右下は数、左下は状態の印、左上は商人の窓の値段なので、
    /// 値段を出していない間は左上が空いている（MainScene の UIItemCell の部品の配置。QuickStash/docs/game-api-notes.md）。
    ///
    /// セルはゲームの部品の使い回し（UIPrefabsPooler）なので、所持品の窓の描き直し（InventoryWidget.Redraw）のたびに付け外しし、
    /// セルが片付けられるとき（UIItemCell.Flush）に外す。
    /// </summary>
    internal static class LockMarks
    {
        private const string MarkName = "QuickStashLock";
        private const string AtlasName = "IconsCompressed";
        private const string SpriteName = "icon_lock";

        // セルの Root（42x42）の左上の角から、南京錠の見える部分の左上までの距離（背景 44x44 の縁の内側）
        private const float Inset = 2f;

        private static Sprite sprite;
        private static bool spriteResolved;

        // 絵の中の南京錠の見える部分の左上（絵の左上から）。IconsCompressed の 11x13 は 0,0、Icons の 24x24 は 6,5
        private static Vector2 visibleOffset;

        public static void Refresh(InventoryWidget widget)
        {
            if (widget == null)
            {
                return;
            }

            bool own = GameBridge.IsPlayerInventory(widget.Data?.Inventory);
            var locks = Plugin.Instance?.Locks?.Current;
            foreach (UIItemCell cell in widget.Cells)
            {
                if (cell == null)
                {
                    continue;
                }

                Item item = cell.DisplayingItem;
                bool show = own && locks != null && item != null && !item.IsEmpty && locks.Contains(item.id) && !IsPriceShown(cell);
                Set(cell, show);
            }
        }

        public static void Hide(UIItemCell cell) => Set(cell, false);

        private static void Set(UIItemCell cell, bool show)
        {
            if (cell == null)
            {
                return;
            }

            Transform parent = RootOf(cell);
            Transform mark = parent.Find(MarkName);
            if (!show)
            {
                if (mark != null && mark.gameObject.activeSelf)
                {
                    mark.gameObject.SetActive(false);
                }

                return;
            }

            if (mark == null)
            {
                mark = Create(parent);
                if (mark == null)
                {
                    return;
                }
            }

            mark.SetAsLastSibling();
            if (!mark.gameObject.activeSelf)
            {
                mark.gameObject.SetActive(true);
            }
        }

        private static Transform RootOf(UIItemCell cell)
        {
            Transform root = cell.transform.Find("Root");
            return root != null ? root : cell.transform;
        }

        private static Transform Create(Transform parent)
        {
            Sprite lockSprite = LockSprite();
            if (lockSprite == null)
            {
                return null;
            }

            var go = new GameObject(MarkName, typeof(RectTransform), typeof(Image));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            Vector2 size = lockSprite.rect.size;
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(Inset - visibleOffset.x, -Inset + visibleOffset.y);

            Image image = go.GetComponent<Image>();
            image.sprite = lockSprite;
            image.raycastTarget = false;
            image.preserveAspect = true;
            return rect;
        }

        private static Sprite LockSprite()
        {
            if (spriteResolved)
            {
                return sprite;
            }

            spriteResolved = true;
            try
            {
                EasySpritesCollection collection = LazySingletonSO<EasySpritesCollection>.Instance;
                SpriteAtlas atlas = collection == null ? null : collection.EnsureSpriteAtlasWithNameLoaded(AtlasName);
                sprite = atlas == null ? null : atlas.GetSprite(SpriteName);
                if (sprite == null && collection != null)
                {
                    sprite = collection.GetSprite(SpriteName);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"南京錠の絵（{SpriteName}）を引けなかった: {e.GetBaseException().Message}");
            }

            if (sprite == null)
            {
                Plugin.Log.LogWarning($"南京錠の絵（{SpriteName}）が見つからないので、ロックの印を出さない（ロック自体は効く）");
                return null;
            }

            Vector2 size = sprite.rect.size;
            // Icons の 24x24 の絵は、南京錠（11x13）が左上から 6,5 の所にある（2026-11-22 に画素で測った）
            visibleOffset = Mathf.Approximately(size.x, 24f) && Mathf.Approximately(size.y, 24f) ? new Vector2(6f, 5f) : Vector2.zero;
            Plugin.Log.LogInfo($"南京錠の絵を引いた: {sprite.name} {size.x}x{size.y}（見える部分の左上 {visibleOffset.x},{visibleOffset.y}）");
            return sprite;
        }

        /// <summary>商人の窓などで、セルの左上に値段を出しているか（出している間は南京錠を重ねない）。</summary>
        private static bool IsPriceShown(UIItemCell cell)
        {
            try
            {
                TextMeshProUGUI price = HarmonyLib.Traverse.Create(cell).Field("priceLabel").GetValue<TextMeshProUGUI>();
                return price != null && price.gameObject.activeSelf && !string.IsNullOrEmpty(price.text);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
