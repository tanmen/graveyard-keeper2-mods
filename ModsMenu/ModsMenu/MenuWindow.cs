using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using LazyBearTechnology;
using ModsMenu.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ModsMenu
{
    /// <summary>
    /// 「Mods」の窓（ESC の窓の「Mods」から開く。PauseMenuButton）。登録された mod ごとに見出しの帯の木のタブ（TabBand）を出し、
    /// 選んだタブの mod の設定の行を並べる（2026-11-23 に本人が選んだ形）。
    ///
    /// 組み方は Craft Todo 0.7.0〜0.7.5 の設定の窓と同じ（ゲーム自身の Mods の窓 UICodeModsWindow と同じ）: 設定の窓（UIGameSettingsWindow）の
    /// GenericWindowLayout を写して中身を空にし、設定の窓の行（オン / オフと選択は UISwitchButton、数は音量の UISlider、ボタンは UIDialogWindowButton）を
    /// 写して並べる。LazyWindow を継ぐので、ゲームの窓と同じく ESC（戻る）で閉じ、開いている間はゲームの操作を止める。
    /// 値は各 mod の ConfigEntry にそのまま書く（BepInEx が cfg に保存し、SettingChanged を鳴らす）。
    ///
    /// 行はどのタブの分も中身の入れ物（Content）の直下に並べ、選んだタブの行だけを出す。タブを替えても窓の大きさが変わらないよう、
    /// タブごとの最後に詰め物（Filler）を置いて、いちばん高いタブに高さをそろえる。
    /// </summary>
    internal sealed class MenuWindow : LazyWindow<LazyWidgetDataBase>
    {
        // ゲームの Mods の窓と同じ並び順（ツールチップ 700 より下、ほかの窓より上）
        private const int WindowSortingOrder = 480;

        // 窓を閉じた直後、閉じたキーで各 mod のキーが鳴らないようにする時間
        private const float QuietSeconds = 0.25f;

        private const float ConfirmSeconds = 3f;

        // 下の段（ボタンと「OK」）の高さと間（ゲームのボタンは 26 の高さ）
        private const float ButtonRowHeight = 26f;
        private const float ButtonGap = 10f;

        // タブのために窓を広げる上限（UI の単位）
        private const float MaxExtraWidth = 360f;

        private static readonly BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Color LineColor = new Color32(120, 110, 100, 255);
        private static readonly Color CaptureColor = new Color32(255, 214, 92, 255);
        private static readonly KeyCode[] Modifiers =
        {
            KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftControl, KeyCode.RightControl, KeyCode.LeftAlt, KeyCode.RightAlt,
        };

        private static MenuWindow instance;
        private static Action onClosed;
        private static KeyCode[] captureKeys;
        private static float quietUntil;

        private readonly List<PageView> pages = new List<PageView>();
        private UISwitchButton switchTemplate;
        private UISlider sliderTemplate;
        private UIDialogWindowButton buttonTemplate;
        private GenericWindowLayout layoutTemplate;
        private TextMeshProUGUI windowTitle;
        private RectTransform content;
        private RectTransform header;
        private RectTransform layoutRoot;
        private RectTransform buttonRow;
        private TabBand tabBand;
        private UISwitchButton pageSwitch;
        private KeyRow capturing;
        private int escapeFrame = -1;
        private int current;
        private float contentWidth;
        private string builtLang;
        private int builtVersion = -1;
        private bool built;

        public static bool IsShownNow => instance != null && instance.IsShown;

        /// <summary>窓が開いている間（キーを選んでいる間も）と閉じた直後。</summary>
        public static bool BlocksHotkeys => IsShownNow || Time.unscaledTime < quietUntil;

        /// <summary>窓を開く。<paramref name="closed"/> は閉じたとき（ESC の窓を開き直す）。窓が組めなければすぐ呼ぶ。</summary>
        public static void OpenWindow(Action closed)
        {
            try
            {
                if (instance != null && (instance.builtLang != GameBridge.CurrentLang() || instance.builtVersion != Registry.Version))
                {
                    // 文言は組むときに書くので、言語を変えたら組み直す。タブや行が足されたときも
                    Destroy(instance.gameObject);
                    instance = null;
                }

                if (instance == null)
                {
                    instance = Create();
                }

                if (instance == null || !instance.built)
                {
                    closed?.Invoke();
                    return;
                }

                onClosed = closed;
                instance.Open(null);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Mods の窓を開けなかった: {e}");
                closed?.Invoke();
            }
        }

        private static MenuWindow Create()
        {
            Transform parent = GameBridge.FindUiRoot();
            if (parent == null)
            {
                Plugin.Log.LogWarning("Mods の窓: ゲームの UI の根が見つからない");
                return null;
            }

            var go = new GameObject("ModsMenu.Window", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = WindowSortingOrder;
            Canvas parentCanvas = parent.GetComponentInParent<Canvas>();
            if (parentCanvas != null && parentCanvas.rootCanvas != null)
            {
                // Sliced の縁の太さはこの値で決まる。根の Canvas に揃える
                canvas.referencePixelsPerUnit = parentCanvas.rootCanvas.referencePixelsPerUnit;
            }

            go.AddComponent<GraphicRaycaster>();
            GamepadNavigationController navigation = go.AddComponent<GamepadNavigationController>();
            if (navigation.navigationGroupSources == null)
            {
                navigation.navigationGroupSources = new List<GamepadNavigationController.NavigationGroupSource>();
            }

            MenuWindow window = go.AddComponent<MenuWindow>();
            if (!window.built)
            {
                Destroy(go);
                return null;
            }

            window.Init();
            return window;
        }

        private void Awake()
        {
            var rect = (RectTransform)transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            // ゲームの窓と同じく、後ろを暗くしてクリックを止める
            Image shade = gameObject.AddComponent<Image>();
            shade.color = new Color(0f, 0f, 0f, 0.4f);
            shade.raycastTarget = true;

            builtLang = GameBridge.CurrentLang();
            builtVersion = Registry.Version;
            content = ResolveTemplates() ? CloneLayout() : null;
            if (content == null)
            {
                Plugin.Log.LogWarning("Mods の窓: ゲームの設定の窓の部品が引けないので、窓を出さない");
                return;
            }

            BuildPages();
            if (pages.Count == 0)
            {
                Plugin.Log.LogWarning("Mods の窓: 出せるタブが無い");
                return;
            }

            BuildTabs();
            EqualizePages();
            current = Math.Max(0, pages.FindIndex(p => p.Page.Guid == Plugin.Instance?.LastPage?.Value));
            built = true;
            int rows = 0;
            foreach (PageView page in pages)
            {
                rows += page.Refreshers.Count;
            }

            Plugin.Log.LogInfo($"Mods の窓を組んだ（タブ {pages.Count}・行 {rows}・{(tabBand != null ? "木のタブ" : "切り替えの行")}・幅 {contentWidth}）");
        }

        public override void Open(LazyWidgetDataBase data)
        {
            base.Open(data);
            CancelCapture();
            ShowPage(current, refresh: true);
        }

        public override void Close()
        {
            if (!IsShown)
            {
                return;
            }

            CancelCapture();
            base.Close();
            quietUntil = Time.unscaledTime + QuietSeconds;
            Action action = onClosed;
            onClosed = null;
            action?.Invoke();
        }

        protected override bool OnPressedBack()
        {
            // キーを選んでいる間の Esc はやめるだけ（窓は閉じない）
            if (capturing != null || escapeFrame == Time.frameCount)
            {
                CancelCapture();
                return true;
            }

            Close();
            return true;
        }

        protected override Dictionary<GameKey, Func<bool>> GetGameKeyDelegates()
        {
            Dictionary<GameKey, Func<bool>> delegates = base.GetGameKeyDelegates();
            // 人物の窓と同じキーでタブを送る（ゲームパッドの LB / RB など）
            delegates[GameKey.NextTab] = () => StepPage(1);
            delegates[GameKey.PrevTab] = () => StepPage(-1);
            return delegates;
        }

        protected override void UpdateGamepadDependentStuff()
        {
            base.UpdateGamepadDependentStuff();
            tabBand?.UpdateGamepad(LazyInput.IsGamepadActive);
        }

        protected override void PrintTips()
        {
            if (lazyButtonTips != null)
            {
                lazyButtonTips.Print(LazyGameKeyTip.Back());
            }
        }

        protected override void TestDraw()
        {
        }

        protected override void Update()
        {
            if (IsShown)
            {
                Hooks.Guard("Mods の窓のキーの読み取り", PollCapture);
                Hooks.Guard("Mods の窓の 2 度押し", TickLatches);
            }

            base.Update();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        // ---- タブ ----

        private bool StepPage(int step)
        {
            if (capturing != null || pages.Count < 2)
            {
                return false;
            }

            int next = (current + step + pages.Count) % pages.Count;
            if (LazyInput.IsGamepadActive)
            {
                GameBridge.PlaySound("tab_click");
            }

            ShowPage(next, refresh: false);
            return true;
        }

        private void ShowPage(int index, bool refresh)
        {
            CancelCapture();
            current = Mathf.Clamp(index, 0, pages.Count - 1);
            for (int i = 0; i < pages.Count; i++)
            {
                PageView page = pages[i];
                bool on = i == current;
                foreach (GameObject row in page.Objects)
                {
                    if (row != null)
                    {
                        row.SetActive(on);
                    }
                }

                foreach (ButtonView button in page.Buttons)
                {
                    button.Latch.Reset();
                    button.Show();
                    button.Button.gameObject.SetActive(on);
                }

                if (on || refresh)
                {
                    foreach (Action refresher in page.Refreshers)
                    {
                        Hooks.Guard("Mods の窓の行の読み直し", refresher);
                    }
                }
            }

            pageSwitch?.UpdateField(current, fireCallback: false);
            ((RectTransform)transform).RefreshContentFitter();
            if (tabBand != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(header);
                tabBand.Show(current, header.rect.width);
                tabBand.UpdateGamepad(LazyInput.IsGamepadActive);
            }

            try
            {
                GamepadNavigationController.ReinitItems(focusOnFirstActive: LazyInput.IsGamepadActive);
            }
            catch (Exception)
            {
                // ゲームパッドの移動先が組み直せなくても、マウスでは使える
            }

            if (Plugin.Instance != null && Plugin.Instance.LastPage.Value != pages[current].Page.Guid)
            {
                Plugin.Instance.LastPage.Value = pages[current].Page.Guid;
            }
        }

        private void BuildTabs()
        {
            var titles = new List<string>();
            foreach (PageView page in pages)
            {
                titles.Add(page.Title);
            }

            tabBand = header == null ? null : TabBand.TryCreate(header, switchTemplate);
            if (tabBand != null)
            {
                tabBand.Selected = i => ShowPage(i, refresh: false);
                tabBand.Build(titles);
                if (windowTitle != null)
                {
                    // 人物の窓と同じく、見出しの帯には題を出さずタブだけを置く
                    windowTitle.gameObject.SetActive(false);
                }

                foreach (string side in new[] { "DecorCommonLeft", "DecorCommonRight" })
                {
                    Transform decor = header.Find(side);
                    decor?.gameObject.SetActive(false);
                }

                // 帯に全部入らなければ、中身の入れ物を広げて窓を広げる（上限まで。WidenFrame）。
                // 0.1.0 は中身の行の幅（詰め物）で広げようとしたが、入れ物は 340 のままで帯も 366 のまま、3 枚目が矢印の先に隠れた（2026-11-23）
                ((RectTransform)transform).RefreshContentFitter();
                float before = header.rect.width;
                float required = tabBand.RequiredHeaderWidth();
                float headerWidth = before;
                if (required > headerWidth)
                {
                    headerWidth = WidenFrame(Mathf.Min(Mathf.Ceil(required - headerWidth), MaxExtraWidth), headerWidth);
                }

                // 枠が広げられなかった（足りなかった）ときは、タブの左右の余白を詰めて入れる。それでも入らない分は矢印で送る
                if (required > headerWidth)
                {
                    tabBand.Compact(required - headerWidth);
                }

                Plugin.Log.LogInfo($"タブ: 見出しの帯の幅 {before} → {headerWidth}・全部入るのに要る幅 {required} → {tabBand.RequiredHeaderWidth()}");
                return;
            }

            // 木のタブが引けないときは、いちばん上に「Mod ◀ 名前 ▶」の切り替えの行を置く（題は「Mods」のまま）
            pageSwitch = Instantiate(switchTemplate, content, false);
            pageSwitch.name = "PageSwitch";
            pageSwitch.transform.SetAsFirstSibling();
            IgnoreLocalize(pageSwitch.gameObject);
            pageSwitch.Initialize(i => Hooks.Guard("Mods の窓の切り替えの行", () => ShowPage(i, refresh: false)), titles.ToArray(), 0, Strings.ModLabel);
            SetHeader(pageSwitch.gameObject, Strings.ModLabel);
        }

        /// <summary>
        /// 窓を <paramref name="extra"/> だけ広げ、広げたあとの見出しの帯の幅を返す。
        /// 設定の窓の枠（GenericWindowLayout の根）は ContentSizeFitter（横も縦も preferred）＋縦並び（左右の余白 24・子の幅は決めない）で、
        /// 幅は中身の入れ物 Content の幅（340 に固定。Content の ContentSizeFitter は縦だけ）＋48 で決まる（MainScene の bundle で読んだ。2026-11-23）。
        /// だから Content の幅を広げる（根の幅を直に変えても fitter が上書きし、Content の中の行を広げても Content は 340 のまま）。
        /// 帯が広がらなければ（ゲームの更新で作りが変わった）元に戻し、そのときの部品の形をログに出す。
        /// </summary>
        private float WidenFrame(float extra, float before)
        {
            Vector2 size = content.sizeDelta;
            content.sizeDelta = new Vector2(size.x + extra, size.y);
            ((RectTransform)transform).RefreshContentFitter();
            float after = header.rect.width;
            if (after > before + 0.5f)
            {
                return after;
            }

            ContentSizeFitter rootFitter = layoutRoot != null ? layoutRoot.GetComponent<ContentSizeFitter>() : null;
            ContentSizeFitter contentFitter = content.GetComponent<ContentSizeFitter>();
            Plugin.Log.LogWarning($"タブ: 中身の入れ物を広げても見出しの帯が広がらない（中身 {size}・中身の fitter 横 "
                + $"{(contentFitter != null ? contentFitter.horizontalFit.ToString() : "なし")}・枠の根の fitter 横 "
                + $"{(rootFitter != null ? rootFitter.horizontalFit.ToString() : "なし")}）。タブの余白を詰める");
            content.sizeDelta = size;
            ((RectTransform)transform).RefreshContentFitter();
            return header.rect.width;
        }

        // ---- ページ ----

        private void BuildPages()
        {
            contentWidth = RowWidth();
            foreach (ModPage page in Registry.Ordered())
            {
                var view = new PageView { Page = page, Title = page.Title() };
                try
                {
                    foreach (MenuRow row in page.Rows)
                    {
                        AddRow(view, row);
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Mods の窓: {view.Title} のタブを組めなかった: {e}");
                    foreach (GameObject go in view.Objects)
                    {
                        Destroy(go);
                    }

                    continue;
                }

                pages.Add(view);
            }

            BuildButtonRow();
        }

        private void AddRow(PageView view, MenuRow row)
        {
            string label = ModPage.Safe(row.Label) ?? string.Empty;
            switch (row.Kind)
            {
                case MenuRowKind.Section:
                    view.Objects.Add(Section(label));
                    break;
                case MenuRowKind.Toggle:
                    view.Objects.Add(Switch(view, label, () => new[] { GameBridge.L("ui_off", "Off"), GameBridge.L("ui_on", "On") }, row));
                    break;
                case MenuRowKind.Choice:
                    view.Objects.Add(Switch(view, label, row.Names, row));
                    break;
                case MenuRowKind.Number:
                    view.Objects.Add(Number(view, label, row));
                    break;
                case MenuRowKind.Key:
                    view.Objects.Add(Key(view, label, row.Shortcut));
                    break;
            }
        }

        /// <summary>
        /// タブを替えても窓の大きさが変わらないよう、タブごとの最後に詰め物を置いて、いちばん高いタブに高さをそろえる。
        /// 詰め物の幅は窓の中身の幅（タブのために広げた幅）にする。
        /// </summary>
        private void EqualizePages()
        {
            // 中身の入れ物は子を真ん中にそろえる（ゲームの設定のまま）ので、タブのために窓を広げても行は真ん中に並ぶ
            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            float spacing = layout != null ? layout.spacing : 6f;

            var heights = new List<float>();
            float tallest = 0f;
            foreach (PageView page in pages)
            {
                float height = 0f;
                int count = 0;
                foreach (GameObject go in page.Objects)
                {
                    if (go == null)
                    {
                        continue;
                    }

                    height += ((RectTransform)go.transform).sizeDelta.y + (count > 0 ? spacing : 0f);
                    count++;
                }

                heights.Add(height);
                tallest = Mathf.Max(tallest, height);
            }

            // 行の後ろの詰め物も間を 1 つ取る（行が 0 のタブは詰め物が先頭なので間が付かない）。どのタブも「いちばん高いタブ＋間 1 つ」にそろえる
            float target = tallest + spacing;
            for (int i = 0; i < pages.Count; i++)
            {
                float fill = target - heights[i] - (pages[i].Objects.Count > 0 ? spacing : 0f);
                RectTransform filler = NewRow("Filler", new Vector2(contentWidth, Mathf.Max(0f, fill)));
                pages[i].Objects.Add(filler.gameObject);
            }

            buttonRow.SetAsLastSibling();
            buttonRow.sizeDelta = new Vector2(contentWidth, ButtonRowHeight);
        }

        // ---- 行 ----

        /// <summary>節の見出し: 文字を中央に、左右に細い線。</summary>
        private GameObject Section(string text)
        {
            RectTransform row = NewRow("Section", RowSize());
            TextMeshProUGUI label = CopyLabel(row, "Label", text, windowTitle, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);

            float width = RowWidth();
            float textWidth = Mathf.Ceil(label.GetPreferredValues(text).x);
            float lineEnd = Mathf.Floor((width - textWidth) / 2f) - 8f;
            if (lineEnd > 28f)
            {
                Line(row, 20f, lineEnd);
                Line(row, width - lineEnd, width - 20f);
            }

            return row.gameObject;
        }

        /// <summary>オン / オフと選択（ゲームの「垂直同期」の行を写す）。</summary>
        private GameObject Switch(PageView view, string name, Func<string[]> names, MenuRow def)
        {
            UISwitchButton row = Instantiate(switchTemplate, content, false);
            row.name = def.Kind.ToString();
            IgnoreLocalize(row.gameObject);
            string[] fields = names();
            if (fields == null || fields.Length == 0)
            {
                fields = new[] { "?" };
            }

            row.Initialize(i => Hooks.Guard("Mods の窓の切り替え", () => def.SetIndex(i)), fields, Math.Max(0, def.GetIndex()), name);
            SetHeader(row.gameObject, name);
            view.Refreshers.Add(() => row.UpdateField(Math.Max(0, def.GetIndex()), fireCallback: false));
            return row.gameObject;
        }

        /// <summary>数（ゲームの音量の行を写す）。− ＋ とつまみで 1 刻みずつ。つまみは刻みの番号で持ち、欄には値を出す。</summary>
        private GameObject Number(PageView view, string name, MenuRow def)
        {
            UISlider row = Instantiate(sliderTemplate, content, false);
            row.name = "Number";
            IgnoreLocalize(row.gameObject);
            SetHeader(row.gameObject, name);
            Slider slider = Field<Slider>(row, "slider");
            TMP_Text amount = Field<TextMeshProUGUI>(row, "amountLabel");
            FieldInfo callback = FindField(row.GetType(), "onChangedCallback");
            FieldInfo step = FindField(row.GetType(), "step");
            FieldInfo quiet = FindField(row.GetType(), "suppressClickSound");
            if (slider == null || callback == null || step == null)
            {
                Destroy(row.gameObject);
                throw new InvalidOperationException($"数の行の部品が引けない（{name}）");
            }

            StepScale scale = def.Scale;

            void ShowValue(double value)
            {
                if (amount != null)
                {
                    amount.text = scale.Format(value);
                }
            }

            // UISlider は「つまみ × step」を欄に書いてから呼び返すので、呼び返しの中で値に書き直す
            void Changed(float index)
            {
                double value = scale.ValueAt(Mathf.RoundToInt(index));
                def.SetNumber(value);
                ShowValue(value);
            }

            void Apply()
            {
                double value = def.GetNumber();
                callback.SetValue(row, null);
                quiet?.SetValue(row, true);
                step.SetValue(row, 1f);
                slider.wholeNumbers = true;
                slider.minValue = 0;
                slider.maxValue = scale.Steps;
                slider.SetValueWithoutNotify(scale.IndexOf(value));
                ShowValue(value);
                quiet?.SetValue(row, false);
                callback.SetValue(row, (Action<float>)(v => Hooks.Guard("Mods の窓の数", () => Changed(v))));
            }

            Apply();
            view.Refreshers.Add(Apply);
            return row.gameObject;
        }

        /// <summary>キー（切り替えの行を写して、矢印の所まで広げた欄にする）。押すと次に押したキーにする。</summary>
        private GameObject Key(PageView view, string name, ConfigEntry<KeyboardShortcut> entry)
        {
            TextMeshProUGUI value = WideCellRow("Key", name, out RectTransform cell, out GameObject row);
            var keyRow = new KeyRow { Entry = entry, Value = value, Color = value.color };
            ClickRelay relay = cell.gameObject.AddComponent<ClickRelay>();
            relay.Clicked = () => Hooks.Guard("Mods の窓のキーの欄", () => StartCapture(keyRow));
            view.Refreshers.Add(() => ShowKey(keyRow));
            return row;
        }

        /// <summary>
        /// 切り替えの行を写し、値の欄を矢印の所まで広げた押せる欄にする。値の文字を返す。
        /// ⚠ 写した欄（Back）の大きさを変えても、窓を開くと元の 118 に戻った（Craft Todo 0.7.0。何が戻すかは分かっていない）ので、
        /// 同じ絵の欄を新しく作って値の文字をそこへ移し、写した欄と矢印は隠す。
        /// </summary>
        private TextMeshProUGUI WideCellRow(string rowName, string name, out RectTransform cell, out GameObject row)
        {
            UISwitchButton template = Instantiate(switchTemplate, content, false);
            row = template.gameObject;
            row.name = rowName;
            IgnoreLocalize(row);
            SetHeader(row, name);
            TextMeshProUGUI value = Field<TextMeshProUGUI>(template, "amountLabel");
            // 矢印と、その押し下げを読む UISwitchButton は使わない
            DestroyImmediate(template);
            var left = row.transform.Find("ToLeft") as RectTransform;
            var right = row.transform.Find("ToRight") as RectTransform;
            var back = row.transform.Find("Back") as RectTransform;
            Image backImage = back == null ? null : back.GetComponent<Image>();
            if (backImage == null || value == null)
            {
                Destroy(row);
                throw new InvalidOperationException($"押す欄の行の部品が引けない（{name}）");
            }

            // 行の中の位置（行の pivot から）で、左の矢印の左端から右の矢印の右端まで
            float xMin = left != null ? left.localPosition.x + left.rect.xMin : back.localPosition.x + back.rect.xMin;
            float xMax = right != null ? right.localPosition.x + right.rect.xMax : back.localPosition.x + back.rect.xMax;
            float shift = back.anchoredPosition.x - back.localPosition.x;

            var go = new GameObject("Cell", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = row.layer;
            cell = (RectTransform)go.transform;
            cell.SetParent(row.transform, false);
            cell.anchorMin = back.anchorMin;
            cell.anchorMax = back.anchorMax;
            cell.pivot = new Vector2(0.5f, 0.5f);
            cell.sizeDelta = new Vector2(Mathf.Round(xMax - xMin), back.sizeDelta.y);
            cell.anchoredPosition = new Vector2(Mathf.Round((xMin + xMax) / 2f + shift), back.anchoredPosition.y);
            Image image = go.GetComponent<Image>();
            image.sprite = backImage.sprite;
            image.type = backImage.type;
            image.pixelsPerUnitMultiplier = backImage.pixelsPerUnitMultiplier;
            image.color = backImage.color;
            image.raycastTarget = true;
            value.transform.SetParent(cell, false);

            back.gameObject.SetActive(false);
            left?.gameObject.SetActive(false);
            right?.gameObject.SetActive(false);
            return value;
        }

        /// <summary>
        /// 下の段: 左に各タブのボタン（選んだタブの分だけ出す）、右に「OK」。どれもゲームの設定の窓の「OK」のボタンを写す（幅は文字に合わせて決まる）。
        /// Craft Todo 0.7.0 で本人が決めた形（「メモを全部消す」を OK の隣に。他のゲームの窓と同じく、下の赤いボタンは閉じる OK）。
        /// </summary>
        private void BuildButtonRow()
        {
            var go = new GameObject("Buttons", typeof(RectTransform));
            go.layer = content.gameObject.layer;
            buttonRow = (RectTransform)go.transform;
            buttonRow.SetParent(content, false);
            buttonRow.sizeDelta = new Vector2(RowWidth(), ButtonRowHeight);
            HorizontalLayoutGroup layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = ButtonGap;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            foreach (PageView page in pages)
            {
                foreach (MenuButton def in page.Page.Buttons)
                {
                    UIDialogWindowButton button = Instantiate(buttonTemplate, buttonRow, false);
                    button.name = "PageButton";
                    IgnoreLocalize(button.gameObject);
                    var view = new ButtonView { Def = def, Button = button, Latch = new ConfirmLatch(ConfirmSeconds), Title = page.Title };
                    view.Show();
                    page.Buttons.Add(view);
                }
            }

            UIDialogWindowButton ok = Instantiate(buttonTemplate, buttonRow, false);
            ok.name = "OkButton";
            IgnoreLocalize(ok.gameObject);
            ok.Draw(new UIDialogWindowData.ButtonData(Close, GameBridge.L("btn_ok", "OK"), null, replaceForGamepad: false, GameKey.Select));
            ok.transform.SetAsLastSibling();
        }

        private void TickLatches()
        {
            float now = Time.unscaledTime;
            foreach (ButtonView button in pages[current].Buttons)
            {
                if (button.Latch.Tick(now))
                {
                    button.Show();
                }
            }
        }

        // ---- キーを選ぶ ----

        private void StartCapture(KeyRow row)
        {
            if (capturing != null && capturing != row)
            {
                ShowKey(capturing);
            }

            capturing = row;
            row.Value.text = Strings.PressKey;
            row.Value.color = CaptureColor;
        }

        private void CancelCapture()
        {
            if (capturing == null)
            {
                return;
            }

            KeyRow row = capturing;
            capturing = null;
            ShowKey(row);
        }

        private void PollCapture()
        {
            if (capturing == null)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                escapeFrame = Time.frameCount;
                CancelCapture();
                return;
            }

            foreach (KeyCode key in CaptureKeys())
            {
                if (!Input.GetKeyDown(key))
                {
                    continue;
                }

                var held = new List<KeyCode>();
                foreach (KeyCode modifier in Modifiers)
                {
                    if (Input.GetKey(modifier))
                    {
                        held.Add(modifier);
                    }
                }

                KeyRow row = capturing;
                capturing = null;
                row.Entry.Value = new KeyboardShortcut(key, held.ToArray());
                Plugin.Log.LogInfo($"キーを変えた: {pages[current].Page.Guid} {row.Entry.Definition} = {row.Entry.Value}");
                ShowKey(row);
                return;
            }
        }

        private static void ShowKey(KeyRow row)
        {
            row.Value.text = Strings.KeyName(row.Entry.Value);
            row.Value.color = row.Color;
        }

        /// <summary>選べるキー: 修飾キー・Esc・左クリック（欄を押すのに使う）・ゲームパッドのボタンを除く全部。</summary>
        private static KeyCode[] CaptureKeys()
        {
            if (captureKeys != null)
            {
                return captureKeys;
            }

            var keys = new List<KeyCode>();
            foreach (KeyCode key in (KeyCode[])Enum.GetValues(typeof(KeyCode)))
            {
                if (key == KeyCode.None || key == KeyCode.Escape || key == KeyCode.Mouse0 || key >= KeyCode.JoystickButton0
                    || Array.IndexOf(Modifiers, key) >= 0 || key == KeyCode.LeftCommand || key == KeyCode.RightCommand
                    || key == KeyCode.LeftWindows || key == KeyCode.RightWindows || key == KeyCode.AltGr)
                {
                    continue;
                }

                if (!keys.Contains(key))
                {
                    keys.Add(key);
                }
            }

            captureKeys = keys.ToArray();
            return captureKeys;
        }

        // ---- ゲームの設定の窓から部品を写す ----

        private bool ResolveTemplates()
        {
            UIGameSettingsWindow settings = null;
            foreach (UIGameSettingsWindow w in Resources.FindObjectsOfTypeAll<UIGameSettingsWindow>())
            {
                if (w != null && w.gameObject.scene.IsValid())
                {
                    settings = w;
                    break;
                }
            }

            if (settings == null)
            {
                return false;
            }

            switchTemplate = Field<UISwitchButton>(settings, "vSyncButton");
            sliderTemplate = Field<UISlider>(settings, "masterVolumeSlider");
            buttonTemplate = Field<UIDialogWindowButton>(settings, "lazyButton");
            layoutTemplate = settings.GetComponentInChildren<GenericWindowLayout>(true);
            if (switchTemplate == null || sliderTemplate == null || buttonTemplate == null || layoutTemplate == null)
            {
                Plugin.Log.LogWarning($"Mods の窓: 部品が足りない（切り替え {switchTemplate != null}・数 {sliderTemplate != null}・"
                    + $"ボタン {buttonTemplate != null}・枠 {layoutTemplate != null}）");
                return false;
            }

            return true;
        }

        /// <summary>枠を写して中身を空にする（UICodeModsWindow.CloneCommonLayout と同じ）。中身の入れ物を返す。</summary>
        private RectTransform CloneLayout()
        {
            var holder = new GameObject("LayoutHolder", typeof(RectTransform));
            holder.SetActive(false);
            holder.transform.SetParent(transform, false);
            GameObject layout = Instantiate(layoutTemplate.gameObject, holder.transform, false);
            layout.name = "GenericWindowLayout";
            Transform inner = layout.transform.Find("Content");
            Transform title = layout.transform.Find("Frame/HeaderGroup/Header");
            if (inner == null || title == null)
            {
                Destroy(holder);
                return null;
            }

            for (int i = inner.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(inner.GetChild(i).gameObject);
            }

            foreach (LocalizedLabel label in title.GetComponents<LocalizedLabel>())
            {
                DestroyImmediate(label);
            }

            header = title.parent as RectTransform;
            windowTitle = title.GetComponent<TextMeshProUGUI>();
            if (windowTitle != null)
            {
                windowTitle.text = Strings.Title;
            }

            Transform close = layout.transform.Find("Frame/HeaderGroup/CloseButton");
            if (close != null)
            {
                close.gameObject.SetActive(true);
                closeButton = close.GetComponentInChildren<LazyButton>(true);
            }

            layout.transform.SetParent(transform, false);
            layoutRoot = (RectTransform)layout.transform;
            Destroy(holder);
            return (RectTransform)inner;
        }

        // ---- 小道具 ----

        private float RowWidth() => switchTemplate != null ? ((RectTransform)switchTemplate.transform).sizeDelta.x : 330f;

        private Vector2 RowSize() => switchTemplate != null ? ((RectTransform)switchTemplate.transform).sizeDelta : new Vector2(330f, 16f);

        private RectTransform NewRow(string name, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = content.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(content, false);
            rect.sizeDelta = size;
            return rect;
        }

        private static void Line(RectTransform row, float x0, float x1)
        {
            var go = new GameObject("Line", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = row.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(row, false);
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(Mathf.Round(x0), 0f);
            rect.sizeDelta = new Vector2(Mathf.Round(x1 - x0), 1f);
            Image image = go.GetComponent<Image>();
            image.color = LineColor;
            image.raycastTarget = false;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>写した行の名前の欄（LeftName）に書く。UISwitchButton の headerLabel が無い行もあるので、名前でも引く。</summary>
        private static void SetHeader(GameObject row, string text)
        {
            Transform name = row.transform.Find("LeftName");
            TMP_Text label = name != null ? name.GetComponent<TMP_Text>() : null;
            if (label != null)
            {
                label.text = text;
            }
        }

        /// <summary>写した部品の訳文の差し替えを止める（言語を変えたり窓を開き直したりしたときに、こちらの文言が上書きされないように）。</summary>
        private static void IgnoreLocalize(GameObject go)
        {
            foreach (LocalizedLabel label in go.GetComponentsInChildren<LocalizedLabel>(true))
            {
                label.IgnoreLocalize = true;
            }
        }

        /// <summary>手本の文字と同じ書体・大きさ・色・文字の形の文字を作る（UICodeModsWindow.CreateLabel と同じ）。</summary>
        /// <remarks>
        /// 揃え方は文字の形（TextStyleComponent）を当てる前に決める。TextStyle.ApplyStyle は初めて当てたときの揃え方を覚えて当てるたびに戻すので、
        /// 後から変えると左上に戻る（Craft Todo 0.7.0 の節の見出しが左に寄った）。
        /// </remarks>
        private static TextMeshProUGUI CopyLabel(Transform parent, string name, string text, TextMeshProUGUI template, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
            label.fontSize = template == null ? 16f : (template.enableAutoSizing ? template.fontSizeMax : template.fontSize);
            label.enableAutoSizing = false;
            if (template != null)
            {
                label.font = template.font;
                label.fontSharedMaterial = template.fontSharedMaterial;
                label.color = template.color;
                label.isOrthographic = template.isOrthographic;
                label.extraPadding = template.extraPadding;
                label.characterSpacing = template.characterSpacing;
                label.wordSpacing = template.wordSpacing;
            }

            label.text = text;
            label.alignment = alignment;
            label.richText = true;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            TextStyleComponent style = template == null ? null : template.GetComponent<TextStyleComponent>();
            if (style != null && style.CurrentTextStyle != null)
            {
                go.AddComponent<TextStyleComponent>().SetTextStyle(style.CurrentTextStyle);
            }

            return label;
        }

        private static T Field<T>(object target, string name) where T : class
        {
            FieldInfo field = target == null ? null : FindField(target.GetType(), name);
            return field == null ? null : field.GetValue(target) as T;
        }

        private static FieldInfo FindField(Type type, string name)
        {
            while (type != null)
            {
                FieldInfo field = type.GetField(name, AnyInstance);
                if (field != null)
                {
                    return field;
                }

                type = type.BaseType;
            }

            return null;
        }

        private sealed class PageView
        {
            public ModPage Page;
            public string Title;
            public readonly List<GameObject> Objects = new List<GameObject>();
            public readonly List<Action> Refreshers = new List<Action>();
            public readonly List<ButtonView> Buttons = new List<ButtonView>();
        }

        private sealed class ButtonView
        {
            public MenuButton Def;
            public UIDialogWindowButton Button;
            public ConfirmLatch Latch;
            public string Title;

            /// <summary>構えているなら確かめの文言、そうでなければふだんの文言で描き直す。</summary>
            public void Show()
            {
                Func<string> text = Latch.Armed && Def.ConfirmLabel != null ? Def.ConfirmLabel : Def.Label;
                Button.Draw(new UIDialogWindowData.ButtonData(Pressed, ModPage.Safe(text) ?? string.Empty, null, replaceForGamepad: false, GameKey.Select));
            }

            private void Pressed()
            {
                Hooks.Guard($"{Title} のボタン", () =>
                {
                    if (Def.ConfirmLabel != null && !Latch.Press(Time.unscaledTime))
                    {
                        Show();
                        return;
                    }

                    Def.OnClick();
                    Show();
                });
            }
        }

        private sealed class KeyRow
        {
            public ConfigEntry<KeyboardShortcut> Entry;
            public TMP_Text Value;
            public Color Color;
        }
    }

    /// <summary>押されたら <see cref="Clicked"/> を呼ぶだけの部品（キーの欄）。</summary>
    internal sealed class ClickRelay : MonoBehaviour, IPointerClickHandler
    {
        public Action Clicked;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                GameBridge.PlayClick();
                Clicked?.Invoke();
            }
        }
    }
}
