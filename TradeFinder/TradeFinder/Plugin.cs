using System;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using TradeFinder.Core;

namespace TradeFinder
{
    /// <summary>
    /// アイテムのツールチップの一番下に、その品を「どこで買えるか」「どこで売れるか」を足す。
    /// 商人はゲームの定義（GameBalance.vendorDefs）とセーブの商人（段階・在庫）から引き、町の店はまだ建てていなければ「未建設」と出す。
    /// 出す・出さないは項目ごとに「いつも / キー（既定 Shift）を押している間 / 出さない」から選ぶ（Mods Menu のタブか cfg）。
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, BuildInfo.Version)]
    [BepInDependency(ModsMenuPage.ModsMenuGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "tanmen.gk2.tradefinder";
        public const string PluginName = "Trade Finder";

        internal static Plugin Instance { get; private set; }

        internal static ManualLogSource Log { get; private set; }

        internal ConfigEntry<ShowWhen> Buy { get; private set; }

        internal ConfigEntry<ShowWhen> Sell { get; private set; }

        internal ConfigEntry<ShowWhen> Unavailable { get; private set; }

        internal ConfigEntry<ShowWhen> Place { get; private set; }

        internal ConfigEntry<ShowWhen> Price { get; private set; }

        internal ConfigEntry<ShowWhen> Happiness { get; private set; }

        internal ConfigEntry<HoldKey> Key { get; private set; }

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            BindConfig();
            ApplyPatches();
            gameObject.AddComponent<KeyWatcher>();
            ModsMenuPage.TryRegister(this);
            Log.LogInfo($"{PluginName} {BuildInfo.Version} を読み込んだ（{Describe()}）");
        }

        /// <summary>いまの設定と、キーを押しているか。</summary>
        internal TradeOptions Options(bool keyHeld) => new TradeOptions
        {
            Buy = Buy.Value,
            Sell = Sell.Value,
            Unavailable = Unavailable.Value,
            Place = Place.Value,
            Price = Price.Value,
            Happiness = Happiness.Value,
            KeyHeld = keyHeld,
        };

        internal string Describe() =>
            $"買える {Buy.Value}・売れる {Sell.Value}・まだの所 {Unavailable.Value}・場所 {Place.Value}・値段 {Price.Value}・幸福 {Happiness.Value}・キー {Key.Value}";

        private void BindConfig()
        {
            const string when = "Always: いつも出す / WhileKey: Keys.HoldKey のキーを押している間だけ出す / Never: 出さない";
            Buy = Config.Bind("Display", "Buy", ShowWhen.Always, "「買える」（その品を売っている商人）を出すか。" + when);
            Sell = Config.Bind("Display", "Sell", ShowWhen.Always, "「売れる」（その品を買い取る商人）を出すか。" + when);
            Unavailable = Config.Bind("Display", "Unavailable", ShowWhen.Always,
                "まだ取引できない所（町の店を建てていない・商人の段階が上がると扱う）も、薄い字で出すか。" + when);
            Place = Config.Bind("Display", "Place", ShowWhen.Always, "商人の名前に、居る場所（右上の地名の枠と同じ名前）を添えるか。" + when);
            Price = Config.Bind("Display", "Price", ShowWhen.Always, "いまの在庫での 1 個の値段（商人の窓のセルと同じ）を出すか。" + when);
            Happiness = Config.Bind("Display", "Happiness", ShowWhen.Always, "売ると幸福が入る商人に、幸福のアイコンを付けるか。" + when);
            Key = Config.Bind("Keys", "HoldKey", HoldKey.Shift, "WhileKey の項目を出すキー（左右どちらでも）。Shift / Ctrl / Alt");
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

    /// <summary>WhileKey の項目を出すキー。</summary>
    internal enum HoldKey
    {
        Shift,
        Ctrl,
        Alt,
    }
}
