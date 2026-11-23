using System;
using BepInEx;
using LazyBearTechnology;
using UnityEngine;

namespace ModsMenu
{
    /// <summary>
    /// Entry point for other mods. Register a settings tab for your plugin in its <c>Awake</c>, then add rows bound to your
    /// <see cref="BepInEx.Configuration.ConfigEntry{T}"/>s. The tab appears in the "Mods" window opened from the pause (ESC) menu.
    /// Values are written straight to your config entries, so BepInEx saves them to your .cfg and fires <c>SettingChanged</c>.
    /// </summary>
    /// <example>
    /// <code>
    /// [BepInPlugin("you.yourmod", "Your Mod", "1.0.0")]
    /// [BepInDependency(ModsMenuApi.PluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
    /// public class YourPlugin : BaseUnityPlugin
    /// {
    ///     void Awake()
    ///     {
    ///         var key = Config.Bind("Keys", "Action", new KeyboardShortcut(KeyCode.H), "...");
    ///         var on = Config.Bind("General", "Enabled", true, "...");
    ///         ModsMenuApi.Register(this)
    ///             .Section("General")
    ///             .Toggle(on, "Enabled")
    ///             .Key(key, "Action key");
    ///     }
    /// }
    /// </code>
    /// </example>
    public static class ModsMenuApi
    {
        /// <summary>The BepInEx GUID of Mods Menu (for <c>[BepInDependency]</c>).</summary>
        public const string PluginGuid = "tanmen.gk2.modsmenu";

        /// <summary>Bumped only when the API changes in a way callers can detect. Current: 1.</summary>
        public const int ApiVersion = 1;

        /// <summary>Adds (or replaces) the tab of <paramref name="plugin"/>. The tab title is the plugin's name.</summary>
        public static ModPage Register(BaseUnityPlugin plugin) => Register(plugin, (Func<string>)null);

        /// <summary>Adds (or replaces) the tab of <paramref name="plugin"/> with a fixed title.</summary>
        public static ModPage Register(BaseUnityPlugin plugin, string title) =>
            Register(plugin, string.IsNullOrEmpty(title) ? null : (Func<string>)(() => title));

        /// <summary>
        /// Adds (or replaces) the tab of <paramref name="plugin"/>. <paramref name="title"/> is read every time the window is built
        /// (the window is rebuilt when the game language changes), so it may return a translated text.
        /// </summary>
        public static ModPage Register(BaseUnityPlugin plugin, Func<string> title)
        {
            if (plugin == null)
            {
                throw new ArgumentNullException(nameof(plugin));
            }

            return Registry.Add(plugin, title);
        }

        /// <summary>Removes the tab of <paramref name="plugin"/>. Returns false if it was not registered.</summary>
        public static bool Unregister(BaseUnityPlugin plugin) => plugin != null && Registry.Remove(plugin.Info?.Metadata?.GUID);

        /// <summary>True while the Mods window is shown.</summary>
        public static bool IsOpen => MenuWindow.IsShownNow;

        /// <summary>
        /// True while the Mods window is shown (including while it waits for a key to assign) and for a moment after it closes.
        /// Skip your own hotkeys while this is true, so that the key pressed in the window does not also trigger them.
        /// </summary>
        public static bool BlocksHotkeys => MenuWindow.BlocksHotkeys;

        /// <summary>The game's current language code (for example "en", "ja"). "en" if it cannot be read.</summary>
        public static string Language => GameBridge.CurrentLang();

        /// <summary>
        /// Text of a game localization key, or <paramref name="fallback"/> if the key does not exist
        /// (for example <c>Localize("ui_on", "On")</c>).
        /// </summary>
        public static string Localize(string key, string fallback) => GameBridge.L(key, fallback);
    }

    /// <summary>ゲームの言語と訳文。</summary>
    internal static class GameBridge
    {
        public static string CurrentLang()
        {
            try
            {
                return LLBase.CurrentLang;
            }
            catch (Exception)
            {
                return "en";
            }
        }

        public static string L(string id, string fallback)
        {
            if (string.IsNullOrEmpty(id))
            {
                return fallback;
            }

            try
            {
                return LLBase.HasL(id) ? LLBase.L(id) : fallback;
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        /// <summary>ゲームの UI の根（倍率と表示・非表示がゲームの UI と揃う）。ゲームの Workshop Creator の窓と同じ取り方。</summary>
        public static Transform FindUiRoot()
        {
            GUIElements gui = GUIElements.Instance;
            if (gui == null)
            {
                return null;
            }

            Component fitter = HarmonyLib.Traverse.Create(gui).Field("uiFitter").GetValue<Component>();
            return fitter != null ? fitter.transform : gui.Root;
        }

        /// <summary>ゲームのボタンと同じ音（LazyAudio の "gui_click"）。</summary>
        public static void PlayClick() => PlaySound("gui_click");

        /// <summary>ゲームの効果音（"gui_click"・"tab_click" など）。</summary>
        public static void PlaySound(string id)
        {
            try
            {
                LazyAudio.PlayAndForget(id);
            }
            catch (Exception)
            {
                // 音が鳴らなくても押した処理は済む
            }
        }
    }
}
