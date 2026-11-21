using System;
using System.Collections.Generic;
using CraftTodo.Core;

namespace CraftTodo
{
    /// <summary>
    /// 制作メニューの数え直し 1 回分の在庫（<see cref="ITodoStock"/>）。持ち物＋作る場所のゾーンの収納を数える
    /// （GameBridge.CreateInventory と同じ範囲）。同じ数え直しの中では、件ごとの範囲とゾーンの収納を作り直さずに使い回す。
    /// </summary>
    internal sealed class GameStock : ITodoStock
    {
        private readonly Dictionary<string, MultiInventory> available = new Dictionary<string, MultiInventory>();
        private readonly Dictionary<string, MultiInventory> zoneOnly = new Dictionary<string, MultiInventory>();
        private readonly Dictionary<string, WorldZoneData> zones = new Dictionary<string, WorldZoneData>();

        public int Available(TodoEntry entry, TodoMaterial material)
        {
            if (!available.TryGetValue(entry.Key, out MultiInventory inventory))
            {
                inventory = GameBridge.CreateInventory(entry);
                available[entry.Key] = inventory;
            }

            return Count(inventory, material);
        }

        public int ZoneOnly(TodoEntry entry, TodoMaterial material)
        {
            string key = ZoneKey(entry);
            if (key == null)
            {
                return 0;
            }

            if (!zoneOnly.TryGetValue(key, out MultiInventory inventory))
            {
                inventory = new MultiInventory(zones[key]);
                zoneOnly[key] = inventory;
            }

            return Count(inventory, material);
        }

        public string ZoneKey(TodoEntry entry)
        {
            WorldZoneData zone = GameBridge.ZoneOfEntry(entry);
            if (zone == null)
            {
                return null;
            }

            string key = zone.gameSceneId + ":" + zone.id;
            zones[key] = zone;
            return key;
        }

        public bool GroupContains(string groupId, string itemId)
        {
            foreach (ItemDef item in GameBridge.GroupItems(groupId))
            {
                if (item != null && string.Equals(item.id, itemId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static int Count(MultiInventory inventory, TodoMaterial material)
        {
            if (!material.IsGroup)
            {
                return inventory.GetTotalCount(material.ItemId);
            }

            int sum = 0;
            foreach (ItemDef item in GameBridge.GroupItems(material.ItemId))
            {
                sum += inventory.GetTotalCount(item.id);
            }

            return sum;
        }
    }
}
