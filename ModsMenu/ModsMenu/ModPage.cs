using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using ModsMenu.Core;

namespace ModsMenu
{
    /// <summary>
    /// One tab of the Mods window. Rows are shown top to bottom in the order they are added; buttons go to the bottom row,
    /// left of "OK". Every method returns the same page so calls can be chained.
    /// Labels can be fixed strings or <see cref="Func{TResult}"/>s that are read each time the window is built
    /// (it is rebuilt when the game language changes).
    /// </summary>
    /// <remarks>
    /// The game's fonts only contain the characters the game itself uses. Characters outside them are drawn as nothing
    /// (for example "・" in Japanese), so prefer plain text.
    /// </remarks>
    public sealed class ModPage
    {
        internal ModPage(string guid, string pluginName, Func<string> title, int sequence)
        {
            Guid = guid;
            PluginName = pluginName;
            title1 = title;
            Sequence = sequence;
        }

        private readonly Func<string> title1;

        internal readonly List<MenuRow> Rows = new List<MenuRow>();

        internal readonly List<MenuButton> Buttons = new List<MenuButton>();

        /// <summary>The BepInEx GUID of the plugin this tab belongs to.</summary>
        public string Guid { get; }

        internal string PluginName { get; }

        internal int Sequence { get; }

        internal int SortOrder { get; private set; } = 100;

        internal string Title()
        {
            string text = title1 == null ? null : Safe(title1);
            return string.IsNullOrEmpty(text) ? PluginName : text;
        }

        /// <summary>Position among the tabs (smaller first; default 100; ties keep the order of registration).</summary>
        public ModPage SetOrder(int order)
        {
            SortOrder = order;
            Registry.Touch();
            return this;
        }

        // ---- 節の見出し ----

        /// <summary>A section heading (centered text between two thin lines).</summary>
        public ModPage Section(string text) => Section(Fixed(text));

        /// <inheritdoc cref="Section(string)"/>
        public ModPage Section(Func<string> text) => Add(new MenuRow(MenuRowKind.Section, text));

        // ---- オン / オフ ----

        /// <summary>An off / on switch (the game's "V-Sync" row).</summary>
        public ModPage Toggle(ConfigEntry<bool> entry, string label) => Toggle(entry, Fixed(label));

        /// <inheritdoc cref="Toggle(ConfigEntry{bool}, string)"/>
        public ModPage Toggle(ConfigEntry<bool> entry, Func<string> label)
        {
            Require(entry, nameof(entry));
            return Add(new MenuRow(MenuRowKind.Toggle, label)
            {
                Entry = entry,
                GetIndex = () => entry.Value ? 1 : 0,
                SetIndex = i => entry.Value = i == 1,
            });
        }

        // ---- 数 ----

        /// <summary>
        /// A whole number with − / + buttons and a slider (the game's volume row). The range is taken from the entry's
        /// <see cref="AcceptableValueRange{T}"/>; an <see cref="ArgumentException"/> is thrown if it has none.
        /// </summary>
        public ModPage Number(ConfigEntry<int> entry, string label) => Number(entry, Fixed(label));

        /// <inheritdoc cref="Number(ConfigEntry{int}, string)"/>
        public ModPage Number(ConfigEntry<int> entry, Func<string> label)
        {
            Require(entry, nameof(entry));
            if (!(entry.Description?.AcceptableValues is AcceptableValueRange<int> range))
            {
                throw new ArgumentException($"{entry.Definition} has no AcceptableValueRange<int>; pass min and max", nameof(entry));
            }

            return Number(entry, label, range.MinValue, range.MaxValue);
        }

        /// <summary>A whole number from <paramref name="min"/> to <paramref name="max"/> in steps of 1.</summary>
        public ModPage Number(ConfigEntry<int> entry, string label, int min, int max) => Number(entry, Fixed(label), min, max);

