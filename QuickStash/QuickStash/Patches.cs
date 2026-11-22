using HarmonyLib;

namespace QuickStash
{
    // ゲームの所持品の窓に差し込む所。根拠は QuickStash/docs/game-api-notes.md。
    // 1 つの差し込みが落ちてもゲームを止めないよう、本体は全部 Hooks.Guard の中で動かす。

    /// <summary>
    /// 在庫の窓の部品を描き直したあと（InventoryWidget.Redraw。所持品・宝箱・袋の窓のどれでも来る。在庫が増減するたびにも来る）。
    /// 所持品の部品なら、ロックした品のセルに南京錠を出し、それ以外のセルからは外す。
    /// </summary>
    [HarmonyPatch(typeof(InventoryWidget), nameof(InventoryWidget.Redraw))]
    internal static class InventoryWidgetRedrawPatch
    {
        private static void Postfix(InventoryWidget __instance)
        {
            Hooks.Guard("所持品の窓の描き直し", () => LockMarks.Refresh(__instance));
        }
    }

    /// <summary>セルが空にされる・片付けられるとき（UIItemCell.Flush。使い回す前に必ず通る）。南京錠を外す。</summary>
    [HarmonyPatch(typeof(UIItemCell), nameof(UIItemCell.Flush), new[] { typeof(bool) })]
    internal static class ItemCellFlushPatch
    {
        private static void Postfix(UIItemCell __instance)
        {
            Hooks.Guard("セルの片付け", () => LockMarks.Hide(__instance));
        }
    }
}
