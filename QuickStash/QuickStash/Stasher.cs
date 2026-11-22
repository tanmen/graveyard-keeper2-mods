using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using QuickStash.Core;

namespace QuickStash
{
    /// <summary>
    /// 所持品から、いまいる場所の宝箱へ「その宝箱にすでに入っている品と同じ品」をしまう。
    ///
    /// ゲームの宝箱の窓の「同じ品を全部移す」ボタン（Item.TakeAllItemsExistingInMeFromOtherInventory）と同じ範囲に合わせる:
    /// - 重ねられる品（ItemDef.stackCount &gt; 1）だけ。道具・武器など 1 つずつの品は動かさない
    /// - 所持品の袋の中の品は動かさない。宝箱の中の袋にも入れない（ignoreAllBags）
    /// - 同じ品かどうかは id で見る（品質の星の違う品は別の id）
    /// そのうえで、ロックした品とホットバーに置いた品（設定 KeepHotBarItems）は残す。
    ///
    /// 宝箱 = いまいる場所（PlayerData.CurrentWorldZoneData）の、ゲームが所持品の窓でまとめて開く収納（new MultiInventory(zone)。
    /// WGODef.inventorySize が 0 でなく OpenInMultiInventory）。プレイヤーが入れられない収納（Inventory.BlocksPlayerItemInsert）と、
    /// 入れられる品の決まり（白・黒の一覧）に合わない収納は外す。同じ品が 2 つ以上の宝箱にあれば、多く入っている宝箱から埋める。
    /// </summary>
    internal static class Stasher
    {
        private const int MaxLoggedItems = 12;

        private static bool loggedFirst;

        public static void Run(LockSet locks, bool keepHotBarItems, bool notify, bool animate)
        {
            PlayerData player = GameBridge.Player;
            Inventory own = player?.Inventory;
            if (own?.Data == null)
            {
                Plugin.Log.LogWarning("所持品が見つからないので、しまえない");
                return;
            }

            WorldZoneData zone = player.CurrentWorldZoneData;
            string zoneName = GameBridge.ZoneName(zone);
            List<Chest> chests = zone == null ? new List<Chest>() : ChestsIn(zone, own);
            var report = new StashReport { Chests = chests.Count };
            var moves = new List<StashMove>();

            if (chests.Count > 0)
            {
                HashSet<string> pinned = keepHotBarItems ? GameBridge.PinnedItems(player) : new HashSet<string>();
                List<Dictionary<string, int>> chestCounts = chests.Select(c => CountTopLevel(c.Inventory.Data)).ToList();
                foreach (KeyValuePair<string, Item> entry in TopLevelKinds(own.Data))
                {
                    StashKind(own, entry.Key, entry.Value, chests, chestCounts, locks, pinned, report, moves);
                }
            }

            if (report.Total > 0)
            {
                GameBridge.PlaySound("item_put");
                if (animate)
                {
                    FlyingItems.Launch(moves);
                }
            }

            string message = report.Message(GameBridge.Japanese, zoneName);
            if (notify && !GameNotice.TryShow("quick_stash", message))
            {
                Plugin.Log.LogInfo("右下の知らせを出せなかった（ゲームの知らせの部品が見つからない）");
            }

            Log(report, zone, zoneName, chests.Count, message);
        }

        private static void StashKind(Inventory own, string id, Item sample, List<Chest> chests, List<Dictionary<string, int>> chestCounts,
            LockSet locks, HashSet<string> pinned, StashReport report, List<StashMove> moves)
        {
            List<int> counts = chestCounts.Select(c => c.TryGetValue(id, out int n) ? n : 0).ToList();
            List<int> targets = StashPlanner.OrderTargets(counts);
            if (targets.Count == 0)
            {
                // どの宝箱にも入っていない品は、しまわない（数えもしない）
                return;
            }

            ItemDef def = sample.Definition;
            StashDecision decision = StashPlanner.Decide(id, def != null && def.stackCount > 1, locks, pinned);
            if (decision != StashDecision.Stash)
            {
                report.AddKept(id, decision);
                return;
            }

            int remaining = TopLevelCount(own.Data, id);
            foreach (int index in targets)
            {
                if (remaining <= 0)
                {
                    break;
                }

                Chest chest = chests[index];
                if (!Accepts(chest.Inventory, def))
                {
                    continue;
                }

                int moved = MoveInto(own, chest.Inventory, id, remaining);
                remaining -= moved;
                report.AddMoved(id, moved);
                if (moved > 0)
                {
                    moves.Add(new StashMove(def, chest.Wgo, moved));
                }
            }

            if (remaining > 0)
            {
                report.AddFull(id);
            }
        }

