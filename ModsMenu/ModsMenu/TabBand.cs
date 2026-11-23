using System;
using System.Collections.Generic;
using System.Reflection;
using LazyBearTechnology;
using ModsMenu.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ModsMenu
{
    /// <summary>
    /// Mods の窓の見出しの帯に並べる木のタブ（2026-11-23 に本人が選んだ形）。
    /// ゲームの人物の窓（CharacterWindow。CharacterWindow_Big / _Small の prefab）の見出しの帯 `Content/HeaderGroup/Windows Tabs Content` を写す:
    /// 横並び（間 -1）で幅は中身に合わせ、両端に宝石の飾り（DecorLeft / DecorRight 60x26。帯の端から 39 はみ出す）、
    /// タブ（CharPageTabButton）は文字の幅＋左右 13。選んだタブは ActiveBack を出して押せなくする（CharPageTabButton.UpdateState）。
    /// 人物の窓は開いたときに Addressables で読まれるので、LazyUI.GetWindow で読み込んでから部品を引く（ゲームが開くときと同じ読み方）。
    /// </summary>
    internal sealed class TabBand
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // タブの帯が見出しの帯の中で避ける幅（右の閉じるボタン 26 ＋ 間 4。中央に置くので左右とも空ける）
        private const float CloseReserve = 30f;

        // あふれたときの矢印の幅（設定の窓の行の矢印 25）
        private const float ArrowWidth = 25f;

        private readonly RectTransform root;
        private readonly CharPageTabButton template;
        private readonly List<CharPageTabButton> tabs = new List<CharPageTabButton>();
        private readonly List<float> widths = new List<float>();
        private GameObject prevHelper;
        private GameObject nextHelper;
        private LazyButton leftArrow;
        private LazyButton rightArrow;
        private float spacing;
        private float overhang;
        private int first;
        private int active;

        private TabBand(RectTransform root, CharPageTabButton template)
        {
            this.root = root;
            this.template = template;
        }

        public int Count => tabs.Count;

        /// <summary>選ばれた（押された）タブの番号を知らせる。</summary>
        public Action<int> Selected;

        /// <summary>人物の窓の帯を写して <paramref name="header"/>（見出しの帯）に入れる。引けなければ null（窓は切り替えの行で代える）。</summary>
        public static TabBand TryCreate(RectTransform header, UISwitchButton arrowSource)
        {
            CharacterWindow window;
            try
            {
                window = LazyUI.GetWindow<CharacterWindow>();
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"タブ: 人物の窓を読み込めなかった: {e.Message}");
                return null;
            }

            GameObject source = Field<GameObject>(window, "charPageTabButtonParent");
            CharPageTabButton prefab = Field<CharPageTabButton>(window, "charPageTabButtonPrefab");
            if (window == null || source == null || prefab == null)
            {
                Plugin.Log.LogWarning($"タブ: 人物の窓の帯が引けない（窓 {window != null}・帯 {source != null}・タブ {prefab != null}）");
                return null;
            }

            // 写すあいだは働かせない（タブの Awake・OnEnable を走らせない）。中のタブは消し、手本の prefab から作り直す
            var holder = new GameObject("TabBandHolder", typeof(RectTransform));
            holder.SetActive(false);
            holder.transform.SetParent(header, false);
            GameObject copy = UnityEngine.Object.Instantiate(source, holder.transform, false);
            copy.name = "ModsMenuTabs";
            foreach (CharPageTabButton old in copy.GetComponentsInChildren<CharPageTabButton>(true))
            {
                UnityEngine.Object.DestroyImmediate(old.gameObject);
            }

            var band = new TabBand((RectTransform)copy.transform, prefab);
            band.prevHelper = Find(copy.transform, "PrevTabGamepadHelper");
            band.nextHelper = Find(copy.transform, "NextTabGamepadHelper");
            HorizontalLayoutGroup layout = copy.GetComponent<HorizontalLayoutGroup>();
            band.spacing = layout != null ? layout.spacing : -1f;
            band.overhang = Math.Max(Overhang(Find(copy.transform, "DecorLeft"), left: true), Overhang(Find(copy.transform, "DecorRight"), left: false));
            band.leftArrow = band.CreateArrow(arrowSource, "ToLeft", "ArrowLeft", -1);
            band.rightArrow = band.CreateArrow(arrowSource, "ToRight", "ArrowRight", 1);

            copy.transform.SetParent(header, false);
            copy.transform.SetAsLastSibling();
            UnityEngine.Object.Destroy(holder);
            Plugin.Log.LogInfo($"タブ: 人物の窓の帯を写した（{source.transform.root.name}・間 {band.spacing}・飾りのはみ出し {band.overhang}・"
                + $"矢印 {(band.leftArrow != null && band.rightArrow != null ? "あり" : "なし")}）");
            return band;
        }

        /// <summary>タブを作る。文字は窓を組むたびに入れ直す（言語を変えたら窓ごと組み直す）。</summary>
        public void Build(IList<string> titles)
        {
            for (int i = 0; i < titles.Count; i++)
            {
                int index = i;
                CharPageTabButton tab = UnityEngine.Object.Instantiate(template, root, false);
                tab.name = "Tab" + i;
                IgnoreLocalize(tab.gameObject);
                // 名前（訳文の鍵）は渡さない: 渡すと OnEnable のたびに LLBase.L(名前) で文字を書き換えられる
                tab.Init(null, () => Hooks.Guard("タブ", () => Selected?.Invoke(index)), i == titles.Count - 1);
                TMP_Text label = Field<TextMeshProUGUI>(tab, "label");
                if (label != null)
                {
                    label.text = titles[i];
                }

                tab.gameObject.SetActive(true);
                tab.UpdateState(false);
                tabs.Add(tab);
            }

            if (leftArrow != null)
            {
                leftArrow.transform.SetAsFirstSibling();
            }

            if (rightArrow != null)
            {
                rightArrow.transform.SetAsLastSibling();
            }

            Measure();
        }

        /// <summary>
        /// 窓を広げられなかったとき、<paramref name="lack"/> だけ縮むよう、タブの文字の左右の余白（ゲームは 13）を詰める（5 まで）。
        /// 詰めても入らない分は矢印で送る。
        /// </summary>
        public void Compact(float lack)
        {
            if (tabs.Count == 0 || lack <= 0f)
            {
                return;
            }

            int cut = Mathf.CeilToInt(lack / (2f * tabs.Count));
            var paddings = new List<string>();
            foreach (CharPageTabButton tab in tabs)
            {
                // 余白はタブの中の Content の並べ方（LayoutGroup。縦か横かは問わない）にある。
                // 0.1.1 は HorizontalLayoutGroup だけを探して引けず、何も詰めなかった（2026-11-23 のログで左右の余白が空）
                Transform inner = tab.transform.Find("Content");
                LayoutGroup layout = inner != null ? inner.GetComponent<LayoutGroup>() : null;
                if (layout == null)
                {
                    continue;
                }

                layout.padding.left = Math.Max(MinPadding, layout.padding.left - cut);
                layout.padding.right = Math.Max(MinPadding, layout.padding.right - cut);
                paddings.Add(layout.padding.left + "/" + layout.padding.right);
            }

            Plugin.Log.LogInfo($"タブ: 余白を詰めた（足りない幅 {lack}・左右の余白 {string.Join(" ", paddings)}）");
            Measure();
        }

        // タブの文字の左右の余白を詰めるときの下限
        private const int MinPadding = 5;

        /// <summary>帯がこの幅（見出しの帯の幅）に全部入るのに要る幅。足りなければ窓を広げる目安。</summary>
        public float RequiredHeaderWidth() => TabStrip.Span(widths, 0, widths.Count, spacing) + 2f * (overhang + CloseReserve);

        /// <summary><paramref name="index"/> のタブを選んだ見た目にし、入りきらなければ見える範囲だけ出す。</summary>
        public void Show(int index, float headerWidth)
        {
            active = index;
            float available = headerWidth - 2f * (overhang + CloseReserve);
            TabWindow window = TabStrip.Fit(widths, available, spacing, 2f * (ArrowWidth + spacing), index, first);
            first = window.First;
            Apply(window);
            if (!logged)
            {
                // 窓を広げたあとの実際の幅（広げが効いたか・矢印が出たかの手掛かり）
                logged = true;
                Plugin.Log.LogInfo($"タブ: 開いたときの見出しの帯の幅 {headerWidth}・並べる範囲 {window}");
            }
        }

        private bool logged;

        /// <summary>矢印で範囲を送る（選んでいるタブは替えない）。</summary>
        private void Scroll(int step)
        {
            float headerWidth = ((RectTransform)root.parent).rect.width;
            float available = headerWidth - 2f * (overhang + CloseReserve);
            first = TabStrip.Scroll(widths, available, spacing, 2f * (ArrowWidth + spacing), first, step);
            int count = TabStrip.Fitting(widths, first, Math.Max(0f, available - 2f * (ArrowWidth + spacing)), spacing);
            Apply(new TabWindow(first, Math.Max(1, count), true));
        }

        /// <summary>ゲームパッドのときだけ LB / RB の印を出す（人物の窓と同じ）。</summary>
        public void UpdateGamepad(bool gamepad)
        {
            prevHelper?.SetActive(gamepad);
            nextHelper?.SetActive(gamepad);
        }

        private void Apply(TabWindow window)
        {
            for (int i = 0; i < tabs.Count; i++)
            {
                bool visible = window.Contains(i);
                tabs[i].gameObject.SetActive(visible);
                if (visible)
                {
                    tabs[i].UpdateState(i == active);
                    // 区切りは見えている最後のタブの右には出さない
                    GameObject separator = Field<GameObject>(tabs[i], "rightSeparator");
                    separator?.SetActive(i < window.First + window.Count - 1);
                }
            }

            leftArrow?.gameObject.SetActive(window.Overflow);
            rightArrow?.gameObject.SetActive(window.Overflow);
            if (window.Overflow)
            {
                leftArrow.interactable = window.First > 0;
                rightArrow.interactable = window.First + window.Count < tabs.Count;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
        }

        /// <summary>タブごとの幅（文字の幅＋左右の余白。ゲームの ContentSizeFitter が決める値）を測る。</summary>
        private void Measure()
        {
            widths.Clear();
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            foreach (CharPageTabButton tab in tabs)
            {
                var rect = (RectTransform)tab.transform;
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                float width = LayoutUtility.GetPreferredWidth(rect);
                widths.Add(width > 0f ? width : rect.rect.width);
            }

            Plugin.Log.LogInfo($"タブの幅: {string.Join(" / ", widths)}（帯に全部入るのに要る見出しの幅 {RequiredHeaderWidth()}）");
        }

        private LazyButton CreateArrow(UISwitchButton source, string childName, string name, int step)
        {
            Transform child = source == null ? null : source.transform.Find(childName);
            if (child == null)
            {
                return null;
            }

            GameObject go = UnityEngine.Object.Instantiate(child.gameObject, root, false);
            go.name = name;
            LazyButton button = go.GetComponent<LazyButton>();
            if (button == null)
            {
                UnityEngine.Object.DestroyImmediate(go);
                return null;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => Hooks.Guard("タブの矢印", () => Scroll(step)));
            LayoutElement element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            element.ignoreLayout = false;
            element.preferredWidth = ArrowWidth;
            element.minWidth = ArrowWidth;
            go.SetActive(false);
            return button;
        }

        private static float Overhang(GameObject decor, bool left)
        {
            if (decor == null)
            {
                return 0f;
            }

            var rect = (RectTransform)decor.transform;
            float x = rect.anchoredPosition.x;
            float w = rect.sizeDelta.x;
            // 帯の端から外へ出る分（左なら左端より左、右なら右端より右）
            return Math.Max(0f, left ? -(x - w * rect.pivot.x) : x + w * (1f - rect.pivot.x));
        }

        private static GameObject Find(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            return t != null ? t.gameObject : null;
        }

        private static void IgnoreLocalize(GameObject go)
        {
            foreach (LocalizedLabel label in go.GetComponentsInChildren<LocalizedLabel>(true))
            {
                label.IgnoreLocalize = true;
            }
        }

        private static T Field<T>(object target, string name) where T : class
        {
            Type type = target?.GetType();
            while (type != null)
            {
                FieldInfo field = type.GetField(name, AnyInstance);
                if (field != null)
                {
                    return field.GetValue(target) as T;
                }

                type = type.BaseType;
            }

            return null;
        }
    }
}
