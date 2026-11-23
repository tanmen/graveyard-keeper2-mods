using System;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace StockFinder
{
    /// <summary>
    /// アイテムのツールチップの一番下に、その品が「どこの宝箱に何個あるか」と「合計で何個あるか」を足す。
    /// 合計は所持品と宝箱の全部。宝箱は、ゲームが所持品の窓でまとめて開く収納（WGODef.OpenInMultiInventory）。
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, BuildInfo.Version)]
    [BepInDependency(ModsMenuPage.ModsMenuGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "tanmen.gk2.stockfinder";
        public const string PluginName = "Stock Finder";

        internal static Plugin Instance { get; private set; }

        internal static ManualLogSource Log { get; private set; }

        internal ConfigEntry<bool> Enabled { get; private set; }

        internal ConfigEntry<int> MaxRows { get; private set; }

        internal ConfigEntry<StockLayout> Layout { get; private set; }

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            BindConfig();
            ApplyPatches();
            ModsMenuPage.TryRegister(this);
            Log.LogInfo($"{PluginName} {BuildInfo.Version} を読み込んだ（並べ方 {Layout.Value}・行の上限 {MaxRows.Value}）");
        }

        private void BindConfig()
        {
            Enabled = Config.Bind("Display", "Enabled", true,
                "アイテムのツールチップに、どこの宝箱に何個あるかと合計を足す");
            MaxRows = Config.Bind("Display", "MaxRows", 8,
                new ConfigDescription("場所の行の上限（所持品と「ほか N か所」の行も含む。超えた分は「ほか N か所」の 1 行にまとめる）",
                    new AcceptableValueRange<int>(1, 30)));
            Layout = Config.Bind("Display", "Layout", StockLayout.Table,
                "Table: 場所を見出しの行にして、その下に宝箱ごとに 1 行（宝箱の名前を字下げ・数を右揃え） / Lines: 宝箱ごとに 1 行で「場所 宝箱: 数」を中央揃え（列がずれるときの逃げ道）");
        }

        private static void ApplyPatches()
        {
            var harmony = new Harmony(PluginGuid);
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

    internal enum StockLayout
    {
        Table,
        Lines,
    }
}