        /// <summary>
        /// 宝箱に入るだけ入れて、入った数だけ所持品から引く（ゲームの「同じ品を全部移す」と同じ順: 足してから引く）。
        /// 引けた数が入れた数より少なければ（ふつうは起きない）、多すぎた分を宝箱から戻して増殖させない。
        /// </summary>
        private static int MoveInto(Inventory own, Inventory chest, string id, int count)
        {
            var source = new Item(id, count);
            if (!chest.Data.AddItemToInventory(source, out List<Item> added, null, ignoreAllBags: true))
            {
                return 0;
            }

            int addedCount = added.Sum(i => i?.Count ?? 0);
            if (addedCount <= 0)
            {
                return 0;
            }

            List<Item> removed = own.RemoveItemById(id, addedCount, null, null, ignoreAllBags: true);
            int removedCount = removed.Sum(i => i?.Count ?? 0);
            if (removedCount < addedCount)
            {
                int excess = addedCount - removedCount;
                chest.Data.RemoveItemFromInventoryById(id, excess, null, null, ignoreAllBags: true);
                Plugin.Log.LogError($"{id} を宝箱に {addedCount} 入れたのに所持品から {removedCount} しか引けなかったので、宝箱から {excess} 戻した");
                addedCount = removedCount;
            }

            chest.NotifyItemsAdded(added);
            return addedCount;
        }

        /// <summary>
        /// その場所の宝箱（所持品と同じ在庫・入れられない収納を除き、同じ在庫は 1 度だけ）。在庫の並びはゲームの new MultiInventory(zone) のまま。
        /// 飛ぶ演出の行き先にするため、在庫ごとにその物（WgoData。同じ在庫を指す物が 2 つあれば先に見つけた方）を添える。
        /// </summary>
        private static List<Chest> ChestsIn(WorldZoneData zone, Inventory own)
        {
            var owners = new Dictionary<Item, WgoData>();
            WorldData world = MainGame.Instance?.GameSave?.worldData;
            if (world != null && zone.wgoDataList != null)
            {
                foreach (SGuid id in zone.wgoDataList)
                {
                    WgoData wgo = world.GetWgoData(id);
                    Item data = wgo?.Definition == null || wgo.Definition.inventorySize == 0 || !wgo.Definition.OpenInMultiInventory ? null : wgo.Inventory?.Data;
                    if (data != null && !owners.ContainsKey(data))
                    {
                        owners.Add(data, wgo);
                    }
                }
            }

            var chests = new List<Chest>();
            var seen = new HashSet<Item>();
            if (own.Data != null)
            {
                seen.Add(own.Data);
            }

            foreach (Inventory inventory in new MultiInventory(zone).inventoryList)
            {
                if (inventory?.Data == null || inventory.BlocksPlayerItemInsert || !seen.Add(inventory.Data))
                {
                    continue;
                }

                owners.TryGetValue(inventory.Data, out WgoData owner);
                chests.Add(new Chest(inventory, owner));
            }

            return chests;
        }

        /// <summary>宝箱の窓が所持品の品を「入れられる」と見る決まり（UIBaseChestWindowData.ItemAvailabilityConditionLeft）。</summary>
        private static bool Accepts(Inventory chest, ItemDef def)
        {
            if (def == null)
            {
                return false;
            }

            Item data = chest.Data;
            if (data.TryGetProperty(out BlackListFilterSerializedItemProperty black) && black.BlackList != null && black.BlackList.Contains(def))
            {
                return false;
            }

            if (data.TryGetProperty(out WhiteListFilterSerializedItemProperty white) && white.WhiteList != null && !white.WhiteList.IsEmpty
                && !white.WhiteList.Contains(def))
            {
                return false;
            }

            return true;
        }