        /// <inheritdoc cref="Number(ConfigEntry{int}, string, int, int)"/>
        public ModPage Number(ConfigEntry<int> entry, Func<string> label, int min, int max)
        {
            Require(entry, nameof(entry));
            return Add(new MenuRow(MenuRowKind.Number, label)
            {
                Entry = entry,
                Scale = new StepScale(min, max, 1),
                GetNumber = () => entry.Value,
                SetNumber = v => entry.Value = (int)Math.Round(v),
            });
        }

        /// <summary>A number from <paramref name="min"/> to <paramref name="max"/> in steps of <paramref name="step"/>.</summary>
        public ModPage Number(ConfigEntry<float> entry, string label, float min, float max, float step = 1f) =>
            Number(entry, Fixed(label), min, max, step);

        /// <inheritdoc cref="Number(ConfigEntry{float}, string, float, float, float)"/>
        public ModPage Number(ConfigEntry<float> entry, Func<string> label, float min, float max, float step = 1f)
        {
            Require(entry, nameof(entry));
            return Add(new MenuRow(MenuRowKind.Number, label)
            {
                Entry = entry,
                Scale = new StepScale(min, max, step),
                GetNumber = () => entry.Value,
                SetNumber = v => entry.Value = (float)v,
            });
        }

        // ---- 選ぶ ----

        /// <summary>
        /// One of the values of an enum, switched with the arrows (the game's "Graphics" row).
        /// <paramref name="names"/> gives the text of each value (default: the value's name).
        /// </summary>
        public ModPage Choice<T>(ConfigEntry<T> entry, string label, Func<T, string> names = null) where T : struct, Enum =>
            Choice(entry, Fixed(label), names);

        /// <inheritdoc cref="Choice{T}(ConfigEntry{T}, string, Func{T, string})"/>
        public ModPage Choice<T>(ConfigEntry<T> entry, Func<string> label, Func<T, string> names = null) where T : struct, Enum =>
            Choice(entry, label, (T[])Enum.GetValues(typeof(T)), names);

        /// <summary>One of <paramref name="values"/>, switched with the arrows.</summary>
        public ModPage Choice<T>(ConfigEntry<T> entry, string label, IList<T> values, Func<T, string> names) =>
            Choice(entry, Fixed(label), values, names);

        /// <inheritdoc cref="Choice{T}(ConfigEntry{T}, string, IList{T}, Func{T, string})"/>
        public ModPage Choice<T>(ConfigEntry<T> entry, Func<string> label, IList<T> values, Func<T, string> names)
        {
            Require(entry, nameof(entry));
            if (values == null || values.Count == 0)
            {
                throw new ArgumentException("values must not be empty", nameof(values));
            }

            T[] copy = values.ToArray();
            EqualityComparer<T> comparer = EqualityComparer<T>.Default;
            return Add(new MenuRow(MenuRowKind.Choice, label)
            {
                Entry = entry,
                Names = () => copy.Select(v => names == null ? v.ToString() : Safe(() => names(v)) ?? v.ToString()).ToArray(),
                GetIndex = () => Array.FindIndex(copy, v => comparer.Equals(v, entry.Value)),
                SetIndex = i => entry.Value = copy[Math.Max(0, Math.Min(copy.Length - 1, i))],
            });
        }

        // ---- キー ----

        /// <summary>
        /// A key or mouse button with optional Shift / Ctrl / Alt. Clicking the field waits for the next key; Esc cancels.
        /// Left click and gamepad buttons cannot be assigned.
        /// </summary>
        public ModPage Key(ConfigEntry<KeyboardShortcut> entry, string label) => Key(entry, Fixed(label));

        /// <inheritdoc cref="Key(ConfigEntry{KeyboardShortcut}, string)"/>
        public ModPage Key(ConfigEntry<KeyboardShortcut> entry, Func<string> label)
        {
            Require(entry, nameof(entry));
            return Add(new MenuRow(MenuRowKind.Key, label) { Entry = entry, Shortcut = entry });
        }

