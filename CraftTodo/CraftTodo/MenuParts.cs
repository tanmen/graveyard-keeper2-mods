using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;

namespace CraftTodo
{
    /// <summary>
    /// 制作メニューの部品の絵を、ゲームの所持品の窓から写す。所持品の窓は MainScene に常駐している（閉じていても在る）:
    /// 見出し板（InventoryHeaderWidget の comm-header_2 と両端の飾り comm-header_2-dec_side_1）、
    /// セルの下の茶色の地（InventoryWidget の widget_inventory-items_bg-active）、
    /// セル（UIItemCell の地 widget_items_cell-inventory_item_cell・アイコンの材質・アイコンの縁の色）。
    /// 上の木のタブ帯（main_window-header_1 と両端の飾り main_window-header_1-dec_side_3）は人物の窓（Addressables で開いたときだけ読まれる）の絵なので、
    /// 同じ絵が入っている SpriteAtlas（UIAtlas・UIAtlasCompressed。所持品の絵と同じアトラスなので読み込まれている）から名前で引く。
    /// 取れなかった部品は、その部品だけ単色で描く（帯が取れなければ帯を出さず、見出しを板の中に書く）。
    /// 調べた根拠は docs/game-api-notes.md の「UI の絵」。
    /// </summary>
    internal sealed class MenuParts
    {
        internal const string StripName = "main_window-header_1";
        internal const string StripCornerName = "main_window-header_1-dec_side_3";
        internal const string HeaderBarName = "comm-header_2";
        internal const string HeaderDecoName = "comm-header_2-dec_side_1";
        internal const string GroundName = "widget_inventory-items_bg-active";
        internal const string CellBackName = "widget_items_cell-inventory_item_cell";

        // 所持品の窓のスクロールの下端の影（UIMultiInventoryWindow の FadeBot。10x10、左右の縁 4、下へ向けて暗くなる）
        internal const string FadeName = "comm-content_fade_1";

        // 名前で引けた sprite。SpriteAtlas.GetSprite は呼ぶたびに複製を作るので、引いたものは覚えておく（引き直しても増えない。
        // 引き直すたびに全部の sprite を並べ直さずに済む）
        private static readonly Dictionary<string, Sprite> AtlasCopies = new Dictionary<string, Sprite>();

        /// <summary>インベントリのアイコンの縁の色（UIItemCellImageColors の normalColor）。セルから引けなかったときに使う。</summary>
        private static readonly Color DefaultIconOutline = new Color(0.1216f, 0.0986f, 0.0941f, 1f);

        public Sprite Strip { get; private set; }

        public Sprite StripCorner { get; private set; }

        public Sprite HeaderBar { get; private set; }

        public Sprite HeaderDeco { get; private set; }

        public Sprite Ground { get; private set; }

        public Sprite CellBack { get; private set; }

        /// <summary>中身があふれてスクロールするとき、下に続きがあることを見せる影。引けなければ影は出さない（スクロールはする）。</summary>
        public Sprite Fade { get; private set; }

        /// <summary>アイテムのアイコンの材質（純粋な青 (0,0,255) の縁を色に置き換える）。引けなければ null（青い縁のまま描く）。</summary>
        public Material IconMaterial { get; private set; }

        public Color IconOutline { get; private set; } = DefaultIconOutline;

        /// <summary>文字の形を写す元（LabelStyler が読む）。</summary>
        public Component HeaderSource { get; private set; }

        public Component CellSource { get; private set; }

        public bool HasStrip => Strip != null;

        /// <summary>主な部品（見出し板と茶色の地）がそろったか。そろわなければときどき引き直す。</summary>
        public bool Complete => HeaderBar != null && Ground != null && CellBack != null && HasStrip;

        public string Description { get; private set; } = "単色";

        public static MenuParts Plain() => new MenuParts();

        public static MenuParts Resolve()
        {
            var parts = new MenuParts();
            try
            {
                parts.ResolveFromInventory();
                parts.ResolveFromAtlases();
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"所持品の窓の絵を引けなかった（引けた部品だけ使う）: {e.Message}");
            }

            parts.Description = parts.Describe();
            return parts;
        }

