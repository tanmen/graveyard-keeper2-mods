using System;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using ModsMenu;
using TradeFinder.Core;

namespace TradeFinder
{
    /// <summary>
    /// Mods Menu（ESC の窓の「Mods」）に Trade Finder のタブを登録する。
    /// Mods Menu は任意の依存: 入っていなければ何もせず、Trade Finder は cfg だけで動く。
    /// ⚠ Mods Menu の型は NoInlining のメソッドの中でだけ触る。Mods Menu が無いときに、このクラスを使う側が型の読み込みで落ちないように。
    /// 文言の字は、ゲームの日本語の訳文（lng_ja）に出てくる字だけで書いた（2026-11-23 に数えた。書体に無い字は描かれない）。
    /// </summary>
    internal static class ModsMenuPage
    {
        public const string ModsMenuGuid = "tanmen.gk2.modsmenu";

        public static void TryRegister(Plugin plugin)
        {
            if (!Chainloader.PluginInfos.ContainsKey(ModsMenuGuid))
            {
                Plugin.Log.LogInfo("Mods Menu が入っていないので、設定は cfg だけで変える");
                return;
            }

            try
            {
                Register(plugin);
                Plugin.Log.LogInfo("Mods Menu にタブを登録した");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Mods Menu にタブを登録できなかった（Mods Menu の版が合わない可能性）: {e}");
            }
        }

        private static string T(string ja, string en) => Strings.Japanese ? ja : en;

        /// <summary>選ぶ値の文字（キーの名前は Keys.HoldKey の値。窓を開き直すと替わる）。</summary>
        private static string WhenName(ShowWhen when, Plugin p)
        {
            switch (when)
            {
                case ShowWhen.Always:
                    return T("いつも", "Always");
                case ShowWhen.WhileKey:
                    return T(p.Key.Value + " を押している間", "While " + p.Key.Value + " is held");
                default:
                    return T("出さない", "Hidden");
            }
        }

        // 並び: 何を出すか（買える・売れる・まだの所）→ 行に添えるもの（場所・値段・幸福）→ キー。Craft Todo 10・Stock Finder 20・Quick Stash 30 の次
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Register(Plugin p)
        {
            Func<ShowWhen, string> when = w => WhenName(w, p);
            ModsMenuApi.Register(p)
                .SetOrder(40)
                .Section(() => T("出す所", "Shown vendors"))
                .Choice(p.Buy, () => T("買える所", "Where to buy"), when)
                .Choice(p.Sell, () => T("売れる所", "Where to sell"), when)
                .Choice(p.Unavailable, () => T("未建設の店と先のステージ", "Not built / later tiers"), when)
                .Section(() => T("行に添えるもの", "Details"))
                .Choice(p.Place, () => T("居る場所", "Location"), when)
                .Choice(p.Price, () => T("値段", "Price"), when)
                .Choice(p.Happiness, () => T("幸福のアイコン", "Happiness icon"), when)
                .Section(() => T("キー", "Key"))
                .Choice(p.Key, () => T("押している間に出すキー", "Hold key"), key => key.ToString());
        }
    }
}
