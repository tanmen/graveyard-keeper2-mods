using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CraftTodo
{
    /// <summary>
    /// 製作メモの板の見た目を、ゲームの建物の窓（UIBuildingWindow）から写す。建物の窓は
    /// 「角飾りの枠（Content/Frame。Sliced）」の上に「石板の地（Frame/BackMask/Back。636x406 の絵を引き伸ばさずマスクで切り抜く）」
    /// を重ねている。ゲームの UI はドット絵なので、地は等倍のまま（Tiled で敷いて）マスクで切り、枠だけを Sliced で伸ばす。
    /// 建物の窓は MainScene に常駐している（閉じていても在る）ので、sprite を名前で探さずに済む。
    /// 取れなかったときは単色の半透明の板（0.2.0 までの見た目）に戻す。調べた根拠は docs/game-api-notes.md。
    ///
    /// 地の絵（comm-frame_bg_2、636x406）は上下の端ほど明るく、上 3 行・下 1 行は模様の無いベタ（ゲームの窓では枠の下に隠れる）。
    /// 0.4.1 は Tiled で敷き詰めていたので、406 を超えると継ぎ目にベタの帯が出た。406 を超えるときは、ベタの行を削った絵を
    /// 上下反転しながら継ぐ（継ぎ目の上下が同じ行になるので模様が切れない）。
    /// </summary>
    internal sealed class PanelSkin
    {
        private static readonly Color PlainColor = new Color(0.07f, 0.06f, 0.09f, 0.78f);

        // 地の絵の上下のベタの行（2026-11-21 に comm-frame_bg_2 の画素で数えた。1 行が 1 色だけの行）
        private const int BackFlatTop = 3;
        private const int BackFlatBottom = 1;

        public Sprite Frame { get; private set; }

        public Image.Type FrameType { get; private set; } = Image.Type.Sliced;

        public float FramePixelsPerUnitMultiplier { get; private set; } = 1f;

        public Color FrameColor { get; private set; } = Color.white;

        public Sprite Back { get; private set; }

        /// <summary>地の絵の上下のベタの行を削ったもの（406 を超えて継ぐとき用）。作れなければ Back のまま。</summary>
        public Sprite BackInner { get; private set; }

        public Color BackColor { get; private set; } = Color.white;

        /// <summary>地を切り抜くマスクの、板の縁からの内側へのずらし（左・下・右・上。ゲームの窓は 13）。</summary>
        public Vector4 BackInset { get; private set; } = new Vector4(13f, 13f, 13f, 13f);

        public bool FromGame => Frame != null;

        public string Description { get; private set; } = "単色の板";

        public static PanelSkin Plain() => new PanelSkin();

        public static PanelSkin Resolve()
        {
            var skin = new PanelSkin();
            try
            {
                foreach (UIBuildingWindow window in Resources.FindObjectsOfTypeAll<UIBuildingWindow>())
                {
                    if (window == null)
                    {
                        continue;
                    }

                    Transform frameT = window.transform.Find("Content/Frame") ?? FindDeep(window.transform, "Frame");
                    Image frame = frameT == null ? null : frameT.GetComponent<Image>();
                    if (frame == null || frame.sprite == null)
                    {
                        continue;
                    }

                    skin.Frame = frame.sprite;
                    skin.FrameType = frame.type;
                    skin.FramePixelsPerUnitMultiplier = frame.pixelsPerUnitMultiplier;
                    skin.FrameColor = frame.color;

                    Transform backT = frameT.Find("BackMask/Back") ?? FindDeep(frameT, "Back");
                    Image back = backT == null ? null : backT.GetComponent<Image>();
                    if (back != null && back.sprite != null)
                    {
                        skin.Back = back.sprite;
                        skin.BackInner = CropRows(back.sprite, BackFlatTop, BackFlatBottom);
                        skin.BackColor = back.color;
                        if (backT.parent is RectTransform mask && mask != frameT)
                        {
                            skin.BackInset = new Vector4(mask.offsetMin.x, mask.offsetMin.y, -mask.offsetMax.x, -mask.offsetMax.y);
                        }
                    }

                    skin.Description = $"建物の窓（{UiDiagnostics.PathOf(frameT, window.transform)}）の枠 {Name(skin.Frame)}（{skin.FrameType}、縁 {skin.Frame.border:0}）"
                        + (skin.Back != null
                            ? $"と地 {Name(skin.Back)}（{skin.Back.rect.width:0}x{skin.Back.rect.height:0} を切り抜き、内側へ {skin.BackInset:0}。"
                              + (skin.BackInner != skin.Back ? $"超えたら {skin.BackInner.rect.height:0} 行を反転して継ぐ）" : "超えたら削らずに反転して継ぐ）")
                            : "（地は見つからない）");
                    break;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"建物の窓の枠を引けなかった（単色の板にする）: {e.Message}");
                skin = new PanelSkin();
            }

            return skin;
        }

        /// <summary>板の Image（枠）と、地を切り抜くマスクに当てる。地の絵そのものは <see cref="LayBack"/> で並べる。</summary>
        public void Apply(Image panelImage, RectTransform backMask)
        {
            if (!FromGame)
            {
                panelImage.sprite = null;
                panelImage.type = Image.Type.Simple;
                panelImage.color = PlainColor;
                backMask.gameObject.SetActive(false);
                return;
            }

            panelImage.sprite = Frame;
            panelImage.type = FrameType;
            panelImage.pixelsPerUnitMultiplier = FramePixelsPerUnitMultiplier;
            panelImage.fillCenter = true;
            panelImage.color = FrameColor;

            if (Back == null)
            {
                backMask.gameObject.SetActive(false);
                return;
            }

            backMask.gameObject.SetActive(true);
            backMask.offsetMin = new Vector2(BackInset.x, BackInset.y);
            backMask.offsetMax = new Vector2(-BackInset.z, -BackInset.w);
        }

        /// <summary>
        /// 地の絵を、板の高さ <paramref name="boardHeight"/> に合わせてマスクの下に並べる（どれも等倍で、左下を揃える）。
        /// 絵の高さに収まれば 1 枚（0.4.1 の Tiled と同じ見え方）。超えたら、ベタの行を削った絵を下から上へ、1 枚おきに上下反転して積む。
        /// 部品は <paramref name="tiles"/> に溜めて使い回す（並べ直すたびに作り直さない）。
        /// </summary>
        public void LayBack(RectTransform backMask, List<Image> tiles, float boardHeight, float referencePixelsPerUnit)
        {
            if (!FromGame || Back == null)
            {
                return;
            }

            float height = boardHeight - BackInset.y - BackInset.w;
            float fullHeight = UiFactory.NativeSize(Back, referencePixelsPerUnit).y;
            Sprite sprite = height <= fullHeight || BackInner == null ? Back : BackInner;
            Vector2 size = UiFactory.NativeSize(sprite, referencePixelsPerUnit);
            int count = size.y <= 0f ? 0 : Mathf.Max(1, Mathf.CeilToInt(height / size.y));

            for (int i = 0; i < count; i++)
            {
                if (i == tiles.Count)
                {
                    Image image = UiFactory.CreateImage("Back", backMask, null, Color.clear);
                    tiles.Add(image);
                }

                Image tile = tiles[i];
                tile.gameObject.SetActive(true);
                tile.sprite = sprite;
                tile.type = Image.Type.Simple;
                tile.pixelsPerUnitMultiplier = 1f;
                tile.color = BackColor;
                RectTransform rect = tile.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = Vector2.zero;
                rect.sizeDelta = size;
                // 奇数枚目は上下反転（下端を軸に裏返るので、上端の位置に置く）
                bool flip = i % 2 == 1;
                rect.localScale = flip ? new Vector3(1f, -1f, 1f) : Vector3.one;
                rect.anchoredPosition = new Vector2(0f, size.y * (flip ? i + 1 : i));
            }

            for (int i = count; i < tiles.Count; i++)
            {
                tiles[i].gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// sprite の上下の行を削った sprite を作る（同じテクスチャの中の範囲を指すだけ）。アトラスに詰められていて範囲が引けないときや、
        /// 絵の端が切り詰められているとき（textureRect と rect の大きさが違う）は、削らずに元の sprite を返す。
        /// </summary>
        private static Sprite CropRows(Sprite sprite, int top, int bottom)
        {
            try
            {
                if (sprite.texture == null || (sprite.packed && sprite.packingRotation != SpritePackingRotation.None))
                {
                    return sprite;
                }

                Rect area = sprite.textureRect;
                if (Mathf.Abs(area.width - sprite.rect.width) > 0.5f || Mathf.Abs(area.height - sprite.rect.height) > 0.5f
                    || area.height <= top + bottom + 1)
                {
                    return sprite;
                }

                // テクスチャの y は下から上へ（下の行を削る = y を上げる）
                var inner = new Rect(area.x, area.y + bottom, area.width, area.height - top - bottom);
                Sprite cropped = Sprite.Create(sprite.texture, inner, new Vector2(0.5f, 0.5f), sprite.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                cropped.name = sprite.name + "-inner";
                return cropped;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"地の絵 {sprite.name} の端を削れなかった（削らずに継ぐ）: {e.Message}");
                return sprite;
            }
        }

        private static string Name(Sprite sprite) => sprite == null ? "-" : sprite.name;

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name && child != root)
                {
                    return child;
                }
            }

            return null;
        }
    }
}