        /// <summary>所持品の一番上の段（袋の中を除く）にある品の種類。見つけた順。</summary>
        private static List<KeyValuePair<string, Item>> TopLevelKinds(Item inventory)
        {
            var kinds = new List<KeyValuePair<string, Item>>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (Item item in inventory.Inventory.ToList())
            {
                if (item == null || item.IsEmpty || item.IsBag || string.IsNullOrEmpty(item.id) || !seen.Add(item.id))
                {
                    continue;
                }

                kinds.Add(new KeyValuePair<string, Item>(item.id, item));
            }

            return kinds;
        }

        private static int TopLevelCount(Item inventory, string id)
        {
            int count = 0;
            foreach (Item item in inventory.Inventory)
            {
                if (item != null && !item.IsEmpty && !item.IsBag && item.id == id)
                {
                    count += item.Count;
                }
            }

            return count;
        }

        /// <summary>宝箱の一番上の段（中の袋を除く）にある品ごとの数。</summary>
        private static Dictionary<string, int> CountTopLevel(Item inventory)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (Item item in inventory.Inventory)
            {
                if (item == null || item.IsEmpty || item.IsBag || string.IsNullOrEmpty(item.id))
                {
                    continue;
                }

                counts.TryGetValue(item.id, out int before);
                counts[item.id] = before + item.Count;
            }

            return counts;
        }

        private static void Log(StashReport report, WorldZoneData zone, string zoneName, int chests, string message)
        {
            var line = new StringBuilder();
            line.Append("しまうキー: 場所 ").Append(zone == null ? "（無し）" : zoneName + " (" + zone.id + ")")
                .Append("・宝箱 ").Append(chests).Append(" 個 → ").Append(message);
            if (report.Kinds > 0)
            {
                line.Append(" [");
                line.Append(string.Join(", ", report.Moved.Take(MaxLoggedItems).Select(m => m.Key + " " + m.Value)));
                if (report.Kinds > MaxLoggedItems)
                {
                    line.Append(", ほか ").Append(report.Kinds - MaxLoggedItems).Append(" 種");
                }

                line.Append(']');
            }

            if (report.LockedKept + report.PinnedKept + report.FullKinds > 0)
            {
                line.Append("（残した: ロック ").Append(report.LockedKept).Append(" 種・ホットバー ").Append(report.PinnedKept)
                    .Append(" 種・入りきらない ").Append(report.FullKinds).Append(" 種）");
            }

            if (!loggedFirst || report.Total > 0)
            {
                loggedFirst = true;
                Plugin.Log.LogInfo(line.ToString());
            }
            else
            {
                Plugin.Log.LogDebug(line.ToString());
            }

            if (zone != null && chests == 0)
            {
                Plugin.Log.LogInfo($"場所 {zone.id} に、しまえる宝箱が 0 個（ゲームが所持品の窓でまとめて開く収納が無い）");
            }
        }

        private sealed class Chest
        {
            public Chest(Inventory inventory, WgoData wgo)
            {
                Inventory = inventory;
                Wgo = wgo;
            }

            public Inventory Inventory { get; }

            /// <summary>その在庫を持つ物（飛ぶ演出の行き先）。見つからなければ null。</summary>
            public WgoData Wgo { get; }
        }
    }

    /// <summary>1 つの宝箱へしまった 1 種類の品（飛ぶ演出の 1 つ分）。</summary>
    internal sealed class StashMove
    {
        public StashMove(ItemDef item, WgoData chest, int count)
        {
            Item = item;
            Chest = chest;
            Count = count;
        }

        public ItemDef Item { get; }

        public WgoData Chest { get; }

        public int Count { get; }
    }
}
