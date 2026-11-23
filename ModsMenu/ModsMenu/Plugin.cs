using System;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace ModsMenu
{
    /// <summary>
    /// ESC の窓（ポーズ）に「Mods」のボタンを足し、押すと登録された mod ごとのタブで設定を変えられる窓を開く。
    /// タブはほかの mod が <see cref="ModsMenuApi.Register(BaseUnityPlugin)"/> で足す（Mods Menu 自身は設定の行を持たない）。
    /// </summary>
    [BepInPlugin(ModsMenuApi.PluginGuid, PluginName, BuildInfo.Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        /// <summary>The plugin name shown by BepInEx.</summary>
        public const string PluginName = "Mods Menu";

        internal static Plugin Instance { get; private set; }

        internal static ManualLogSource Log { get; private set; }

        /// <summary>最後に開いていたタブ（mod の GUID）。次に窓を開いたときにそのタブから出す。</summary>
        internal ConfigEntry<string> LastPage { get; private set; }

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            LastPage = Config.Bind("Window", "LastPage", string.Empty,
                "最後に開いていたタブ（mod の GUID）。次に Mods の窓を開いたときにそのタブから出す");
            ApplyPatches();
            Log.LogInfo($"{PluginName} {BuildInfo.Version} を読み込んだ（API {ModsMenuApi.ApiVersion}）");
        }

        private static void ApplyPatches()
        {
            var harmony = new Harmony(ModsMenuApi.PluginGuid);
            int ok = 0;
            int failed = 0;
            foreach (Type type in AccessTools.GetTypesFromAssembly(typeof(Plugin).Assembly))
            {
                if (!type.GetCustomAttributes(typeof(HarmonyPatch), false).Any())
                {
                    continue;
                }

                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                    ok++;
                }
                catch (Exception e)
                {
                    failed++;
                    Log.LogError($"パッチを当てられなかった（ゲームの更新で形が変わった可能性）: {type.Name}: {e.Message}");
                }
            }

            Log.LogInfo($"パッチ: 成功 {ok} / 失敗 {failed}（差し替えたメソッド {harmony.GetPatchedMethods().Count()} 個）");
        }
    }
}
