using System;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using ModsMenu;

namespace QuickStash
{
    /// <summary>
    /// Mods Menu（ESC の窓の「Mods」）に Quick Stash のタブを登録する（0.3.0〜。2026-11-23 に本人の求めで、しまうキーとロックのキーを変えられるようにした）。
    /// Mods Menu は任意の依存: 入っていなければ何もせず、Quick Stash はキーと cfg だけで動く。
    /// ⚠ Mods Menu の型は NoInlining のメソッドの中でだけ触る。Mods Menu が無いときに、このクラスを使う側が型の読み込みで落ちないように。
    /// 文言の字は、ゲームの日本語の訳文（lng_ja）に出てくる字だけで書いた（2026-11-23 に数えた。書体に無い字は描かれない）。
    /// </summary>
    internal static class ModsMenuPage
    {
        public const string ModsMenuGuid = "tanmen.gk2.modsmenu";

        private static bool registered;

        /// <summary>Mods の窓が開いている間（キーを選んでいる間も）と閉じた直後。しまうキー・ロックのキーはこの間鳴らさない。</summary>
        public static bool BlocksHotkeys => registered && QueryBlocks();

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
                registered = true;
                Plugin.Log.LogInfo("Mods Menu にタブを登録した");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Mods Menu にタブを登録できなかった（Mods Menu の版が合わない可能性）: {e}");
            }
        }

        private static string T(string ja, string en) => GameBridge.Japanese ? ja : en;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool QueryBlocks() => ModsMenuApi.BlocksHotkeys;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Register(Plugin p)
        {
            ModsMenuApi.Register(p)
                .SetOrder(30)
                .Section(() => T("キー", "Keys"))
                .Key(p.StashKey, () => T("しまう", "Stash"))
                .Key(p.LockKey, () => T("ロック", "Lock"))
                .Section(() => T("しまい方", "Stashing"))
                .Toggle(p.KeepHotBarItems, () => T("ホットバーの品は残す", "Keep hotbar items"))
                .Toggle(p.Notify, () => T("右下に知らせる", "Show notice"))
                .Toggle(p.Animation, () => T("飛ぶ演出", "Flying icons"));
        }
    }
}
