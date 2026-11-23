using System;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using ModsMenu;

namespace CraftTodo
{
    /// <summary>
    /// Mods Menu（ESC の窓の「Mods」）に Craft Todo のタブを登録する（0.8.0〜。0.7.0〜0.7.5 の「制作メニューの設定」の窓の行をそのまま移した）。
    /// Mods Menu は任意の依存: 入っていなければ何もせず、Craft Todo はキーと cfg だけで動く。
    /// ⚠ Mods Menu の型は NoInlining のメソッドの中でだけ触る。Mods Menu が無いときに、このクラスを使う側が型の読み込みで落ちないように。
    /// </summary>
    internal static class ModsMenuPage
    {
        public const string ModsMenuGuid = "tanmen.gk2.modsmenu";

        private static bool registered;

        /// <summary>Mods の窓が開いている間（キーを選んでいる間も）と閉じた直後。制作メニューのキーはこの間鳴らさない。</summary>
        public static bool BlocksHotkeys => registered && QueryBlocks();

        public static void TryRegister(Plugin plugin)
        {
            if (!Chainloader.PluginInfos.ContainsKey(ModsMenuGuid))
            {
                Plugin.Log.LogInfo("Mods Menu が入っていないので、設定は cfg だけで変える（ESC の窓の「Mods」は出ない）");
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

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool QueryBlocks() => ModsMenuApi.BlocksHotkeys;

        // 並びは 0.7.0 で本人が図で決めた形（節「数え方と足し方」「表示」「キー」と、下の段の「メモを全部消す」）
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Register(Plugin p)
        {
            ModsMenuApi.Register(p)
                .SetOrder(10)
                .Section(() => Strings.SectionCounting)
                .Toggle(p.StackMaterials, () => Strings.StackMaterials)
                .Toggle(p.AddOnBlockedClick, () => Strings.AddOnBlockedClick)
                .Toggle(p.AddWhenCraftWindowOpens, () => Strings.AddWhenCraftWindowOpens)
                .Section(() => Strings.SectionDisplay)
                .Toggle(p.Visible, () => Strings.Visible)
                .Number(p.MaxEntries, () => Strings.MaxEntries)
                .Toggle(p.AutoPosition, () => Strings.AutoPosition)
                .Number(p.OffsetX, () => Strings.OffsetX, 0f, 60f)
                .Number(p.OffsetY, () => Strings.OffsetY, 0f, 60f)
                .Toggle(p.GameFrame, () => Strings.GameFrame)
                .Section(() => Strings.SectionKeys)
                .Key(p.ToggleKey, () => Strings.ToggleKey)
                .Key(p.StackKey, () => Strings.StackKey)
                .Key(p.RegisterKey, () => Strings.RegisterKey)
                .Key(p.RemoveKey, () => Strings.RemoveKey)
                .Key(p.ClearKey, () => Strings.ClearKey)
                .Button(() => Strings.ClearButton, () => p.ClearTodos("Mods の窓"), () => Strings.ClearConfirm);
        }
    }
}
