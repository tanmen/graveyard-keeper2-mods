using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using LazyBearTechnology;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CraftTodo
{
    /// <summary>
    /// 料理ごとの札の絵を、ゲームの技術ツリーの札（TechTreeElementWidget。MainScene にプールの元が常駐している）から写す。
    /// 札の絵（tech_tree-tech_plate-opened は金・-available は緑、126x64）は 1 枚の絵に名札の帯・中・足が入っている。
    /// 足を外し（行 0〜50 の下に行 61〜63 を継ぐ）、横に広げる（中央の列 32〜93 を 1 回おきに左右反転して繰り返す）ために、
    /// 同じテクスチャの範囲を切った sprite を作る（PanelSkin.CropRows と同じ。アトラスの中の範囲を指すだけ）。
    /// 名札の文字の形は札の HeaderLabel の TextStyleComponent が持つ TextStyle（言語ごとの書体はゲームが選ぶ）。
    /// セルの地（tech_tree-tech_plate-item_cell、32x32）は札の部品から参照されていないので、アトラスから名前で引く。
    /// 札が引けない・切れないときは使わない（制作メニューは 0.4.4 の形に戻る）。調べた根拠は docs/game-api-notes.md の「UI の絵」。
    /// </summary>
    internal sealed class PlateParts
    {
        internal const string OpenedName = "tech_tree-tech_plate-opened";
        internal const string AvailableName = "tech_tree-tech_plate-available";
        internal const string CellName = "tech_tree-tech_plate-item_cell";

        // 札の絵の寸法（2026-11-21 に画素で測った。行は上から数える）。
        // 行 0 黒い縁・1〜14 名札の帯・15〜16 区切り・17〜50 中・51〜60 足・61 金の線・62 黒い縁・63 透明
        public const int SourceWidth = 126;
        public const int SourceHeight = 64;
        public const int TopRows = 51;
        public const int BottomFrom = 61;
        public const int BottomRows = 3;

        /// <summary>横に広げるときに繰り返す中央の列（32〜93）。左右の 32 列はそのまま。</summary>
        public const int MidLeft = 32;
        public const int MidRight = 94;

        /// <summary>足を外した札の高さ。</summary>
        public const int Height = TopRows + BottomRows;

        /// <summary>そろった料理は名札の帯だけにする: 行 0〜14 の下に行 61〜63 を継ぐ。</summary>
        public const int BandRows = 15;

        public const int CollapsedHeight = BandRows + BottomRows;

        /// <summary>札のいちばん下の透明な行の数（行 63）。札の見えている下端はこの分だけ上。</summary>
        public const int ClearBottom = 1;

        /// <summary>名札の文字の枠の高さ（ゲームの HeaderLabel は札の上端から 16 の高さ、横は中央）。</summary>
        public const int NameHeight = 16;

        /// <summary>
        /// 名札の文字の枠の上端。ゲームと同じ 0 に置くと、日本語の文字（縁取りを含めて 12 行）が行 1〜12 に出て、
        /// 帯（行 1〜14）の上 0・下 2 になる（0.5.4 を 2026-11-21 に画面で数えた。本人の言葉:「文字がまだ中心より上に寄ってる」）。
        /// 1 下げて上 1・下 1 にする。× も同じだけ下げる
        /// </summary>
        public const int NameTop = 1;

        /// <summary>セルの段の上端（中の行 17〜50 に 32 のセルを 1 行空けて置く）。</summary>
        public const int RowTop = 18;

        public const int DefaultCellSize = 32;

        // 切った sprite。引き直しても同じ範囲は作り直さない（元の sprite と範囲で引く）
        private static readonly Dictionary<(int, int, int, int, int), Sprite> Pieces = new Dictionary<(int, int, int, int, int), Sprite>();

        // 縁（Sliced の border）を付け替えた sprite（元の sprite と縁で引く）
        private static readonly Dictionary<(int, Vector4), Sprite> Rebordered = new Dictionary<(int, Vector4), Sprite>();

        public Sprite Opened { get; private set; }

        public Sprite Available { get; private set; }

        public Sprite Cell { get; private set; }

        /// <summary>技術ツリーのアイコンの材質（LinkedEntityWidget の Icon）。引けなければ null（所持品のセルの材質を使う）。</summary>
        public Material IconMaterial { get; private set; }

        /// <summary>技術ツリーのアイコンの縁の色（材質の _Color）。引けなければ null（所持品のセルの色を使う）。</summary>
        public Color? IconOutline { get; private set; }

        public bool CanCrop { get; private set; }

        /// <summary>札として使えるか（金と緑の札がそろい、範囲を切れる）。</summary>
        public bool Usable => Opened != null && Available != null && CanCrop;

        /// <summary>部品が全部そろったか。そろわなければときどき引き直す。</summary>
        public bool Complete => Usable && Cell != null && name != null;

        public string Description { get; private set; } = "-";

        private NameLook name;
        private NameLook readyName;

        public static PlateParts Resolve()
        {
            var plates = new PlateParts();
            try
            {
                plates.ResolveFromTechTree();
                plates.ResolveFromAtlases();
                plates.CanCrop = Croppable(plates.Opened) && Croppable(plates.Available);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"技術ツリーの札の絵を引けなかった（札は使わない）: {e.Message}");
                plates.CanCrop = false;
            }

            plates.Description = plates.Describe();
            return plates;
        }

        /// <summary>
        /// 札の絵の範囲（左上から x・<paramref name="top"/>、w x h）を切った sprite。<paramref name="ready"/> なら緑の札から切る。
        /// </summary>
        public Sprite Piece(bool ready, int x, int top, int w, int h)
        {
            Sprite source = ready ? Available : Opened;
            var key = (source.GetInstanceID(), x, top, w, h);
            if (Pieces.TryGetValue(key, out Sprite cached) && cached != null)
            {
                return cached;
            }

            Rect area = source.textureRect;
            // テクスチャの y は下から上へ
            var rect = new Rect(area.x + x, area.y + (SourceHeight - top - h), w, h);
            Sprite piece = Sprite.Create(source.texture, rect, new Vector2(0.5f, 0.5f), source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            piece.name = source.name + "-" + x + "_" + top + "_" + w + "x" + h;
            Pieces[key] = piece;
            return piece;
        }

        /// <summary>
        /// 同じ絵で Sliced の縁（左・下・右・上）だけを付け替えた sprite。縁の中が同じ行・列の繰り返しの絵なら、縁を細くしても
        /// 見え方は変わらず、縁の合計より低く（狭く）置いても縁が潰れない。範囲が引けない詰め方なら null。
        /// </summary>
        public static Sprite WithBorder(Sprite sprite, Vector4 border)
        {
            if (sprite == null)
            {
                return null;
            }

            var id = (sprite.GetInstanceID(), border);
            if (Rebordered.TryGetValue(id, out Sprite cached))
            {
                return cached;
            }

            Sprite result = null;
            try
            {
                if (CanUseTextureRect(sprite))
                {
                    result = Sprite.Create(sprite.texture, sprite.textureRect, new Vector2(0.5f, 0.5f), sprite.pixelsPerUnit, 0, SpriteMeshType.FullRect, border);
                    result.name = sprite.name + "-border";
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"絵 {sprite.name} の縁を付け替えられなかった（元の縁のまま使う）: {e.Message}");
            }

            Rebordered[id] = result;
            return result;
        }

        /// <summary>名札の文字に、技術ツリーの札の文字の形を当てる（緑の札なら緑の札の文字の形）。引けていなければ何もしない。</summary>
        public void ApplyName(TMP_Text label, bool ready)
        {
            NameLook look = ready && readyName != null ? readyName : name;
            if (look == null)
            {
                return;
            }

            // TextStyle.ApplyStyle（の中の LanguageModLoader.ApplyDirection）は、初めて当てたときの揃え方を覚えておき、当てるたびにそれへ戻す。
            // LabelStyler は揃え方を決める前に 1 度当てているので、そのままだと左上揃えに戻る（0.5.3 で名札の文字が左上にずれた）
            TextAlignmentOptions alignment = label.alignment;
            look.Style.ApplyStyle(label, look.StaticFont, look.Color, look.Outline);
            label.alignment = alignment;
            if (look.Size > 0f)
            {
                label.fontSize = look.Size;
            }
        }

        private void ResolveFromTechTree()
        {
            foreach (TechTreeElementWidget widget in Resources.FindObjectsOfTypeAll<TechTreeElementWidget>())
            {
                if (widget == null)
                {
                    continue;
                }

                Traverse t = Traverse.Create(widget);
                GameObject unlocked = t.Field("unlockedObj").GetValue<GameObject>();
                GameObject available = t.Field("availableObj").GetValue<GameObject>();
                Image openedBack = BackOf(unlocked);
                if (openedBack == null || openedBack.sprite == null)
                {
                    continue;
                }

                Opened = openedBack.sprite;
                Image availableBack = BackOf(available);
                if (availableBack != null && availableBack.sprite != null)
                {
                    Available = availableBack.sprite;
                }

                TextMeshProUGUI[] headers = t.Field("headers").GetValue<TextMeshProUGUI[]>();
                if (headers != null)
                {
                    foreach (TextMeshProUGUI header in headers)
                    {
                        NameLook look = NameLook.Of(header);
                        if (look == null)
                        {
                            continue;
                        }

                        if (name == null && header.transform.IsChildOf(unlocked.transform))
                        {
                            name = look;
                        }
                        else if (readyName == null && available != null && header.transform.IsChildOf(available.transform))
                        {
                            readyName = look;
                        }
                    }
                }

                break;
            }

            foreach (LinkedEntityWidget cell in Resources.FindObjectsOfTypeAll<LinkedEntityWidget>())
            {
                Image icon = cell == null ? null : Traverse.Create(cell).Field("icon").GetValue<Image>();
                Material material = icon == null ? null : icon.material;
                // 材質が UI の既定のもの（色を掛けるだけ）なら使わない（MenuParts と同じ）
                if (material == null || material.shader == null || material == Canvas.GetDefaultCanvasMaterial() || material.shader.name == "UI/Default")
                {
                    continue;
                }

                IconMaterial = material;
                // 技術ツリーのアイコンは BlueColorReplaceComponent が Image の色（1F1F28）を縁の色にする。材質の _Color は元の白のまま
                // （0.5.0〜0.5.3 はこれを読んで縁が白くなった）
                if (icon.GetComponent<BlueColorReplaceComponent>() != null)
                {
                    IconOutline = icon.color;
                }

                break;
            }
        }

        private void ResolveFromAtlases()
        {
            var finder = new MenuParts.SpriteFinder();
            if (Opened == null)
            {
                Opened = finder.Find(OpenedName);
            }

            if (Available == null)
            {
                Available = finder.Find(AvailableName);
            }

            Cell = finder.Find(CellName);
        }

        private static Image BackOf(GameObject state)
        {
            if (state == null)
            {
                return null;
            }

            Transform back = state.transform.Find("Back");
            return back != null ? back.GetComponent<Image>() : state.GetComponentInChildren<Image>(true);
        }

        /// <summary>
        /// 測った寸法の絵で、テクスチャの中の範囲が引けるか（回転・きつい詰め方・端の切り詰めがあると範囲で切れない）。
        /// </summary>
        private static bool Croppable(Sprite sprite)
        {
            if (sprite == null || Mathf.Abs(sprite.rect.width - SourceWidth) > 0.5f || Mathf.Abs(sprite.rect.height - SourceHeight) > 0.5f)
            {
                return false;
            }

            return CanUseTextureRect(sprite);
        }

        private static bool CanUseTextureRect(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
            {
                return false;
            }

            if (sprite.packed && (sprite.packingRotation != SpritePackingRotation.None || sprite.packingMode == SpritePackingMode.Tight))
            {
                return false;
            }

            Rect area = sprite.textureRect;
            return Mathf.Abs(area.width - sprite.rect.width) <= 0.5f && Mathf.Abs(area.height - sprite.rect.height) <= 0.5f;
        }

        private string Describe()
        {
            var sb = new StringBuilder();
            sb.Append(Name(Opened)).Append("＋").Append(Name(Available));
            sb.Append(CanCrop ? "（足を外して横に広げる）" : "（範囲で切れないので使わない）");
            sb.Append("・セル ").Append(Name(Cell));
            sb.Append("・名札の文字 ").Append(name == null ? "-" : name.Describe());
            if (readyName != null)
            {
                sb.Append("／").Append(readyName.Describe());
            }

            sb.Append("・アイコンの材質 ").Append(IconMaterial == null ? "-" : IconMaterial.shader.name);
            sb.Append("（縁の色 ").Append(IconOutline.HasValue ? ColorUtility.ToHtmlStringRGB(IconOutline.Value) : "-").Append("）");
            return sb.ToString();
        }

        private static string Name(Sprite sprite) => sprite == null ? "-" : sprite.name + "（" + sprite.rect.width.ToString("0") + "x" + sprite.rect.height.ToString("0") + "）";

        /// <summary>札の HeaderLabel の文字の形（TextStyleComponent.ApplyStyle と同じ引数で当てる）。</summary>
        private sealed class NameLook
        {
            public TextStyle Style;
            public bool StaticFont;
            public Color? Color;
            public Color? Outline;
            public float Size;

            public static NameLook Of(TextMeshProUGUI header)
            {
                TextStyleComponent component = header == null ? null : header.GetComponent<TextStyleComponent>();
                TextStyle style = component == null ? null : component.CurrentTextStyle;
                if (style == null)
                {
                    return null;
                }

                Traverse t = Traverse.Create(component);
                var look = new NameLook { Style = style, Size = header.fontSize };
                look.StaticFont = t.Field("staticFont").GetValue<bool>();
                if (t.Field("hasCustomColor").GetValue<bool>())
                {
                    look.Color = t.Field("customColor").GetValue<Color>();
                }

                if (t.Field("hasCustomOutlineColor").GetValue<bool>())
                {
                    look.Outline = t.Field("customOutlineColor").GetValue<Color>();
                }

                return look;
            }

            public string Describe() => Style.name + "（" + Size.ToString("0") + "）";
        }
    }
}
