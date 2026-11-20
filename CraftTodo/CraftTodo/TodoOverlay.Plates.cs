using System.Collections.Generic;
using CraftTodo.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CraftTodo
{
    /// <summary>
    /// 0.5.x の形: 制作機ごとの区切り（所持品の窓の見出し板に制作機の名前だけ。× は無い）の下に所持品の窓の茶色の地を敷き、
    /// その中に料理ごとの札を 1 枚ずつ縦に並べる（docs/mockup-craft-menu-0.5.png）。
    ///   幅: 板は 210 に固定（セルの段が入らないときだけ広げる。名前が入らなければ「…」で省略）。地の中の余白は見えている間で左右 4・上 3・下 6
    ///   札: 技術ツリーの札（PlateParts）の足を外し、横に広げる。名札の帯に「料理 ×回数」（中央）と ×（右端から 7）、
    ///       中に「[作る物] = [素材] + [素材] …」の 32 のセル（行 18 から、札の中央）
    ///   そろった料理は緑の札の名札の帯だけにして（セルは出さない）、その制作機の一番下へ。料理が全部そろった制作機は一番下へ
    ///   （制作機の名前の色は変えない）。札の間 4・制作機の間 6。
    ///   札を押すと（× を除く）、そろったかに関わらずセルの段を出し入れする（0.5.4〜。そろった / そろわなくなったら既定に戻る）。
    /// 制作機の順と、制作機の中の料理の順は、メモの一覧の順（新しく足したものが先頭）のまま。
    /// </summary>
    internal sealed partial class TodoOverlay
    {
        // 板の幅（本人の指定、2026-11-21）
        private const int MenuWidth = 210;

        private const int PlateGap = 4;
        private const int GroupGap = 6;

        // 記号（= +）の左右。ゲームの書体の記号は 8 幅で、3 だと作る物＋素材 3 つの段が 170 になり札（178）に入らなかった（0.5.3 は幅が 214 に広がった）
        private const int PlateOpGap = 2;

        // 制作機の茶色の地の中の余白（本人の指定、2026-11-21:「地の上下左右のpaddingを4に」→「地の上3下5に」→「地の下paddingを6に」）。
        // 見えている間で数える: 左右は地の端から、上は見出し板の下端から（見出し板は地に 1 重なる）、下は札の見えている下端から
        // （札のいちばん下の行は透明）地の下端まで
        private const int StationPadSide = 4;
        private const int StationPadTop = 3;
        private const int StationPadBottom = 6;

        // 茶色の地（widget_inventory-items_bg-active、178x48、縁 60/20/60/20）は行 1〜43 が同じ行の繰り返しで、縁は上 1 行・下 4 行だけ。
        // 縁を細くした sprite を作り、札 1 枚だけの低い地でも縁が潰れないようにする（左・下・右・上）
        private static readonly Vector4 StationGroundBorder = new Vector4(60f, 4f, 60f, 1f);

        // セルの段の左右に空ける幅の合計（札の縁と模様の分）
        private const int PlateRowPad = 12;

        // 名札の × は右端から 7、× と名札の文字の間は 4（文字は札の中央に置くので、両側に同じだけ空ける）
        private const int PlateCloseRight = 7;
        private const int PlateCloseGap = 4;

        // 32 のセルの数は地の端から 3・3 内側（44 の所持品のセルの 6・6 を、図で描いた位置の差のぶん詰めた）
        private const int PlateCountInset = 3;

        private static readonly Color PlateCloseColor = new Color32(232, 214, 180, 255);

        private readonly List<GroupView> groups = new List<GroupView>();

        // 札を押して出し入れした状態（メモの鍵 → セルの段を出すか）。並べ直しても残し、そろった / そろわなくなったら捨てる。
        // ゲームを閉じると消える（保存しない）
        private readonly Dictionary<string, bool> plateExpanded = new Dictionary<string, bool>();
        private Sprite stationGround;
        private int stationGroundMin;

        private int PlateCellSize => plates?.Cell != null ? (int)UiFactory.NativeSize(plates.Cell, referencePixelsPerUnit).y : PlateParts.DefaultCellSize;

        private int PlateWidth => innerWidth - StationPadSide * 2;

        /// <summary>メモを制作機ごとにまとめて区切りと札を作る。要る内側の幅を返す（ふつうは板の幅 210 の内側）。</summary>
        private int CreateGroups(Plugin plugin, out string note)
        {
            // 地は縁を細くした sprite（作れなければ元の縁のまま、地の高さを縁の合計より低くしない）
            Sprite thin = PlateParts.WithBorder(parts.Ground, StationGroundBorder);
            stationGround = thin ?? parts.Ground;
            stationGroundMin = thin == null && parts.Ground != null ? Mathf.CeilToInt(parts.Ground.border.y + parts.Ground.border.w) : 0;

            // 消えたメモの出し入れの状態は捨てる
            var keys = new HashSet<string>();
            foreach (TodoEntry entry in plugin.Todos.Entries)
            {
                keys.Add(entry.Key);
            }

            var stale = new List<string>();
            foreach (string key in plateExpanded.Keys)
            {
                if (!keys.Contains(key))
                {
                    stale.Add(key);
                }
            }

            foreach (string key in stale)
            {
                plateExpanded.Remove(key);
            }

            var byKey = new Dictionary<string, GroupView>();
            int opWidth = 0;
            int widestRow = 0;
            string widestRowKey = "-";
            foreach (TodoEntry entry in plugin.Todos.Entries)
            {
                string key = GroupKey(entry);
                if (!byKey.TryGetValue(key, out GroupView group))
                {
                    string name = GameBridge.StationName(entry);
                    group = CreateGroup(string.IsNullOrEmpty(name) ? Strings.NoStation : name);
                    byKey[key] = group;
                    groups.Add(group);
                }

                EntryView view = CreatePlate(entry, ref opWidth);
                group.Entries.Add(view);
                views.Add(view);
                if (view.RowWidth > widestRow)
                {
                    widestRow = view.RowWidth;
                    widestRowKey = entry.Key;
                }
            }

            int fixedInner = MenuWidth - SideInset * 2;
            int rowNeed = Mathf.Max(widestRow + PlateRowPad, PlateParts.SourceWidth) + StationPadSide * 2;
            string ground = stationGround == null ? "-" : stationGround.name + (thin != null ? "（縁 " + StationGroundBorder.ToString("0") + "）" : "（元の縁。高さ " + stationGroundMin + " 以上）");
            note = $"固定 {MenuWidth}（内側 {fixedInner}） / セルの段 {rowNeed}（{widestRowKey}） / 制作機の地 {ground}";
            return Mathf.Max(fixedInner, rowNeed);
        }

        /// <summary>同じ制作機（置いてある 1 台）のメモを 1 つの区切りにまとめる鍵。台が分からなければ制作機の種類でまとめる。</summary>
        private static string GroupKey(TodoEntry entry)
        {
            return !string.IsNullOrEmpty(entry.StationId) ? "id:" + entry.StationId : "def:" + entry.StationDefId;
        }

        /// <summary>制作機の区切り: 茶色の地を先に作り、見出し板（両端に飾り、右は左右反転）を後に作る（見出し板を地に 1 重ねて上に描く）。</summary>
        private GroupView CreateGroup(string name)
        {
            var group = new GroupView { FullTitle = name };
            group.Ground = UiFactory.CreateImage("StationGround", content, stationGround, PlainGround).rectTransform;
            group.Header = UiFactory.CreateImage("Station", content, parts.HeaderBar, PlainHeader).rectTransform;
            if (parts.HeaderDeco != null)
            {
                group.DecoLeft = UiFactory.CreateImage("DecoLeft", group.Header, parts.HeaderDeco, Color.clear).rectTransform;
                group.DecoRight = UiFactory.CreateImage("DecoRight", group.Header, parts.HeaderDeco, Color.clear).rectTransform;
                group.DecoSize = UiFactory.NativeSize(parts.HeaderDeco, referencePixelsPerUnit);
            }

            group.Title = UiFactory.CreateLabel(group.Header, "Title", styler, TextRole.Header, TextAlignmentOptions.Center);
            group.Title.text = name;
            // 0.4.4 の見出しと同じく、飾りの見えている模様の幅だけ空ける
            int deco = group.DecoLeft != null ? Mathf.Min(HeaderDecoVisible + 1, (int)group.DecoSize.x) : 4;
            group.TitleInset = deco + TitleGap;
            return group;
        }

        private void SizeGroup(GroupView group)
        {
            int bar = BarHeight;
            UiFactory.Place(group.Header, 0f, 0f, innerWidth, bar);
            if (group.DecoLeft != null)
            {
                UiFactory.Place(group.DecoLeft, 0f, 0f, group.DecoSize.x, group.DecoSize.y);
                UiFactory.Place(group.DecoRight, innerWidth - group.DecoSize.x, 0f, group.DecoSize.x, group.DecoSize.y, mirror: true);
            }

            UiFactory.Stretch(group.Title.rectTransform, group.TitleInset, 0f, group.TitleInset, 0f);
            group.Title.text = FitText(group.Title, group.FullTitle, innerWidth - group.TitleInset * 2);
        }

        /// <summary>料理ごとの札。絵の切れ端は幅が決まってから並べる（<see cref="SizePlate"/>）。</summary>
        private EntryView CreatePlate(TodoEntry entry, ref int opWidth)
        {
            var view = new EntryView { Entry = entry, IsPlate = true };
            view.Root = UiFactory.CreateRect("Plate", content);
            string key = entry.Key;
            // 札のどこを押しても（× を除く）セルの段を出し入れする。当たる面は札いっぱいで、いちばん下（× の文字が上で先に当たる）
            ClickArea click = UiFactory.CreateRect("Click", view.Root).gameObject.AddComponent<ClickArea>();
            UiFactory.Stretch(click.rectTransform, 0f, 0f, 0f, 0f);
            click.Clicked = () => TogglePlate(key);

            // 絵を先に作り、セルと文字を後に作る（上に描く）。足を外した札と名札の帯だけの形を両方持ち、出し分ける
            view.PlateFull = UiFactory.CreateRect("Full", view.Root);
            view.PlateBand = UiFactory.CreateRect("Band", view.Root);

            int cell = PlateCellSize;
            view.Row = UiFactory.CreateRect("Row", view.Root);
            int x = 0;
            view.ProductCount = CreatePlateCell(view.Row, x, GameBridge.Sprite(entry.IconId), entry, -1);
            view.ProductCount.text = Strings.Multiplier(entry.Multiplier);
            view.ProductCount.color = TimesColor;
            x += cell;

            for (int i = 0; i < entry.Materials.Count; i++)
            {
                TMP_Text op = UiFactory.CreateLabel(view.Row, "Op", styler, TextRole.Header, TextAlignmentOptions.Center);
                op.text = i == 0 ? Strings.EqualsSign : Strings.PlusSign;
                if (opWidth == 0)
                {
                    opWidth = Mathf.Max(UiFactory.TextWidth(op, Strings.EqualsSign), UiFactory.TextWidth(op, Strings.PlusSign), 4);
                }

                x += PlateOpGap;
                UiFactory.Place(op.rectTransform, x, 0f, opWidth, cell);
                x += opWidth + PlateOpGap;
                view.Materials.Add(CreatePlateCell(view.Row, x, GameBridge.MaterialIcon(entry.Materials[i]), entry, i));
                x += cell;
            }

            view.RowWidth = x;

            // 名札の文字は技術ツリーの札と同じ形（引けなければ所持品の見出しの形のまま）
            view.Title = UiFactory.CreateLabel(view.Root, "Name", styler, TextRole.Header, TextAlignmentOptions.Center);
            plates.ApplyName(view.Title, false);
            view.FullTitle = Strings.Title(GameBridge.RecipeName(entry), entry.Multiplier, null);
            view.Title.text = view.FullTitle;

            view.Close = UiFactory.CreateLabel(view.Root, "Remove", styler, TextRole.Header, TextAlignmentOptions.Center);
            plates.ApplyName(view.Close, false);
            view.Close.text = Strings.Close;
            view.Close.color = PlateCloseColor;
            view.Close.raycastTarget = true;
            view.CloseWidth = UiFactory.TextWidth(view.Close, view.Close.text) + CloseBoxPad;

            Button button = view.Close.gameObject.AddComponent<Button>();
            button.targetGraphic = view.Close;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => Plugin.Instance.RemoveTodo(key));
            return view;
        }

        /// <summary>セルの段を出しているか。押して替えていなければ、そろっていない料理は出し、そろった料理は畳む。</summary>
        private bool IsExpanded(EntryView view)
        {
            return plateExpanded.TryGetValue(view.Entry.Key, out bool expanded) ? expanded : !view.Ready;
        }

        private void TogglePlate(string key)
        {
            EntryView view = views.Find(v => v.IsPlate && v.Entry.Key == key);
            if (view == null)
            {
                return;
            }

            plateExpanded[key] = !IsExpanded(view);
            SkinPlate(view);
            Layout();
            GameBridge.PlayClick();
        }

        /// <summary>札のセル。セルの上を押しても札の出し入れになるよう、セルが受けた左クリックを札へ回す。</summary>
        private TMP_Text CreatePlateCell(RectTransform row, int x, Sprite icon, TodoEntry entry, int materialIndex)
        {
            string key = entry.Key;
            return CreateCell(row, x, icon, plates.Cell, PlateCellSize, PlateCountInset,
                plates.IconMaterial ?? parts.IconMaterial, plates.IconOutline ?? parts.IconOutline, entry, materialIndex, () => TogglePlate(key));
        }

        /// <summary>札の幅に合わせて絵の切れ端を並べ、名札の文字・×・セルの段を置く。</summary>
        private void SizePlate(EntryView view)
        {
            int width = PlateWidth;
            UiFactory.Place(view.PlateFull, 0f, 0f, width, PlateParts.Height);
            UiFactory.Place(view.PlateBand, 0f, 0f, width, PlateParts.CollapsedHeight);
            // 足を外す（行 0〜50 の下に行 61〜63）。畳んだ形は名札の帯だけ（行 0〜14 の下に行 61〜63）
            AddPlateBand(view, view.PlateFull, 0, PlateParts.TopRows, 0);
            AddPlateBand(view, view.PlateFull, PlateParts.BottomFrom, PlateParts.BottomRows, PlateParts.TopRows);
            AddPlateBand(view, view.PlateBand, 0, PlateParts.BandRows, 0);
            AddPlateBand(view, view.PlateBand, PlateParts.BottomFrom, PlateParts.BottomRows, PlateParts.BandRows);

            // 名札の文字と × は帯の上下の真ん中に来るよう 1 下げる（PlateParts.NameTop）
            UiFactory.Place(view.Title.rectTransform, 0f, PlateParts.NameTop, width, PlateParts.NameHeight);
            // × の文字の右端を札の右端から 7 に（押せる箱は文字の左右に 1 ずつ広い）
            UiFactory.Place(view.Close.rectTransform, width - PlateCloseRight - view.CloseWidth + CloseBoxPad / 2, PlateParts.NameTop, view.CloseWidth, PlateParts.NameHeight);
            int closeText = view.CloseWidth - CloseBoxPad;
            view.Title.text = FitText(view.Title, view.FullTitle, width - (PlateCloseRight + closeText + PlateCloseGap) * 2);
            UiFactory.Place(view.Row, (width - view.RowWidth) / 2, PlateParts.RowTop, view.RowWidth, PlateCellSize);
            view.FullHeight = PlateParts.Height;
            view.CollapsedHeight = PlateParts.CollapsedHeight;
            SkinPlate(view);
        }

        /// <summary>
        /// 札の絵の 1 段（上から <paramref name="sourceTop"/> 行目から <paramref name="rows"/> 行）を、札の幅に広げて <paramref name="y"/> に並べる。
        /// 左右の 32 列はそのまま、中央の列 32〜93 を繰り返し、1 回おきに左右反転する（継ぎ目の左右が同じ列になるので模様が切れない）。
        /// </summary>
        private void AddPlateBand(EntryView view, RectTransform parent, int sourceTop, int rows, int y)
        {
            int width = PlateWidth;
            int right = PlateParts.SourceWidth - PlateParts.MidRight;
            int middle = PlateParts.MidRight - PlateParts.MidLeft;
            AddPlatePiece(view, parent, 0, sourceTop, PlateParts.MidLeft, rows, 0, y, false);
            int x = PlateParts.MidLeft;
            for (int i = 0; x < width - right; i++)
            {
                int segment = Mathf.Min(middle, width - right - x);
                bool mirror = i % 2 == 1;
                // 反転した段の左端は元の絵の右端（列 93）から始まる
                int source = mirror ? PlateParts.MidRight - segment : PlateParts.MidLeft;
                AddPlatePiece(view, parent, source, sourceTop, segment, rows, x, y, mirror);
                x += segment;
            }

            AddPlatePiece(view, parent, PlateParts.MidRight, sourceTop, right, rows, width - right, y, false);
        }

        private void AddPlatePiece(EntryView view, RectTransform parent, int sourceX, int sourceTop, int w, int h, int x, int y, bool mirror)
        {
            Image image = UiFactory.CreateImage("Piece", parent, null, Color.clear);
            UiFactory.Place(image.rectTransform, x, y, w, h, mirror);
            view.Pieces.Add(new PlatePiece { Image = image, X = sourceX, Top = sourceTop, Width = w, Height = h });
        }

        /// <summary>
        /// 札の絵（そろったら緑・そろっていなければ金）と形（セルの段を出すなら足を外した札、出さないなら名札の帯だけ）、
        /// 名札の文字の形を合わせる。
        /// </summary>
        private void SkinPlate(EntryView view)
        {
            foreach (PlatePiece piece in view.Pieces)
            {
                piece.Image.sprite = plates.Piece(view.Ready, piece.X, piece.Top, piece.Width, piece.Height);
                piece.Image.type = Image.Type.Simple;
                piece.Image.color = Color.white;
            }

            bool expanded = IsExpanded(view);
            view.PlateFull.gameObject.SetActive(expanded);
            view.PlateBand.gameObject.SetActive(!expanded);
            view.Row.gameObject.SetActive(expanded);
            plates.ApplyName(view.Title, view.Ready);
            plates.ApplyName(view.Close, view.Ready);
            view.Close.color = PlateCloseColor;
        }

        /// <summary>
        /// 制作機ごとに積む。そろっていない料理のある制作機を一覧の順に、料理が全部そろった制作機をその下に。
        /// 制作機の中も、そろっていない料理を先に、そろった料理（緑の名札の帯）を下に。積んだ高さを返す。
        /// </summary>
        private int StackGroups()
        {
            int bar = BarHeight;
            int y = 0;
            bool first = true;
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (GroupView group in groups)
                {
                    bool done = group.Entries.TrueForAll(v => v.Ready);
                    if (done != (pass == 1))
                    {
                        continue;
                    }

                    if (!first)
                    {
                        y += GroupGap;
                    }

                    first = false;
                    UiFactory.Place(group.Header, 0f, y, innerWidth, bar);
                    int groundTop = y + bar - HeaderOverlap;
                    int inside = y + bar + StationPadTop;
                    bool firstPlate = true;
                    for (int inner = 0; inner < 2; inner++)
                    {
                        foreach (EntryView view in group.Entries)
                        {
                            if (view.Ready != (inner == 1))
                            {
                                continue;
                            }

                            if (!firstPlate)
                            {
                                inside += PlateGap;
                            }

                            firstPlate = false;
                            int height = IsExpanded(view) ? view.FullHeight : view.CollapsedHeight;
                            UiFactory.Place(view.Root, StationPadSide, inside, PlateWidth, height);
                            inside += height;
                        }
                    }

                    int groundHeight = Mathf.Max(inside - PlateParts.ClearBottom + StationPadBottom - groundTop, stationGroundMin);
                    UiFactory.Place(group.Ground, 0f, groundTop, innerWidth, groundHeight);
                    y = groundTop + groundHeight;
                }
            }

            return y;
        }

        /// <summary>
        /// <paramref name="maxWidth"/> に入らなければ末尾を削って「…」を付ける（書体に「…」が無ければ「...」）。
        /// 本人の言葉（2026-11-21）:「文字列に幅が引っ張られてるのであれば、省略してもよい」。
        /// </summary>
        private static string FitText(TMP_Text label, string text, int maxWidth)
        {
            if (string.IsNullOrEmpty(text) || maxWidth <= 0 || UiFactory.TextWidth(label, text) <= maxWidth)
            {
                return text;
            }

            string ellipsis = label.font != null && label.font.HasCharacter('…', true, true) ? "…" : "...";
            for (int length = text.Length - 1; length > 0; length--)
            {
                // サロゲートペアの片方だけを残さない
                if (char.IsHighSurrogate(text[length - 1]))
                {
                    continue;
                }

                string candidate = text.Substring(0, length).TrimEnd() + ellipsis;
                if (UiFactory.TextWidth(label, candidate) <= maxWidth)
                {
                    return candidate;
                }
            }

            return ellipsis;
        }

        private sealed class PlatePiece
        {
            public Image Image;
            public int X;
            public int Top;
            public int Width;
            public int Height;
        }

        private sealed class GroupView
        {
            public RectTransform Ground;
            public RectTransform Header;
            public RectTransform DecoLeft;
            public RectTransform DecoRight;
            public Vector2 DecoSize;
            public TMP_Text Title;
            public string FullTitle;
            public int TitleInset;
            public readonly List<EntryView> Entries = new List<EntryView>();
        }
    }
}
