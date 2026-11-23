using System;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using ModsMenu;

namespace StockFinder
{
    /// <summary>
    /// Mods Menu（ESC の窓の「Mods」）に Stock Finder のタブを登録する（0.2.0〜）。
    /// Mods Menu は任意の依存: 入っていなければ何もせず、Stock Finder は cfg だけで動く。
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

        private static string T(string ja, string en) => Strings.CurrentLang() == "ja" ? ja : en;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Register(Plugin p)
        {
            ModsMenuApi.Register(p)
                .SetOrder(20)
                .Section(() => T("表示", "Display"))
                .Toggle(p.Enabled, () => T("ツールチップに足す", "Add to tooltips"))
                .Number(p.MaxRows, () => T("行の上限", "Max rows"))
                .Choice(p.Layout, () => T("並べ方", "Layout"),
                    layout => layout == StockLayout.Table ? T("列をそろえる", "Table") : T("中央にそろえる", "Centered"));
        }
    }
}