        private void ResolveFromInventory()
        {
            foreach (InventoryHeaderWidget header in Resources.FindObjectsOfTypeAll<InventoryHeaderWidget>())
            {
                Sprite bar = header == null ? null : Traverse.Create(header).Field("headerBackgroundActiveSprite").GetValue<Sprite>();
                if (bar == null)
                {
                    continue;
                }

                HeaderBar = bar;
                HeaderSource = header;
                Image deco = FindImage(header.transform, "DecotLeft");
                if (deco != null && deco.sprite != null)
                {
                    HeaderDeco = deco.sprite;
                }

                break;
            }

            foreach (InventoryWidget widget in Resources.FindObjectsOfTypeAll<InventoryWidget>())
            {
                Sprite ground = widget == null ? null : Traverse.Create(widget).Field("backgroundActiveSprite").GetValue<Sprite>();
                if (ground != null)
                {
                    Ground = ground;
                    break;
                }
            }

            // セルは窓ごとに地が違う（製作の窓・装備など）。所持品のセルの地を持つものを選ぶ
            UIItemCell fallback = null;
            foreach (UIItemCell cell in Resources.FindObjectsOfTypeAll<UIItemCell>())
            {
                if (cell == null)
                {
                    continue;
                }

                Traverse t = Traverse.Create(cell);
                Image back = t.Field("background").GetValue<Image>();
                if (back != null && back.sprite != null && back.sprite.name == CellBackName)
                {
                    UseCell(cell, t, back);
                    return;
                }

                if (fallback == null && t.Field("icon").GetValue<Image>() != null)
                {
                    fallback = cell;
                }
            }

            if (fallback != null)
            {
                UseCell(fallback, Traverse.Create(fallback), null);
            }
        }

        private void UseCell(UIItemCell cell, Traverse t, Image back)
        {
            CellSource = cell;
            if (back != null)
            {
                CellBack = back.sprite;
            }

            Image icon = t.Field("icon").GetValue<Image>();
            Material material = icon == null ? null : icon.material;
            // 材質が UI の既定のもの（色を掛けるだけ）なら、縁の置き換えをすると全体が暗くなる。そのときは使わない
            if (material != null && material.shader != null && material != Canvas.GetDefaultCanvasMaterial()
                && material.shader.name != "UI/Default")
            {
                IconMaterial = material;
            }

            ImageColors colors = t.Field("colors").GetValue<ImageColors>();
            if (colors != null)
            {
                IconOutline = colors.NormalColor;
            }
        }

        private void ResolveFromAtlases()
        {
            var finder = new SpriteFinder();
            Strip = finder.Find(StripName);
            StripCorner = finder.Find(StripCornerName);
            if (HeaderBar == null)
            {
                HeaderBar = finder.Find(HeaderBarName);
            }

            if (HeaderDeco == null)
            {
                HeaderDeco = finder.Find(HeaderDecoName);
            }

            if (Ground == null)
            {
                Ground = finder.Find(GroundName);
            }

            if (CellBack == null)
            {
                CellBack = finder.Find(CellBackName);
            }

            Fade = finder.Find(FadeName);
        }

        private string Describe()
        {
            var sb = new StringBuilder();
            sb.Append("帯 ").Append(HasStrip ? Name(Strip) + "＋" + Name(StripCorner) : "なし（見出しは板の中）");
            sb.Append("・見出し板 ").Append(Name(HeaderBar)).Append("＋").Append(Name(HeaderDeco));
            sb.Append("・地 ").Append(Name(Ground));
            sb.Append("・セル ").Append(Name(CellBack));
            sb.Append("・下端の影 ").Append(Name(Fade));
            sb.Append("・アイコンの材質 ").Append(IconMaterial == null ? "なし（青い縁のまま）" : IconMaterial.shader.name);
            sb.Append("（縁の色 ").Append(ColorUtility.ToHtmlStringRGB(IconOutline)).Append("）");
            return sb.ToString();
        }

        private static string Name(Sprite sprite) => sprite == null ? "-" : sprite.name + "（" + sprite.rect.width.ToString("0") + "x" + sprite.rect.height.ToString("0") + "）";

        private static Image FindImage(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child != root && child.name == name)
                {
                    return child.GetComponent<Image>();
                }
            }

            return null;
        }

        /// <summary>sprite を名前で探す。読み込み済みの sprite を先に、無ければ読み込み済みの SpriteAtlas から引く（PlateParts も使う）。</summary>
        internal sealed class SpriteFinder
        {
            private Dictionary<string, Sprite> loaded;
            private SpriteAtlas[] atlases;

            public Sprite Find(string name)
            {
                if (AtlasCopies.TryGetValue(name, out Sprite copy) && copy != null)
                {
                    return copy;
                }

                if (loaded == null)
                {
                    loaded = new Dictionary<string, Sprite>();
                    foreach (Sprite s in Resources.FindObjectsOfTypeAll<Sprite>())
                    {
                        if (s != null && !loaded.ContainsKey(s.name))
                        {
                            loaded[s.name] = s;
                        }
                    }
                }

                if (loaded.TryGetValue(name, out Sprite found))
                {
                    AtlasCopies[name] = found;
                    return found;
                }

                if (atlases == null)
                {
                    atlases = Resources.FindObjectsOfTypeAll<SpriteAtlas>();
                }

                foreach (SpriteAtlas atlas in atlases)
                {
                    Sprite s = atlas == null ? null : atlas.GetSprite(name);
                    if (s != null)
                    {
                        s.name = name;
                        AtlasCopies[name] = s;
                        return s;
                    }
                }

                return null;
            }
        }
    }
}
