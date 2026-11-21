using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using LazyBearTechnology;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CraftTodo
{
    /// <summary>
    /// 「制作メニューの設定」の窓（0.7.0〜。ESC の窓の「制作メニュー」から開く。PauseMenuButton）。
    /// 形は 2026-11-21 に本人が図で決めた: ゲームの設定の窓（UIGameSettingsWindow）と同じ部品で、
    /// 節「数え方・足し方」「表示」「キー」と「メモを全部消す」のボタン。
    ///
    /// 組み方はゲーム自身の Mods の窓（UICodeModsWindow）と同じ: 設定の窓の GenericWindowLayout（枠・暗い地・木の帯・閉じるボタン）を写して
    /// 中身を空にし、設定の窓の行（オン / オフは UISwitchButton、数は音量の UISlider、ボタンは UIDialogWindowButton）を写して並べる。
    /// LazyWindow を継ぐので、ゲームの窓と同じく ESC（戻る）で閉じ、開いている間はゲームの操作を止める。
    /// 値は BepInEx の設定（cfg）にそのまま書く（書いた時点で cfg に保存される）。
    /// </summary>
    internal sealed class SettingsWindow : LazyWindow<LazyWidgetDataBase>
    {
        // ゲームの Mods の窓と同じ並び順（ツールチップ 700 より下、ほかの窓より上）
        private const int WindowSortingOrder = 480;

        // 「メモを全部消す」を 1 度押してから、もう一度押すと消せる時間
        private const float ClearConfirmSeconds = 3f;

        // 窓を閉じた直後、閉じたキーで制作メニューのキーが鳴らないようにする時間
        private const float QuietSeconds = 0.25f;

        // 下の段（「メモを全部消す」と「OK」）の高さと間（ゲームのボタンは 26 の高さ）
        private const float ButtonRowHeight = 26f;
        private const float ButtonGap = 10f;

        private const int OffsetMin = 0;
        private const int OffsetMax = 60;

        private static readonly BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Color LineColor = new Color32(120, 110, 100, 255);
        private static readonly Color CaptureColor = new Color32(255, 214, 92, 255);
        private static readonly KeyCode[] Modifiers =
        {
            KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftControl, KeyCode.RightControl, KeyCode.LeftAlt, KeyCode.RightAlt,
        };

        private static SettingsWindow instance;
        private static Action onClosed;
        private static KeyCode[] captureKeys;

        private readonly List<Action> refreshers = new List<Action>();
        private readonly List<KeyRow> keyRows = new List<KeyRow>();
        private UISwitchButton switchTemplate;
        private UISlider sliderTemplate;
        private UIDialogWindowButton buttonTemplate;
        private TextMeshProUGUI windowTitle;
        private UIDialogWindowButton clearButton;
        private float clearArmedUntil;
        private bool clearArmed;
        private KeyRow capturing;
        private int escapeFrame = -1;
        private float quietUntil;
        private string builtLang;
        private bool built;

        /// <summary>設定の窓が開いている間（と閉じた直後）は、制作メニューのキーを鳴らさない。</summary>
        public static bool BlocksHotkeys => instance != null && (instance.IsShown || Time.unscaledTime < instance.quietUntil);

        /// <summary>窓を開く。<paramref name="closed"/> は閉じたとき（ESC の窓を開き直す）。窓が組めなければすぐ呼ぶ。</summary>
        public static void OpenWindow(Action closed)
        {
            try
            {
                if (instance != null && instance.builtLang != GameBridge.CurrentLang())
                {
                    // 文言は組むときに書くので、言語を変えたら組み直す
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
                Plugin.Log.LogError($"設定の窓を開けなかった: {e}");
                closed?.Invoke();
            }
        }

        private static SettingsWindow Create()
        {
            Transform parent = GameBridge.FindUiRoot();
            if (parent == null)
            {
                Plugin.Log.LogWarning("設定の窓: ゲームの UI の根が見つからない");
                return null;
            }

            var go = new GameObject("CraftTodo.SettingsWindow", typeof(RectTransform));
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

            SettingsWindow window = go.AddComponent<SettingsWindow>();
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
            RectTransform content = ResolveTemplates() ? CloneLayout() : null;
            if (content == null)
            {
                Plugin.Log.LogWarning("設定の窓: ゲームの設定の窓の部品が引けないので、窓を出さない");
                return;
            }

            BuildRows(content);
            built = true;
            Plugin.Log.LogInfo($"設定の窓を組んだ（行 {refreshers.Count}・キー {keyRows.Count}）");
        }

        public override void Open(LazyWidgetDataBase data)
        {
            base.Open(data);
            CancelCapture();
            DisarmClear();
            foreach (Action refresh in refreshers)
            {
                refresh();
            }

            ((RectTransform)transform).RefreshContentFitter();
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
                Hooks.Guard("設定の窓のキーの読み取り", PollCapture);
                if (clearArmed && Time.unscaledTime >= clearArmedUntil)
                {
                    DisarmClear();
                }
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
                Plugin.Log.LogWarning($"設定の窓: 部品が足りない（切り替え {switchTemplate != null}・数 {sliderTemplate != null}・"
                    + $"ボタン {buttonTemplate != null}・枠 {layoutTemplate != null}）");
                return false;
            }

            return true;
        }

        private GenericWindowLayout layoutTemplate;

        /// <summary>枠を写して中身を空にする（UICodeModsWindow.CloneCommonLayout と同じ）。中身の入れ物を返す。</summary>
        private RectTransform CloneLayout()
        {
            var holder = new GameObject("LayoutHolder", typeof(RectTransform));
            holder.SetActive(false);
            holder.transform.SetParent(transform, false);
            GameObject layout = Instantiate(layoutTemplate.gameObject, holder.transform, false);
            layout.name = "GenericWindowLayout";
            Transform content = layout.transform.Find("Content");
            Transform header = layout.transform.Find("Frame/HeaderGroup/Header");
            if (content == null || header == null)
            {
                Destroy(holder);
                return null;
            }

            for (int i = content.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(content.GetChild(i).gameObject);
            }

            foreach (LocalizedLabel label in header.GetComponents<LocalizedLabel>())
            {
                DestroyImmediate(label);
            }

            windowTitle = header.GetComponent<TextMeshProUGUI>();
            if (windowTitle != null)
            {
                windowTitle.text = Strings.SettingsTitle;
            }

            Transform close = layout.transform.Find("Frame/HeaderGroup/CloseButton");
            if (close != null)
            {
                close.gameObject.SetActive(true);
                closeButton = close.GetComponentInChildren<LazyButton>(true);
            }

            layout.transform.SetParent(transform, false);
            Destroy(holder);
            return (RectTransform)content;
        }

        private void BuildRows(RectTransform content)
        {
            Plugin p = Plugin.Instance;
            Section(content, Strings.SectionCounting);
            Toggle(content, Strings.StackMaterials, p.StackMaterials);
            Toggle(content, Strings.AddOnBlockedClick, p.AddOnBlockedClick);
            Toggle(content, Strings.AddWhenCraftWindowOpens, p.AddWhenCraftWindowOpens);

            Section(content, Strings.SectionDisplay);
            Toggle(content, Strings.Visible, p.Visible);
            Number(content, Strings.MaxEntries, 1, 30, () => p.MaxEntries.Value, v => p.MaxEntries.Value = v);
            Toggle(content, Strings.AutoPosition, p.AutoPosition);
            Number(content, Strings.OffsetX, OffsetMin, OffsetMax, () => Mathf.RoundToInt(p.OffsetX.Value), v => p.OffsetX.Value = v);
            Number(content, Strings.OffsetY, OffsetMin, OffsetMax, () => Mathf.RoundToInt(p.OffsetY.Value), v => p.OffsetY.Value = v);
            Toggle(content, Strings.GameFrame, p.GameFrame);

            Section(content, Strings.SectionKeys);
            Key(content, Strings.ToggleKey, p.ToggleKey);
            Key(content, Strings.StackKey, p.StackKey);
            Key(content, Strings.RegisterKey, p.RegisterKey);
            Key(content, Strings.RemoveKey, p.RemoveKey);
            Key(content, Strings.ClearKey, p.ClearKey);

            // 下の段は「メモを全部消す」と「OK」（閉じる）を横に並べる（OK が右）。2026-11-21 に本人:「下にリセットではなく、OKにしたい。
            // 他の設定が全部下部の赤いボタンがOKなので、そのまま閉じる為に押しそうになった」→ 続けて「OKの隣か上に全削除のボタン配置しても
            // いいかと思った」で、隣と上の 2 案の図から隣を選んだ
            BottomButtons(content);
        }

        // ---- 行 ----

        /// <summary>節の見出し: 文字を中央に、左右に細い線（図の形）。</summary>
        private void Section(RectTransform content, string text)
        {
            RectTransform row = NewRow(content, "Section");
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
        }

        /// <summary>オン / オフ（ゲームの「垂直同期」の行を写す）。</summary>
        private void Toggle(RectTransform content, string name, ConfigEntry<bool> entry)
        {
            UISwitchButton row = Instantiate(switchTemplate, content, false);
            row.name = "Toggle";
            IgnoreLocalize(row.gameObject);
            string[] fields = { L("ui_off", "Off"), L("ui_on", "On") };
            row.Initialize(i => Hooks.Guard("設定の窓の切り替え", () => entry.Value = i == 1), fields, entry.Value ? 1 : 0, name);
            SetHeader(row.gameObject, name);
            refreshers.Add(() => row.UpdateField(entry.Value ? 1 : 0, fireCallback: false));
        }

        /// <summary>数（ゲームの音量の行を写す）。− ＋ とつまみで 1 ずつ。</summary>
        private void Number(RectTransform content, string name, int min, int max, Func<int> get, Action<int> set)
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
                Plugin.Log.LogWarning($"設定の窓: 数の行の部品が引けない（{name}）");
                Destroy(row.gameObject);
                return;
            }

            // UISlider は 0〜100 を step で刻む作りなので、範囲は Slider に直に入れる（UISlider の − ＋ は value を 1 ずつ動かす）
            void Apply()
            {
                int value = get();
                callback.SetValue(row, null);
                quiet?.SetValue(row, true);
                step.SetValue(row, 1f);
                slider.wholeNumbers = true;
                slider.minValue = Math.Min(min, value);
                slider.maxValue = Math.Max(max, value);
                slider.SetValueWithoutNotify(value);
                if (amount != null)
                {
                    amount.text = value.ToString();
                }

                quiet?.SetValue(row, false);
                callback.SetValue(row, (Action<float>)(v => Hooks.Guard("設定の窓の数", () => set(Mathf.RoundToInt(v)))));
            }

            Apply();
            refreshers.Add(Apply);
        }

        /// <summary>キー（切り替えの行を写して、矢印の所まで広げた欄にする）。押すと次に押したキーにする。</summary>
        private void Key(RectTransform content, string name, ConfigEntry<KeyboardShortcut> entry)
        {
            TextMeshProUGUI value = WideCellRow(content, "Key", name, out RectTransform cell);
            if (value == null)
            {
                return;
            }

            var keyRow = new KeyRow { Entry = entry, Value = value, Color = value.color };
            ClickRelay relay = cell.gameObject.AddComponent<ClickRelay>();
            relay.Clicked = () => Hooks.Guard("設定の窓のキーの欄", () => StartCapture(keyRow));
            keyRows.Add(keyRow);
            refreshers.Add(() => ShowKey(keyRow));
        }

        /// <summary>
        /// 切り替えの行を写し、値の欄を矢印の所まで広げた押せる欄にする（キーと「メモを全部消す」の行）。値の文字を返す。
        /// ⚠ 写した欄（Back）の大きさを変えても、窓を開くと元の 118 に戻った（0.7.0。何が戻すかは分かっていない）ので、
        /// 同じ絵の欄を新しく作って値の文字をそこへ移し、写した欄と矢印は隠す。
        /// </summary>
        private TextMeshProUGUI WideCellRow(RectTransform content, string rowName, string name, out RectTransform cell)
        {
            cell = null;
            UISwitchButton template = Instantiate(switchTemplate, content, false);
            GameObject row = template.gameObject;
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
                Plugin.Log.LogWarning($"設定の窓: 押す欄の行の部品が引けない（{name}）");
                Destroy(row);
                return null;
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
            if (left != null)
            {
                left.gameObject.SetActive(false);
            }

            if (right != null)
            {
                right.gameObject.SetActive(false);
            }

            return value;
        }

        /// <summary>
        /// 下の段: 左に「メモを全部消す」、右に「OK」。どちらもゲームの設定の窓の「OK」のボタンを写す（幅は文字に合わせて決まる）。
        /// 「メモを全部消す」は 1 度押すと「もう一度で消す」に替わり、その間にもう一度押すと消す（字数をそろえて、OK が動かないようにする）。
        /// </summary>
        private void BottomButtons(RectTransform content)
        {
            var go = new GameObject("Buttons", typeof(RectTransform));
            go.layer = content.gameObject.layer;
            var row = (RectTransform)go.transform;
            row.SetParent(content, false);
            row.sizeDelta = new Vector2(RowWidth(), ButtonRowHeight);
            HorizontalLayoutGroup layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = ButtonGap;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            clearButton = Instantiate(buttonTemplate, row, false);
            clearButton.name = "ClearButton";
            IgnoreLocalize(clearButton.gameObject);
            ShowClear();
            refreshers.Add(DisarmClear);

            UIDialogWindowButton ok = Instantiate(buttonTemplate, row, false);
            ok.name = "OkButton";
            IgnoreLocalize(ok.gameObject);
            ok.Draw(new UIDialogWindowData.ButtonData(Close, L("btn_ok", "OK"), null, replaceForGamepad: false, GameKey.Select));
        }

        private void OnClearPressed()
        {
            Hooks.Guard("設定の窓の「メモを全部消す」", () =>
            {
                if (!clearArmed)
                {
                    clearArmed = true;
                    clearArmedUntil = Time.unscaledTime + ClearConfirmSeconds;
                    ShowClear();
                    return;
                }

                Plugin.Instance.ClearTodos("設定の窓");
                DisarmClear();
            });
        }

        private void DisarmClear()
        {
            clearArmed = false;
            ShowClear();
        }

        private void ShowClear()
        {
            if (clearButton != null)
            {
                string text = clearArmed ? Strings.ClearConfirm : Strings.ClearButton;
                clearButton.Draw(new UIDialogWindowData.ButtonData(OnClearPressed, text, null, replaceForGamepad: false, GameKey.Select));
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
                Plugin.Log.LogInfo($"設定の窓でキーを変えた: {row.Entry.Definition.Key} = {row.Entry.Value}");
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

        // ---- 小道具 ----

        private static string L(string id, string fallback)
        {
            try
            {
                return LLBase.HasL(id) ? LLBase.L(id) : fallback;
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        private float RowWidth() => switchTemplate != null ? ((RectTransform)switchTemplate.transform).sizeDelta.x : 330f;

        private RectTransform NewRow(RectTransform content, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = content.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(content, false);
            rect.sizeDelta = switchTemplate != null ? ((RectTransform)switchTemplate.transform).sizeDelta : new Vector2(330f, 16f);
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
        /// 後から変えると左上に戻る（0.7.0 の節の見出しが左に寄った。docs/game-api-notes.md の「TextStyle.ApplyStyle は揃え方を戻す」）。
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

        private sealed class KeyRow
        {
            public ConfigEntry<KeyboardShortcut> Entry;
            public TMP_Text Value;
            public Color Color;
        }
    }

    /// <summary>押されたら <see cref="Clicked"/> を呼ぶだけの部品（設定の窓のキーの欄）。</summary>
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
