using System;
using System.Collections.Generic;
using CraftTodo.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CraftTodo
{
    /// <summary>
    /// 画面の左（HUD の左上の枠の下）に「制作メニュー」を出す。ゲームの UI の根の下に自分の Canvas を置くので、
    /// UI の倍率と「UI を隠す」演出（カットシーンなど）はゲームの UI と揃う。
    /// HUD が隠れている間（メニュー・カットシーン・建物の設置中・戦闘）は出さない。
    ///
    /// 形（0.5.3。docs/mockup-craft-menu-0.5.png。板の幅は 210 に固定、制作機の地の中の余白は左右 4・上 3・下 5）:
    ///   本体の板（建物の窓の枠と石板の地。PanelSkin）
    ///     帯（木のタブ帯に「制作メニュー」。人物の窓の中の所持品の窓と同じく、枠の上・左右から 11 内側に置く）
    ///     制作機ごとの区切り（所持品の窓の見出し板に制作機の名前だけ）＋ その下の茶色の地
    ///       料理ごとの札（技術ツリーの札の足を外したもの。名札に「料理 ×回数」と ×、中に「[作る物] = [素材] + [素材] …」の 32 のセル）
    ///   そろった料理は緑の名札の帯だけにしてその制作機の一番下へ、全部そろった制作機は一番下へ回す（TodoOverlay.Plates.cs）。
    /// 札が引けないとき（または GameFrame を切ったとき）は 0.4.4 の形に戻す:
    ///     1 件ごと: 見出し板「制作物 ×回数  制作機」＋ その下の茶色の地にセル（所持品の窓の InventoryHeaderWidget と Grid と同じ重ね方）。
    ///     そろった件は見出し板だけにして（緑の文字）、一番下へ回す。
    /// ドット絵がにじまないよう、並べ方は自分で計算して整数の位置に置く（絵は等倍、縁のある絵だけ Sliced で伸ばす）。
    /// 部品の絵は MenuParts と PlateParts、板は PanelSkin、文字は LabelStyler がゲームから写す。
    ///
    /// 置き場所は HUD の左上の枠と「？」ボタンの下。板は画面の下端までの高さに抑え、収まらない件は帯の下の Viewport
    /// （RectMask2D）の中でホイールでスクロールする（所持品の窓と同じくスクロールバーは無く、下に続きがあれば下端に影）。
    /// スクロールの位置も整数に丸める（ゲームの SmoothMouseWheelScroll は小数の位置で止まり、ドット絵がにじむので使わない）。
    /// </summary>
    internal sealed partial class TodoOverlay : MonoBehaviour
    {
        private const float RefreshInterval = 0.5f;

        // F9 で数え方を切り替えたとき、帯の文字を替えておく時間（2026-11-21 に本人が図で決めた）
        private const float NoticeSeconds = 2f;

        // HUD（50）より上、カットシーン（300）・ツールチップ（700）より下（GUIElements の定数）。
        // セルのツールチップ（ゲームの UITooltip）はこの上に出る（TooltipBridge が初めて出したときにログで確かめる）
        internal const int SortingOrder = 55;

        // 寸法（UI の単位 = 絵の 1 画素）。枠からの内側は人物の窓の中の所持品の窓（MultiInventoryWidget/Frame）に合わせる:
        // 帯（HeaderGroup）は上・左右から 11、中身（BackMask・Inventory Scroll）は 12
        private const int SideInset = 12;
        private const int BottomInset = 12;
        private const int StripInset = 11;
        private const int StripGap = 4;
        private const int EntryGap = 6;
        private const int GroundPadTop = 3;
        private const int GroundPadBottom = 5;
        private const int GroundPadSide = 4;
        private const int HeaderOverlap = 1;
        private const int OpGap = 4;
        // 見出しの文字と、両端の飾り・× の間。× の箱は文字の幅＋これ（押せる幅にもなる）
        private const int TitleGap = 2;
        private const int CloseBoxPad = 2;

        // 見出しの飾り（comm-header_2-dec_side_1、22x26）の見えている模様は端から 19 列目まで（2026-11-21 に画素で測った）
        private const int HeaderDecoVisible = 19;
        private const int MinInnerWidth = 160;

        // 上の帯の絵 main_window-header_1 の幅と、細くした縁（左・下・右・上。2026-11-21 に列を数えた）
        private const int StripSourceWidth = 228;
        private static readonly Vector4 StripThinBorder = new Vector4(94f, 0f, 94f, 0f);

        // 所持品のセルの数は地の端から 6・6 内側（所持品のセルは 42 のマスに 44 の地を置き、数をマスの右下から 5・5 内側に置く）
        private const int InventoryCountInset = 6;
        private const int DefaultBarHeight = 26;
        private const int DefaultCellSize = 44;
        private const float MaxRetryDelay = 60f;

        // 板の下端と画面の下端の間（UI の単位）。画面が小さくても中身の窓はこれより低くしない
        private const int BottomMargin = 8;

        // 見えている端どうしの間を OffsetY にするための、絵の透明な行（2026-11-21 に画素で測った）。
        // 「？」の絵（hud-tutorial_button_bg、44x44）は下 10 行が薄い影で、本体は 34 行目まで。
        // 板の枠の絵（comm-frame_1-border）は上 4 行が透明で、5 行目から角飾り
        private const int TutorialShadowBottom = 10;
        private const int FrameClearTop = 4;
        private const int MinViewportHeight = 60;

        // ホイールの 1 目盛りで動く量（ゲームの所持品の窓と同じく 1 マス分）と、追いつくまでの時間の目安
        private const float ScrollStep = 42f;
        private const float MaxScrollNotches = 3f;
        private const float ScrollSmoothTime = 0.06f;

        private static readonly Color DoneColor = new Color32(151, 219, 64, 255);
        private static readonly Color MissingColor = new Color32(240, 86, 60, 255);
        private static readonly Color TimesColor = new Color32(255, 195, 0, 255);
        private static readonly Color DimColor = new Color32(190, 165, 135, 255);
        private static readonly Color NoticeColor = new Color32(255, 214, 92, 255);

        // ゲームの絵が引けなかった部品の単色
        private static readonly Color PlainHeader = new Color(0.33f, 0.22f, 0.15f, 0.95f);
        private static readonly Color PlainGround = new Color(0.22f, 0.16f, 0.13f, 0.92f);
        private static readonly Color PlainCell = new Color(0f, 0f, 0f, 0.28f);
        private static readonly Color PlainStrip = new Color(0.45f, 0.33f, 0.2f, 0.95f);

        private static readonly Vector3[] Corners = new Vector3[4];

        private readonly List<EntryView> views = new List<EntryView>();
        private readonly List<Image> backTiles = new List<Image>();
        private GameObject root;
        private RectTransform panel;
        private RectTransform body;
        private Image bodyImage;
        private RectTransform backMask;
        private RectTransform viewport;
        private RectTransform content;
        private ScrollArea scrollArea;
        private Image fade;
        private int fadeHeight;
        private int contentHeight;
        private int maxBodyHeight = int.MaxValue;
        private int scrollMax;
        private float scrollTarget;
        private float scrollCurrent;
        private int appliedScroll = -1;
        private StripView strip;
        private TMP_Text inlineTitle;
        private int inlineTitleHeight;
        private int bodyWidth;
        private int innerWidth;
        private float referencePixelsPerUnit = 50f;
        private PanelSkin skin;
        private MenuParts parts;
        private PlateParts plates;
        private bool usePlates;
        private bool lookUsesGameFrame;
        private string loggedLook;
        private bool loggedShapes;
        private LabelStyler styler;
        private HUD hud;
        private bool dirty = true;
        private bool failed;
        private string builtLang;
        private float nextRefresh;
        private float nextRootSearch;
        private float nextHudSearch;
        private float nextStyleRetry;
        private float retryDelay = 5f;
        private string inlineKey;
        private string noticeText;
        private float noticeUntil;
        private string noticeShown;
        private TMP_Text noticeLabel;

        private void Start()
        {
            Plugin plugin = Plugin.Instance;
            plugin.Todos.Changed += MarkDirty;
            // 帯の右端のキーは組み立てのときに書くので、キーを変えたら組み直す。数え方を変えたらすぐ数え直す
            plugin.ToggleKey.SettingChanged += OnLookSettingChanged;
            plugin.StackMaterials.SettingChanged += OnCountSettingChanged;
        }

        private void OnDestroy()
        {
            if (Plugin.Instance != null)
            {
                Plugin.Instance.Todos.Changed -= MarkDirty;
                Plugin.Instance.ToggleKey.SettingChanged -= OnLookSettingChanged;
                Plugin.Instance.StackMaterials.SettingChanged -= OnCountSettingChanged;
            }

            if (root != null)
            {
                Destroy(root);
            }
        }

        private void MarkDirty() => dirty = true;

        private void OnLookSettingChanged(object sender, EventArgs e) => dirty = true;

        private void OnCountSettingChanged(object sender, EventArgs e) => nextRefresh = 0f;

        /// <summary>帯の文字を <see cref="NoticeSeconds"/> 秒だけ <paramref name="text"/> に替える（F9 で数え方を切り替えたとき）。</summary>
        private void ShowNotice(string text)
        {
            noticeText = text;
            noticeUntil = Time.unscaledTime + NoticeSeconds;
        }

        /// <summary>帯（帯が無ければ板の中の見出し）に知らせを出す・元に戻す。組み直したあと（見出しが新しくなった）も残りの時間は出し続ける。</summary>
        private void UpdateNotice()
        {
            if (noticeText != null && Time.unscaledTime >= noticeUntil)
            {
                noticeText = null;
            }

            TMP_Text label = strip != null ? strip.Title : inlineTitle;
            if (label == null || (label == noticeLabel && noticeText == noticeShown))
            {
                return;
            }

            string want = noticeText;
            if (strip != null)
            {
                strip.Title.text = want ?? Strings.Header;
                strip.Title.color = want != null ? NoticeColor : strip.TitleColor;
                if (strip.Key != null)
                {
                    strip.Key.gameObject.SetActive(want == null);
                }
            }
            else
            {
                inlineTitle.text = want ?? Strings.HeaderWithKey(inlineKey);
                inlineTitle.color = want != null ? NoticeColor : DimColor;
            }

            noticeShown = want;
            noticeLabel = label;
        }

        private void Update()
        {
            if (failed)
            {
                return;
            }

            try
            {
                Tick();
            }
            catch (Exception e)
            {
                // 表示が壊れても遊びは止めない。メモの記録（Patches）はそのまま続く
                failed = true;
                Plugin.Log.LogError($"制作メニューの表示で失敗したので、表示を止める: {e}");
                if (root != null)
                {
                    root.SetActive(false);
                }
            }
        }

        private void Tick()
        {
            Plugin plugin = Plugin.Instance;
            bool inGame = GameBridge.CurrentSlotName() != null;
            if (inGame)
            {
                HandleKeys(plugin);
            }

            bool show = plugin.Visible.Value && inGame && plugin.Todos.Count > 0 && IsHudShown();
            if (!show)
            {
                if (root != null && root.activeSelf)
                {
                    root.SetActive(false);
                }

                return;
            }

            if (!EnsureRoot())
            {
                return;
            }

            if (!root.activeSelf)
            {
                root.SetActive(true);
            }

            string lang = GameBridge.CurrentLang();
            // 所持品の窓の絵・文字の形が引けていなければ、ときどき引き直す（引けないままなら間隔を延ばす）
            bool retryLook = Time.unscaledTime >= nextStyleRetry && !LookComplete(plugin);
            if (dirty || lang != builtLang || retryLook || lookUsesGameFrame != plugin.GameFrame.Value)
            {
                if (retryLook)
                {
                    retryDelay = Mathf.Min(MaxRetryDelay, retryDelay * 2f);
                }

                Rebuild(plugin);
                builtLang = lang;
                dirty = false;
                nextRefresh = 0f;
            }

            if (Time.unscaledTime >= nextRefresh)
            {
                RefreshCounts(plugin);
                Reposition(plugin);
                nextRefresh = Time.unscaledTime + RefreshInterval;
            }

            UpdateNotice();
            UpdateScroll();
        }

        private bool LookComplete(Plugin plugin)
        {
            if (styler == null || skin == null || parts == null)
            {
                return false;
            }

            return styler.UsesGameStyles && (!plugin.GameFrame.Value || (skin.FromGame && parts.Complete && plates != null && plates.Complete));
        }

        private void HandleKeys(Plugin plugin)
        {
            // 設定の窓を開いている間（キーを選んでいる間も）は、ここのキーを鳴らさない
            if (SettingsWindow.BlocksHotkeys)
            {
                return;
            }

            if (plugin.StackKey.Value.IsDown())
            {
                bool on = plugin.ToggleStacking();
                // 右下のゲームの知らせで出す（本人の希望）。出せなければ帯の文字を 2 秒替える
                if (!GameNotice.TryShow("crafttodo_stacking", on ? Strings.StackingNoticeOn : Strings.StackingNoticeOff))
                {
                    ShowNotice(on ? Strings.StackingOn : Strings.StackingOff);
                }

                GameBridge.PlayClick();
            }

            // BepInEx のショートカットは、指定していないキーが押されている間は鳴らない
            // （素のホイールは Shift を押している間は鳴らない）。それでも同じ設定にされたときのために、減らす方を先に見る
            if (plugin.RemoveKey.Value.IsDown())
            {
                TodoEntry picked = TodoPicker.PickUnderPointer();
                if (picked != null && plugin.UnregisterTodo(picked))
                {
                    GameBridge.PlayClick();
                }
            }
            else if (plugin.RegisterKey.Value.IsDown())
            {
                TodoEntry picked = TodoPicker.PickUnderPointer();
                if (picked != null)
                {
                    plugin.RegisterTodo(picked);
                    GameBridge.PlayClick();
                }
            }

            if (plugin.ClearKey.Value.IsDown())
            {
                plugin.ClearTodos("キー");
            }
            else if (plugin.ToggleKey.Value.IsDown())
            {
                plugin.Visible.Value = !plugin.Visible.Value;
            }
        }

        private bool IsHudShown()
        {
            if (hud == null && Time.unscaledTime >= nextHudSearch)
            {
                hud = GameBridge.FindHud();
                nextHudSearch = Time.unscaledTime + 1f;
            }

            return hud != null && hud.isActiveAndEnabled && hud.Mode == HUDMode.Common;
        }

        private bool EnsureRoot()
        {
            if (root != null)
            {
                return true;
            }

            if (Time.unscaledTime < nextRootSearch)
            {
                return false;
            }

            nextRootSearch = Time.unscaledTime + 1f;
            Transform parent = GameBridge.FindUiRoot();
            if (parent == null)
            {
                return false;
            }

            RectTransform rootRect = UiFactory.CreateRect("CraftTodo.Overlay", parent);
            root = rootRect.gameObject;
            UiFactory.Stretch(rootRect, 0f, 0f, 0f, 0f);
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;
            // Sliced の縁の太さはこの値で決まる。ゲームの UI は 1 単位 = 絵の 1 画素（sprite の ppu 50）なので、根の Canvas に揃える
            Canvas parentCanvas = parent.GetComponentInParent<Canvas>();
            if (parentCanvas != null && parentCanvas.rootCanvas != null)
            {
                canvas.referencePixelsPerUnit = parentCanvas.rootCanvas.referencePixelsPerUnit;
            }

            referencePixelsPerUnit = canvas.referencePixelsPerUnit;
            root.AddComponent<GraphicRaycaster>();

            panel = UiFactory.CreateRect("Panel", rootRect);
            UiFactory.Place(panel, 0f, 0f, 0f, 0f);

            dirty = true;
            Plugin.Log.LogInfo($"制作メニューの表示を作った（親: {parent.name}、基準の画素/単位 {canvas.referencePixelsPerUnit}）");
            return true;
        }

        // ---- 組み立て ----

        private void Rebuild(Plugin plugin)
        {
            for (int i = panel.childCount - 1; i >= 0; i--)
            {
                GameObject child = panel.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }

            views.Clear();
            groups.Clear();
            backTiles.Clear();
            strip = null;
            inlineTitle = null;
            fade = null;
            appliedScroll = -1;
            if (!loggedShapes)
            {
                loggedShapes = true;
                Plugin.Log.LogInfo(GameBridge.DescribeRecipeShapes());
                TooltipBridge.Prepare();
            }

            ResolveLook(plugin);
            nextStyleRetry = Time.unscaledTime + retryDelay;

            body = UiFactory.CreateRect("Body", panel);
            bodyImage = body.gameObject.AddComponent<Image>();
            bodyImage.raycastTarget = false;
            // 地: 枠の内側のマスクの下に、絵を等倍で敷く（絵は板の高さが決まってから Layout で並べる）
            backMask = UiFactory.CreateRect("BackMask", body);
            backMask.gameObject.AddComponent<RectMask2D>();
            UiFactory.Stretch(backMask, 0f, 0f, 0f, 0f);
            skin.Apply(bodyImage, backMask);

            string key = plugin.ToggleKey.Value.MainKey == KeyCode.None ? null : Strings.KeyName(plugin.ToggleKey.Value);
            inlineKey = key;
            // 札は枠の絵と同じくゲームの絵を使う設定のときだけ（札が引けなければ 0.4.4 の形）
            usePlates = plugin.GameFrame.Value && skin.FromGame && plates != null && plates.Usable;
            int minInner = usePlates ? PlateParts.SourceWidth : MinInnerWidth;
            int inner = minInner;
            if (parts.HasStrip)
            {
                strip = CreateStrip(key);
            }
            else
            {
                inlineTitle = UiFactory.CreateLabel(body, "Header", styler, TextRole.Header, TextAlignmentOptions.Left);
                inlineTitle.text = Strings.HeaderWithKey(key);
                inlineTitle.color = DimColor;
                inner = Mathf.Max(inner, UiFactory.TextWidth(inlineTitle, inlineTitle.text));
                inlineTitleHeight = Mathf.CeilToInt(inlineTitle.GetPreferredValues(inlineTitle.text).y);
            }

            // 中身の窓: 帯の下から板の下の余白の上まで。件は Content に並べ、あふれたら Content を上下に動かす
            viewport = UiFactory.CreateRect("Viewport", body);
            viewport.gameObject.AddComponent<RectMask2D>();
            scrollArea = viewport.gameObject.AddComponent<ScrollArea>();
            scrollArea.raycastTarget = false;
            scrollArea.Scrolled = OnScrolled;
            content = UiFactory.CreateRect("Content", viewport);
            if (parts.Fade != null)
            {
                fade = UiFactory.CreateImage("FadeBot", body, parts.Fade, Color.clear);
                fadeHeight = (int)UiFactory.NativeSize(parts.Fade, referencePixelsPerUnit).y;
            }

            string widthNote;
            if (usePlates)
            {
                inner = Mathf.Max(inner, CreateGroups(plugin, out widthNote));
            }
            else
            {
                int opWidth = 0;
                int widestTitle = 0;
                int widestRow = 0;
                string widestTitleKey = "-";
                foreach (TodoEntry entry in plugin.Todos.Entries)
                {
                    EntryView view = CreateEntryView(entry, ref opWidth);
                    views.Add(view);
                    inner = Mathf.Max(inner, view.RowWidth + GroundPadSide * 2, view.TitleNeed);
                    widestRow = Mathf.Max(widestRow, view.RowWidth + GroundPadSide * 2);
                    if (view.TitleNeed > widestTitle)
                    {
                        widestTitle = view.TitleNeed;
                        widestTitleKey = entry.Key;
                    }
                }

                widthNote = $"見出し {widestTitle}（{widestTitleKey}） / セルの段 {widestRow}";
            }

            int width = inner + SideInset * 2;
            if (strip != null)
            {
                width = Mathf.Max(width, strip.MinWidth + StripInset * 2);
            }

            if (skin.FromGame && skin.Frame != null)
            {
                // 枠の角飾り（Sliced の左右の縁）が潰れない幅
                width = Mathf.Max(width, Mathf.CeilToInt(skin.Frame.border.x + skin.Frame.border.z));
            }

            // 偶数にそろえる（帯の文字を中央に置いたとき半端な位置にしないため）
            if (width % 2 == 1)
            {
                width++;
            }

            if (width != bodyWidth)
            {
                // 幅を詰めたいと言われたときの手がかり（どれで決まったか）。幅が変わったときだけ出す
                Plugin.Log.LogInfo($"制作メニューの幅 {width}（内側 {width - SideInset * 2}）: {widthNote}"
                    + $" / 帯 {(strip != null ? strip.MinWidth + StripInset * 2 - SideInset * 2 : 0)} / 最小 {minInner}"
                    + (skin.FromGame && skin.Frame != null ? $" / 枠の角飾り {Mathf.CeilToInt(skin.Frame.border.x + skin.Frame.border.z) - SideInset * 2}" : string.Empty));
            }

            bodyWidth = width;
            innerWidth = width - SideInset * 2;
            foreach (EntryView view in views)
            {
                if (view.IsPlate)
                {
                    SizePlate(view);
                }
                else
                {
                    SizeEntry(view);
                }
            }

            foreach (GroupView group in groups)
            {
                SizeGroup(group);
            }

            if (strip != null)
            {
                SizeStrip(strip, bodyWidth);
            }

            Layout();
        }

        /// <summary>板・部品の絵・文字の形を決め直す（ゲームの絵を使う設定なら写す。写せなかった部品は単色）。</summary>
        private void ResolveLook(Plugin plugin)
        {
            bool wantGame = plugin.GameFrame.Value;
            if (skin == null || lookUsesGameFrame != wantGame || (wantGame && !skin.FromGame))
            {
                skin = wantGame ? PanelSkin.Resolve() : PanelSkin.Plain();
            }

            // 文字の形は単色の板でも所持品の窓から写すので、絵を使わない設定でも部品は引く
            MenuParts gameParts = null;
            if (parts == null || lookUsesGameFrame != wantGame || (wantGame && !parts.Complete))
            {
                gameParts = MenuParts.Resolve();
                parts = wantGame ? gameParts : MenuParts.Plain();
            }

            // 札は枠の絵と同じく、ゲームの絵を使う設定のときだけ引く
            if (!wantGame)
            {
                plates = null;
            }
            else if (plates == null || !plates.Complete)
            {
                plates = PlateParts.Resolve();
            }

            lookUsesGameFrame = wantGame;
            if (styler == null || !styler.UsesGameStyles)
            {
                styler = LabelStyler.Resolve(hud, gameParts ?? (wantGame ? parts : MenuParts.Resolve()));
            }

            string plateLook = plates == null ? "使わない" : plates.Description;
            string look = skin.Description + " / " + parts.Description + " / " + plateLook + " / " + styler.Description;
            if (loggedLook != look)
            {
                loggedLook = look;
                Plugin.Log.LogInfo($"制作メニューの見た目: 板 {skin.Description} / 部品 {parts.Description} / 札 {plateLook} / 文字 {styler.Description}");
                if (wantGame && !skin.FromGame)
                {
                    LogFrameCandidates();
                }
            }

            if (LookComplete(plugin))
            {
                retryDelay = 5f;
            }
        }

        private void LogFrameCandidates()
        {
            UIBuildingWindow buildingWindow = null;
            foreach (UIBuildingWindow w in Resources.FindObjectsOfTypeAll<UIBuildingWindow>())
            {
                buildingWindow = w;
                break;
            }

            UITooltip tooltip = null;
            foreach (UITooltip t in Resources.FindObjectsOfTypeAll<UITooltip>())
            {
                tooltip = t;
                break;
            }

            UiDiagnostics.LogFrameCandidates(
                ("建物の窓", buildingWindow),
                ("HUD 左上", GameBridge.HudLeftGroup(hud)),
                ("地名", GUIElements.Instance == null ? null : GUIElements.Instance.WorldZoneWidget),
                ("ツールチップ", tooltip));
        }

        private int BarHeight => parts.HeaderBar != null ? (int)UiFactory.NativeSize(parts.HeaderBar, referencePixelsPerUnit).y : DefaultBarHeight;

        private int CellSize => parts.CellBack != null ? (int)UiFactory.NativeSize(parts.CellBack, referencePixelsPerUnit).y : DefaultCellSize;

        /// <summary>
        /// 帯: 人物の窓の中の所持品の窓（MultiInventoryWidget/Frame/HeaderGroup）と同じ形。木の帯を Sliced で伸ばし、
        /// 両端に飾り（右は左右反転）、文字は中央。右端に表示・非表示のキーを小さく書く。
        /// </summary>
        private StripView CreateStrip(string key)
        {
            var view = new StripView();
            view.Rect = UiFactory.CreateRect("Strip", body);
            Image back = view.Rect.gameObject.AddComponent<Image>();
            back.raycastTarget = false;
            // 帯の絵（228x26、縁 L96 R96）は列 94〜133 が同じ列の繰り返し。縁を 94 にした写しなら幅 188（板 210 の内側）まで縁が潰れない
            Sprite stripSprite = parts.Strip;
            if (stripSprite != null && Mathf.Abs(stripSprite.rect.width - StripSourceWidth) < 0.5f)
            {
                stripSprite = PlateParts.WithBorder(stripSprite, StripThinBorder) ?? stripSprite;
            }

            UiFactory.SetSprite(back, stripSprite, PlainStrip);
            view.Height = (int)UiFactory.NativeSize(parts.Strip, referencePixelsPerUnit).y;

            if (parts.StripCorner != null)
            {
                view.CornerLeft = UiFactory.CreateImage("CornerLeft", view.Rect, parts.StripCorner, Color.clear).rectTransform;
                view.CornerRight = UiFactory.CreateImage("CornerRight", view.Rect, parts.StripCorner, Color.clear).rectTransform;
                view.CornerSize = UiFactory.NativeSize(parts.StripCorner, referencePixelsPerUnit);
            }

            view.Title = UiFactory.CreateLabel(view.Rect, "Title", styler, TextRole.Header, TextAlignmentOptions.Center);
            view.Title.text = Strings.Header;
            view.TitleColor = view.Title.color;
            int titleWidth = UiFactory.TextWidth(view.Title, view.Title.text);

            if (!string.IsNullOrEmpty(key))
            {
                view.Key = UiFactory.CreateLabel(view.Rect, "Key", styler, TextRole.Count, TextAlignmentOptions.Right);
                view.Key.text = key;
                view.Key.color = DimColor;
                view.KeyWidth = UiFactory.TextWidth(view.Key, key);
            }

            // 文字は帯の中央、キーは右の飾りから 4 内側に右揃え。文字とキーの間を 4 空ける（keySide に含む。0.5.0 までは更に 4 ずつ空けていた）
            int cornerSide = view.CornerLeft != null ? (int)view.CornerSize.x : 0;
            int keySide = view.KeyWidth > 0 ? view.KeyWidth + 8 : 4;
            view.MinWidth = titleWidth + (cornerSide + keySide) * 2;
            if (stripSprite != null)
            {
                // 帯の絵の左右の縁（Sliced）が潰れない幅
                view.MinWidth = Mathf.Max(view.MinWidth, Mathf.CeilToInt(stripSprite.border.x + stripSprite.border.z));
            }

            return view;
        }

        private void SizeStrip(StripView view, int bodyWidthValue)
        {
            int width = bodyWidthValue - StripInset * 2;
            UiFactory.Place(view.Rect, StripInset, StripInset, width, view.Height);
            UiFactory.Stretch(view.Title.rectTransform, 0f, 0f, 0f, 0f);
            if (view.CornerLeft != null)
            {
                UiFactory.Place(view.CornerLeft, 0f, 0f, view.CornerSize.x, view.CornerSize.y);
                UiFactory.Place(view.CornerRight, width - view.CornerSize.x, 0f, view.CornerSize.x, view.CornerSize.y, mirror: true);
            }

            if (view.Key != null)
            {
                float right = width - (view.CornerLeft != null ? view.CornerSize.x : 0f) - 4f;
                UiFactory.Place(view.Key.rectTransform, right - view.KeyWidth - 4f, 0f, view.KeyWidth + 4f, view.Height);
            }
        }

        private EntryView CreateEntryView(TodoEntry entry, ref int opWidth)
        {
            var view = new EntryView { Entry = entry };
            view.Root = UiFactory.CreateRect("Entry", content);

            // 茶色の地を先に作り、見出し板を後に作る（見出し板を地に 1 重ねて上に描く。所持品の窓と同じ）
            view.Ground = UiFactory.CreateImage("Ground", view.Root, parts.Ground, PlainGround).rectTransform;
            view.Row = UiFactory.CreateRect("Row", view.Ground);
            int x = 0;
            view.ProductCount = CreateCell(view.Row, x, GameBridge.Sprite(entry.IconId), entry, -1);
            view.ProductCount.text = Strings.Multiplier(entry.Multiplier);
            view.ProductCount.color = TimesColor;
            x += CellSize;

            for (int i = 0; i < entry.Materials.Count; i++)
            {
                TMP_Text op = UiFactory.CreateLabel(view.Row, "Op", styler, TextRole.Header, TextAlignmentOptions.Center);
                op.text = i == 0 ? Strings.EqualsSign : Strings.PlusSign;
                if (opWidth == 0)
                {
                    opWidth = Mathf.Max(UiFactory.TextWidth(op, Strings.EqualsSign), UiFactory.TextWidth(op, Strings.PlusSign), 4);
                }

                x += OpGap;
                UiFactory.Place(op.rectTransform, x, 0f, opWidth, CellSize);
                x += opWidth + OpGap;

                TodoMaterial material = entry.Materials[i];
                view.Materials.Add(CreateCell(view.Row, x, GameBridge.MaterialIcon(material), entry, i));
                x += CellSize;
            }

            view.RowWidth = x;

            view.Header = UiFactory.CreateImage("Header", view.Root, parts.HeaderBar, PlainHeader).rectTransform;
            if (parts.HeaderDeco != null)
            {
                view.DecoLeft = UiFactory.CreateImage("DecoLeft", view.Header, parts.HeaderDeco, Color.clear).rectTransform;
                view.DecoRight = UiFactory.CreateImage("DecoRight", view.Header, parts.HeaderDeco, Color.clear).rectTransform;
                view.DecoSize = UiFactory.NativeSize(parts.HeaderDeco, referencePixelsPerUnit);
            }

            view.Title = UiFactory.CreateLabel(view.Header, "Title", styler, TextRole.Header, TextAlignmentOptions.Center);
            view.Title.text = Strings.Title(GameBridge.RecipeName(entry), entry.Multiplier, GameBridge.StationName(entry));
            view.TitleColor = view.Title.color;

            view.Close = UiFactory.CreateLabel(view.Header, "Remove", styler, TextRole.Header, TextAlignmentOptions.Center);
            view.Close.text = Strings.Close;
            view.Close.color = DimColor;
            view.Close.raycastTarget = true;
            view.CloseWidth = UiFactory.TextWidth(view.Close, view.Close.text) + CloseBoxPad;

            // 文字は「左の飾り〜× の手前」の中で中央に置く（ゲームの枠の中の見出しも、右の閉じるボタンの分を空けて中央に置いている）。
            // 幅はこの見出しで決まることが多いので、飾りの絵の幅ではなく見えている模様の幅だけ空ける（0.4.3 までより 12 狭い）。
            // 文字は折り返さない（LabelStyler）ので、余りを持たなくても 2 行にはならない
            int deco = DecoReserve(view);
            view.TitleLeft = deco + TitleGap;
            view.TitleRight = deco + view.CloseWidth + TitleGap;
            view.TitleNeed = UiFactory.TextWidth(view.Title, view.Title.text) + view.TitleLeft + view.TitleRight;
            Button button = view.Close.gameObject.AddComponent<Button>();
            button.targetGraphic = view.Close;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            string key = entry.Key;
            button.onClick.AddListener(() => Plugin.Instance.RemoveTodo(key));
            return view;
        }

        /// <summary>見出し板の両端で、飾りのために空ける幅（飾りの見えている模様＋1。飾りが無ければ 4）。</summary>
        private static int DecoReserve(EntryView view)
        {
            return view.DecoLeft != null ? Mathf.Min(HeaderDecoVisible + 1, (int)view.DecoSize.x) : 4;
        }

        /// <summary>
        /// 所持品のセルと同じ形: 44x44 の地、アイコンは等倍で中央、数は右下（所持品のセルは 42 のマスに 44 の地を置き、
        /// 数をマスの右下から 5・5 内側に置く → 地の端からは 6・6）。右上揃えの 44x10 の枠。
        /// </summary>
        private TMP_Text CreateCell(RectTransform row, int x, Sprite icon, TodoEntry entry, int materialIndex)
        {
            return CreateCell(row, x, icon, parts.CellBack, CellSize, InventoryCountInset, parts.IconMaterial, parts.IconOutline, entry, materialIndex, null);
        }

        /// <summary>
        /// セル: <paramref name="size"/> 角の地、アイコンは等倍で中央、数は右下から <paramref name="countInset"/> 内側。
        /// 地は当たる面にして、乗ったらゲームのツールチップを出す（<paramref name="materialIndex"/> が負なら作る物）。
        /// 左クリックは <paramref name="clicked"/> へ（札の出し入れ。セルが当たると札の当たる面には届かないため）。
        /// </summary>
        private TMP_Text CreateCell(RectTransform row, int x, Sprite icon, Sprite cellBack, int size, int countInset, Material iconMaterial, Color iconOutline,
            TodoEntry entry, int materialIndex, Action clicked)
        {
            Image back = UiFactory.CreateImage("Cell", row, cellBack, PlainCell);
            RectTransform cell = back.rectTransform;
            UiFactory.Place(cell, x, 0f, size, size);
            back.raycastTarget = true;
            CellTooltip tooltip = back.gameObject.AddComponent<CellTooltip>();
            tooltip.Entry = entry;
            tooltip.MaterialIndex = materialIndex;
            tooltip.Clicked = clicked;

            if (icon != null)
            {
                Image image = UiFactory.CreateImage("Icon", cell, icon, Color.clear);
                Vector2 native = UiFactory.NativeSize(icon, referencePixelsPerUnit);
                // 奇数の大きさの絵でも画素の境目に乗るよう、左上を整数に置く
                UiFactory.Place(image.rectTransform, Mathf.Floor((size - native.x) / 2f), Mathf.Floor((size - native.y) / 2f), native.x, native.y);
                if (iconMaterial != null)
                {
                    // ゲームのアイコンは純粋な青の縁を持ち、専用の材質でセルの色に置き換えて描く（UIItemCell と同じ呼び方）
                    image.material = iconMaterial;
                    image.BlueColorReplace(iconOutline);
                }
            }

            TMP_Text count = UiFactory.CreateLabel(cell, "Count", styler, TextRole.Count, TextAlignmentOptions.TopRight);
            RectTransform rect = count.rectTransform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-countInset, countInset);
            rect.sizeDelta = new Vector2(size, 10f);
            return count;
        }

        private void SizeEntry(EntryView view)
        {
            int bar = BarHeight;
            int cell = CellSize;
            UiFactory.Place(view.Header, 0f, 0f, innerWidth, bar);
            if (view.DecoLeft != null)
            {
                UiFactory.Place(view.DecoLeft, 0f, 0f, view.DecoSize.x, view.DecoSize.y);
                UiFactory.Place(view.DecoRight, innerWidth - view.DecoSize.x, 0f, view.DecoSize.x, view.DecoSize.y, mirror: true);
            }

            UiFactory.Stretch(view.Title.rectTransform, view.TitleLeft, 0f, view.TitleRight, 0f);
            UiFactory.Place(view.Close.rectTransform, innerWidth - DecoReserve(view) - view.CloseWidth, 0f, view.CloseWidth, bar);

            int groundHeight = GroundPadTop + cell + GroundPadBottom;
            UiFactory.Place(view.Ground, 0f, bar - HeaderOverlap, innerWidth, groundHeight);
            UiFactory.Place(view.Row, (innerWidth - view.RowWidth) / 2, GroundPadTop, view.RowWidth, cell);
            view.FullHeight = bar - HeaderOverlap + groundHeight;
            view.CollapsedHeight = bar;
        }

        /// <summary>
        /// 縦に並べる。そろっていない件を元の順に、そろった件（見出し板だけ）をその下に。
        /// 板は画面の下端まで（<see cref="maxBodyHeight"/>）に抑え、収まらなければ中身の窓の中でスクロールさせる。
        /// </summary>
        private void Layout()
        {
            int top = strip != null ? StripInset + strip.Height + StripGap : SideInset;
            if (inlineTitle != null)
            {
                UiFactory.Place(inlineTitle.rectTransform, SideInset, top, innerWidth, inlineTitleHeight);
                top += inlineTitleHeight + StripGap;
            }

            contentHeight = usePlates ? StackGroups() : StackEntries();
            int limit = maxBodyHeight == int.MaxValue ? int.MaxValue : Mathf.Max(MinViewportHeight, maxBodyHeight - top - BottomInset);
            bool scrolls = contentHeight > limit;
            int viewHeight = scrolls ? limit : contentHeight;

            int bodyHeight = top + viewHeight + BottomInset;
            if (skin.FromGame && skin.Frame != null)
            {
                // 枠の角飾り（Sliced の上下の縁）が潰れない高さ
                bodyHeight = Mathf.Max(bodyHeight, Mathf.CeilToInt(skin.Frame.border.y + skin.Frame.border.w));
            }

            UiFactory.Place(body, 0f, 0f, bodyWidth, bodyHeight);
            panel.sizeDelta = new Vector2(bodyWidth, bodyHeight);
            skin.LayBack(backMask, backTiles, bodyHeight, referencePixelsPerUnit);

            UiFactory.Place(viewport, SideInset, top, innerWidth, viewHeight);
            // あふれていないときはホイールを受けない（板の上でゲームのクリックを止める面を増やさない）
            scrollArea.raycastTarget = scrolls;
            scrollMax = scrolls ? contentHeight - viewHeight : 0;
            scrollTarget = Mathf.Clamp(scrollTarget, 0f, scrollMax);
            scrollCurrent = Mathf.Clamp(scrollCurrent, 0f, scrollMax);
            if (fade != null)
            {
                // 所持品の窓の FadeBot と同じく、地の幅いっぱい・中身の窓の下端に重ねる
                float left = skin.FromGame ? skin.BackInset.x : SideInset;
                float width = skin.FromGame ? bodyWidth - skin.BackInset.x - skin.BackInset.z : innerWidth;
                UiFactory.Place(fade.rectTransform, left, top + viewHeight - fadeHeight, width, fadeHeight);
            }

            appliedScroll = -1;
            ApplyScroll();
        }

        /// <summary>0.4.4 の形: そろっていない件を元の順に、そろった件（見出し板だけ）をその下に積む。積んだ高さを返す。</summary>
        private int StackEntries()
        {
            int y = 0;
            bool first = true;
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (EntryView view in views)
                {
                    if (view.Ready != (pass == 1))
                    {
                        continue;
                    }

                    if (!first)
                    {
                        y += EntryGap;
                    }

                    first = false;
                    int height = view.Ready ? view.CollapsedHeight : view.FullHeight;
                    view.Ground.gameObject.SetActive(!view.Ready);
                    UiFactory.Place(view.Root, 0f, y, innerWidth, height);
                    y += height;
                }
            }

            return y;
        }

        private void OnScrolled(float notches)
        {
            if (scrollMax <= 0)
            {
                return;
            }

            // 上へ回すと正。1 度に大きな値が来ても（入力の作りによる）3 目盛りまでにする
            float amount = Mathf.Clamp(notches, -MaxScrollNotches, MaxScrollNotches);
            scrollTarget = Mathf.Clamp(scrollTarget - amount * ScrollStep, 0f, scrollMax);
        }

        /// <summary>スクロールの位置を目標へ寄せる（毎フレーム）。置く位置は整数に丸める。</summary>
        private void UpdateScroll()
        {
            if (content == null || scrollCurrent == scrollTarget)
            {
                return;
            }

            float t = 1f - Mathf.Exp(-Time.unscaledDeltaTime / ScrollSmoothTime);
            scrollCurrent = Mathf.Lerp(scrollCurrent, scrollTarget, t);
            if (Mathf.Abs(scrollTarget - scrollCurrent) < 0.5f)
            {
                scrollCurrent = scrollTarget;
            }

            ApplyScroll();
        }

        private void ApplyScroll()
        {
            int offset = Mathf.RoundToInt(scrollCurrent);
            if (offset == appliedScroll)
            {
                return;
            }

            appliedScroll = offset;
            UiFactory.Place(content, 0f, -offset, innerWidth, contentHeight);
            if (fade != null)
            {
                // 下に続きがあるときだけ影を出す
                fade.gameObject.SetActive(offset < scrollMax);
            }
        }

        private void RefreshCounts(Plugin plugin)
        {
            bool reorder = false;
            var entries = new List<TodoEntry>(views.Count);
            foreach (EntryView view in views)
            {
                entries.Add(view.Entry);
            }

            // 積み上げるときは、同じ素材を取り合う件の要る数を合計する（TodoProgress.EvaluateAll）
            List<List<MaterialProgress>> all = TodoProgress.EvaluateAll(entries, new GameStock(), plugin.StackMaterials.Value);
            for (int v = 0; v < views.Count; v++)
            {
                EntryView view = views[v];
                List<MaterialProgress> progress = all[v];
                for (int i = 0; i < progress.Count && i < view.Materials.Count; i++)
                {
                    MaterialProgress p = progress[i];
                    TMP_Text label = view.Materials[i];
                    SetText(label, p.Have + "/" + p.Need);
                    label.color = p.Done ? DoneColor : MissingColor;
                }

                bool ready = TodoProgress.IsReady(progress);
                if (!view.ReadyKnown || view.Ready != ready)
                {
                    bool changed = view.ReadyKnown;
                    view.Ready = ready;
                    view.ReadyKnown = true;
                    reorder = true;
                    if (view.IsPlate)
                    {
                        // そろった / そろわなくなったら、押して出し入れした状態は捨てて既定（そろったら畳む）に戻す
                        if (changed)
                        {
                            plateExpanded.Remove(view.Entry.Key);
                        }

                        // 札は金 ↔ 緑の絵と名札の文字の形を替える（名札の文字の色はどちらもクリーム色）
                        SkinPlate(view);
                    }
                }

                if (!view.IsPlate)
                {
                    view.Title.color = ready ? DoneColor : view.TitleColor;
                }
            }

            if (reorder)
            {
                Layout();
            }
        }

        private static void SetText(TMP_Text label, string text)
        {
            if (label.text != text)
            {
                label.text = text;
            }
        }

        /// <summary>
        /// HUD の左上の枠と「？」ボタン（チュートリアルの一覧）の、低い方の下端のすぐ下に置く（見えている端どうしの間が OffsetY。
        /// 「？」の下の影と枠の絵の上の透明な行は数えない）。「？」は操作できない間
        /// （窓を開いている間など）は消えるが、そのたびに板が上下しないよう、消えていても場所は空けておく。
        /// 枠の大きさがおかしい（画面の上 3%〜45% の外に下端がある）ときや、AutoPosition を切ったときは、画面の上から 17% の高さに置く。
        /// 置いた位置から画面の下端までが板の高さの上限になる（変わったら並べ直す）。
        /// </summary>
        private void Reposition(Plugin plugin)
        {
            var rootRect = (RectTransform)root.transform;
            Rect area = rootRect.rect;
            float x = plugin.OffsetX.Value;
            float y = -(area.height * 0.17f) - plugin.OffsetY.Value;

            if (plugin.AutoPosition.Value)
            {
                RectTransform group = GameBridge.HudLeftGroup(hud);
                if (group != null && group.gameObject.activeInHierarchy)
                {
                    Rect g = RectIn(rootRect, group);
                    float left = Mathf.Max(0f, g.xMin - area.xMin) + plugin.OffsetX.Value;
                    float bottom = g.yMin;
                    RectTransform tutorial = GameBridge.HudTutorialButton(hud);
                    if (tutorial != null)
                    {
                        Rect b = RectIn(rootRect, tutorial);
                        float panelLeft = area.xMin + left;
                        if (b.xMin < panelLeft + bodyWidth && b.xMax > panelLeft)
                        {
                            // 影は板の下に隠れてよい。見えている本体の下端に揃える
                            bottom = Mathf.Min(bottom, b.yMin + TutorialShadowBottom);
                        }
                    }

                    float top = bottom - area.yMax;
                    if (top < -area.height * 0.03f && top > -area.height * 0.45f)
                    {
                        x = left;
                        // 枠の絵の上の透明な行のぶん上げ、見えている枠の上端と上の部品の間を OffsetY にする
                        y = top - plugin.OffsetY.Value + (skin != null && skin.FromGame ? FrameClearTop : 0);
                    }
                }
            }

            x = Mathf.Round(x);
            y = Mathf.Round(y);
            panel.anchoredPosition = new Vector2(x, y);

            int available = Mathf.FloorToInt(area.height + y) - BottomMargin;
            if (available != maxBodyHeight)
            {
                maxBodyHeight = available;
                Layout();
            }
        }

        /// <summary><paramref name="target"/> の四隅を <paramref name="space"/> の中の座標で囲んだ矩形。</summary>
        private static Rect RectIn(RectTransform space, RectTransform target)
        {
            target.GetWorldCorners(Corners);
            Vector3 a = space.InverseTransformPoint(Corners[0]);
            Vector3 b = space.InverseTransformPoint(Corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        private sealed class StripView
        {
            public RectTransform Rect;
            public int Height;
            public TMP_Text Title;
            public Color TitleColor;
            public RectTransform CornerLeft;
            public RectTransform CornerRight;
            public Vector2 CornerSize;
            public TMP_Text Key;
            public int KeyWidth;
            public int MinWidth;
        }

        private sealed class EntryView
        {
            public TodoEntry Entry;

            /// <summary>料理ごとの札（0.5.0）か、0.4.4 の見出し板＋茶色の地か。</summary>
            public bool IsPlate;
            public RectTransform Root;

            /// <summary>札の絵の切れ端を並べる親（足を外した札・名札の帯だけ）。切れ端は札の幅が決まってから並べ、金 ↔ 緑は絵を差し替える。</summary>
            public RectTransform PlateFull;
            public RectTransform PlateBand;
            public readonly List<PlatePiece> Pieces = new List<PlatePiece>();

            /// <summary>名札の文字の省略する前の全文（札の幅が決まってから入る分だけ出す）。</summary>
            public string FullTitle;
            public RectTransform Header;
            public RectTransform DecoLeft;
            public RectTransform DecoRight;
            public Vector2 DecoSize;
            public TMP_Text Title;
            public Color TitleColor;
            public int TitleLeft;
            public int TitleRight;
            public int TitleNeed;
            public TMP_Text Close;
            public int CloseWidth;
            public RectTransform Ground;
            public RectTransform Row;
            public int RowWidth;
            public TMP_Text ProductCount;
            public readonly List<TMP_Text> Materials = new List<TMP_Text>();
            public int FullHeight;
            public int CollapsedHeight;
            public bool Ready;
            public bool ReadyKnown;
        }
    }
}
