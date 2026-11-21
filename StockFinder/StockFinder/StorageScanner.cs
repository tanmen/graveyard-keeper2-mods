using System;
using System.Collections.Generic;
using System.Diagnostics;
using StockFinder.Core;
using UnityEngine;

namespace StockFinder
{
    /// <summary>
    /// 品が置いてある所を数える。所持品（袋の中も）と、世界にある宝箱の全部。
    /// 宝箱は、ゲームが所持品の窓でまとめて開く収納（WGODef.inventorySize が 0 でなく OpenInMultiInventory。MultiInventory(WorldZoneData) と同じ選び方）。
    /// ゾーンの一覧（WorldZoneData.MultiInventoryWgoDatas）ではなく場面の全部の物（GameSceneData.wgoDataList）から拾うので、
    /// ゾーンの外に置いた宝箱も数える。別の物の在庫を指す物（hasRefToOtherWgoInventory）は、同じ在庫を 1 度だけ数える。
    /// 宝箱の一覧は 2 秒と、ゲームのセーブ（GameSave）が替わるまで使い回す（数は毎回数え直す）。
    /// </summary>
    internal static class StorageScanner
    {
        private const float CacheSeconds = 2f;
        private const int MaxErrorLogs = 3;

        private static List<Storage> storages;
        private static float builtAt;
        private static GameSave builtFor;
        private static bool loggedScan;
        private static int errorLogs;

        /// <summary>所持品と宝箱ごとの数（見つけた順。数が 0 の宝箱は入れない。所持品は 0 でも入れる）。</summary>
        public static List<StockPlace> Places(string itemId)
        {
            var places = new List<StockPlace>();
            Inventory own = MainGame.PlayerData?.Inventory;
            if (own?.Data != null)
            {
                places.Add(new StockPlace(null, Strings.PlayerInventory, own.Data.GetTotalCountInInventory(itemId), isPlayer: true));
            }

            foreach (Storage storage in Storages())
            {
                int count = storage.Inventory.Data.GetTotalCountInInventory(itemId);
                if (count > 0)
                {
                    places.Add(new StockPlace(storage.Zone, storage.Name, count));
                }
            }

            return places;
        }

        private static List<Storage> Storages()
        {
            GameSave save = MainGame.Instance == null ? null : MainGame.Instance.GameSave;
            float now = Time.unscaledTime;
            if (storages != null && ReferenceEquals(save, builtFor) && now - builtAt < CacheSeconds && now >= builtAt)
            {
                return storages;
            }

            Stopwatch watch = Stopwatch.StartNew();
            var found = new List<Storage>();
            var seen = new HashSet<Inventory>();
            int scanned = 0;
            int shared = 0;
            int outside = 0;
            var zones = new HashSet<string>();

            Inventory own = MainGame.PlayerData?.Inventory;
            if (own != null)
            {
                seen.Add(own);
            }

            WorldData world = MainGame.WorldData;
            if (world?.gameSceneDataList != null)
            {
                foreach (GameSceneData scene in world.gameSceneDataList)
                {
                    if (scene?.wgoDataList == null)
                    {
                        continue;
                    }

                    foreach (WgoData wgo in scene.wgoDataList)
                    {
                        scanned++;
                        try
                        {
                            WGODef def = wgo?.Definition;
                            if (def == null || def.inventorySize == 0 || !def.OpenInMultiInventory)
                            {
                                continue;
                            }

                            Inventory inventory = wgo.Inventory;
                            if (inventory?.Data == null)
                            {
                                continue;
                            }

                            if (!seen.Add(inventory))
                            {
                                shared++;
                                continue;
                            }

                            WorldZoneData zone = wgo.WorldZoneData;
                            string zoneName;
                            if (zone == null)
                            {
                                outside++;
                                zoneName = Strings.Elsewhere;
                            }
                            else
                            {
                                zoneName = Strings.ZoneName(zone.id);
                                zones.Add(zone.id);
                            }

                            found.Add(new Storage(inventory, zoneName, Strings.ObjectName(wgo.id)));
                        }
                        catch (Exception e)
                        {
                            LogError("宝箱を調べられなかった（" + (wgo?.id ?? "?") + "）", e);
                        }
                    }
                }
            }

            storages = found;
            builtAt = now;
            builtFor = save;

            if (!loggedScan)
            {
                loggedScan = true;
                string line = $"宝箱を拾った: {found.Count} 個（場所 {zones.Count} か所・場所の外 {outside} 個・在庫を共有していて省いた {shared} 個）"
                    + $"・見た物 {scanned} 個・{watch.Elapsed.TotalMilliseconds:0.0} ms";
                if (found.Count == 0)
                {
                    // 0 個は成功ではない（読み込み前か、ゲームの形が変わって拾えていない）
                    Plugin.Log.LogWarning(line + "。宝箱が 0 個なので、ツールチップには所持品の数だけが出る");
                    loggedScan = scanned > 0; // まだ何も読み込まれていなかったなら、次に拾えたときにもう 1 度出す
                }
                else
                {
                    Plugin.Log.LogInfo(line);
                }
            }

            return storages;
        }

        private static void LogError(string what, Exception e)
        {
            if (errorLogs >= MaxErrorLogs)
            {
                return;
            }

            errorLogs++;
            Plugin.Log.LogError($"{what}: {e}" + (errorLogs == MaxErrorLogs ? "（以後は出さない）" : string.Empty));
        }

        private sealed class Storage
        {
            public Storage(Inventory inventory, string zone, string name)
            {
                Inventory = inventory;
                Zone = zone;
                Name = name;
            }

            public Inventory Inventory { get; }

            public string Zone { get; }

            public string Name { get; }
        }
    }
}