        // ---- 下の段のボタン ----

        /// <summary>
        /// A button in the bottom row, left of "OK". With <paramref name="confirmLabel"/>, the first click only changes the text
        /// to it and a second click within 3 seconds runs <paramref name="onClick"/> (for destructive actions).
        /// </summary>
        public ModPage Button(string label, Action onClick, string confirmLabel = null) =>
            Button(Fixed(label), onClick, confirmLabel == null ? null : Fixed(confirmLabel));

        /// <inheritdoc cref="Button(string, Action, string)"/>
        public ModPage Button(Func<string> label, Action onClick, Func<string> confirmLabel = null)
        {
            if (onClick == null)
            {
                throw new ArgumentNullException(nameof(onClick));
            }

            Buttons.Add(new MenuButton { Label = label ?? Fixed(string.Empty), ConfirmLabel = confirmLabel, OnClick = onClick });
            Registry.Touch();
            return this;
        }

        // ---- 小道具 ----

        private ModPage Add(MenuRow row)
        {
            Rows.Add(row);
            Registry.Touch();
            return this;
        }

        private static Func<string> Fixed(string text) => () => text ?? string.Empty;

        private static void Require(object entry, string name)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(name);
            }
        }

        internal static string Safe(Func<string> text)
        {
            try
            {
                return text();
            }
            catch (Exception e)
            {
                Plugin.Log?.LogWarning($"文言を作れなかった: {e.Message}");
                return null;
            }
        }
    }

    internal enum MenuRowKind
    {
        Section,
        Toggle,
        Number,
        Choice,
        Key,
    }

    /// <summary>ページの 1 行（窓はこれを見てゲームの部品を写して並べる）。</summary>
    internal sealed class MenuRow
    {
        public MenuRow(MenuRowKind kind, Func<string> label)
        {
            Kind = kind;
            Label = label ?? (() => string.Empty);
        }

        public MenuRowKind Kind { get; }

        public Func<string> Label { get; }

        public ConfigEntryBase Entry;

        public Func<int> GetIndex;

        public Action<int> SetIndex;

        public Func<string[]> Names;

        public StepScale Scale;

        public Func<double> GetNumber;

        public Action<double> SetNumber;

        public ConfigEntry<KeyboardShortcut> Shortcut;
    }

    internal sealed class MenuButton
    {
        public Func<string> Label;

        public Func<string> ConfirmLabel;

        public Action OnClick;
    }

    /// <summary>登録されたページ。プラグインの GUID ごとに 1 つ（登録し直したら差し替える）。</summary>
    internal static class Registry
    {
        private static readonly List<ModPage> Pages = new List<ModPage>();

        private static int sequence;

        /// <summary>ページか行が変わるたびに増える（窓は開くときにこれを見て組み直す）。</summary>
        public static int Version { get; private set; }

        public static ModPage Add(BaseUnityPlugin plugin, Func<string> title)
        {
            BepInPlugin meta = plugin.Info?.Metadata ?? MetadataHelper.GetMetadata(plugin);
            string guid = meta?.GUID ?? plugin.GetType().FullName;
            string name = meta?.Name ?? plugin.GetType().Name;
            int replaced = Pages.RemoveAll(p => p.Guid == guid);
            var page = new ModPage(guid, name, title, sequence++);
            Pages.Add(page);
            Touch();
            Plugin.Log?.LogInfo($"タブを{(replaced > 0 ? "登録し直した" : "登録した")}: {name}（{guid}）");
            return page;
        }

        public static bool Remove(string guid)
        {
            if (guid == null || Pages.RemoveAll(p => p.Guid == guid) == 0)
            {
                return false;
            }

            Touch();
            return true;
        }

        public static void Touch() => Version++;

        public static int Count => Pages.Count;

        public static List<ModPage> Ordered() => Pages.OrderBy(p => p.SortOrder).ThenBy(p => p.Sequence).ToList();
    }
}
