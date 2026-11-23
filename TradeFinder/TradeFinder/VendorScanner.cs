using System;
using System.Collections.Generic;
using System.Linq;
using TradeFinder.Core;

namespace TradeFinder
{
    /// <summary>
    /// 商人を引く。根拠は TradeFinder/docs/game-api-notes.md。
    /// 定義（段階ごとの品・売らない品・買い取らない品・幸福の入る品、町の店の建物の名前）は初めの 1 度だけ GameBalance から写し、
    /// 段階・建てたか・居る場所は、ツールチップを出すたびにセーブから引く。
    /// </summary>
    internal static class VendorScanner
    {
        private sealed class VendorStatic
        {
            public string Id;

            public string WgoId;

            /// <summary>町の店なら、段階 1 の建物の定義の id（名前の訳文の鍵。例 Pharmacy_t1 → 薬屋）。町の店でなければ null。</summary>
            public string ShopId;

            public List<VendorTier> Tiers;
        }

        private static List<VendorStatic> statics;
        private static bool loggedFirst;

        public static List<VendorFacts> Current()
        {
            var facts = new List<VendorFacts>();
            GameSave save = MainGame.Instance?.GameSave;
            VendorSystem system = save?.vendorSystem;
            if (system == null)
            {
                return facts;
            }

            foreach (VendorStatic vendor in Statics())
            {
                Vendor live = system.GetVendor(vendor.Id);
                if (live == null)
                {
                    continue;
                }

                bool built = vendor.ShopId == null || (save.knowledgeSystem?.IsVendorForOrdersUnlocked(vendor.Id) ?? true);
                string name = vendor.ShopId != null ? Strings.L(vendor.ShopId, null) : Strings.L(vendor.Id, null);
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                facts.Add(new VendorFacts(vendor.Id, name, built ? PlaceOf(vendor.WgoId) : null, built, live.CurTier, vendor.Tiers));
            }

            LogFirst(facts);
            return facts;
        }

        /// <summary>いまの段階・在庫での 1 個の値段（商人の窓のセルと同じ）。買えるはいまの在庫で、売れるは 1 個売ったあとの在庫で、元値より高くしない。</summary>
        public static int PriceOf(string vendorId, ItemDef item, TradeSide side)
        {
            Vendor vendor = MainGame.Instance?.GameSave?.vendorSystem?.GetVendor(vendorId);
            if (vendor == null || item == null)
            {
                return 0;
            }

            int stock = vendor.Inventory?.Data?.GetTotalCountInInventory(item.id) ?? 0;
            if (side == TradeSide.Buy)
            {
                return UnityEngine.Mathf.RoundToInt(UnityEngine.Mathf.Round(vendor.CurPrice(item.id, buy: true, stock) * 100f) / 100f);
            }

            float price = vendor.CurPrice(item.id, buy: false, stock + 1);
            if (price > item.basePrice)
            {
                price = item.basePrice;
            }

            return UnityEngine.Mathf.RoundToInt(UnityEngine.Mathf.Round(price * 100f) / 100f);
        }

        /// <summary>商人の居る場所の名前。商人の絵の id（VendorDef.icon。NPC と町の店の店番の物の id）で世界の物を引き、そのゾーンの名前。</summary>
        private static string PlaceOf(string wgoId)
        {
            if (string.IsNullOrEmpty(wgoId))
            {
                return null;
            }

            List<WgoData> found = MainGame.WorldData?.GetWgoDataList(wgoId);
            if (found == null)
            {
                return null;
            }

            foreach (WgoData data in found)
            {
                WorldZoneData zone = data?.WorldZoneData;
                if (zone != null && !string.IsNullOrEmpty(zone.id))
                {
                    string name = Strings.ZoneName(zone.id);
                    if (!string.IsNullOrEmpty(name))
                    {
                        return name;
                    }
                }
            }

            return null;
        }

        private static List<VendorStatic> Statics()
        {
            if (statics != null)
            {
                return statics;
            }

            var list = new List<VendorStatic>();
            GameBalance balance = GameBalance.Me;
            if (balance?.vendorDefs == null)
            {
                return list; // まだ読めない。次に出すときに引き直す
            }

            Dictionary<string, string> shops = ShopIds(balance);
            foreach (VendorDef def in balance.vendorDefs)
            {
                if (def == null || string.IsNullOrEmpty(def.id) || def.id.StartsWith("test", StringComparison.Ordinal) || def.tierDataList == null)
                {
                    continue;
                }

                List<VendorTier> tiers = def.tierDataList.Select(Tier).ToList();
                if (tiers.All(t => t.Products.Count == 0))
                {
                    continue; // 注文だけの商人（ジャックの工房など）
                }

                shops.TryGetValue(def.id, out string shopId);
                list.Add(new VendorStatic
                {
                    Id = def.id,
                    WgoId = IconId(def),
                    ShopId = shopId,
                    Tiers = tiers,
                });
            }

            statics = list;
            Plugin.Log.LogInfo($"商人の定義を写した: {list.Count} 人（町の店 {list.Count(v => v.ShopId != null)}・"
                + $"扱う品 {list.SelectMany(v => v.Tiers).SelectMany(t => t.Products).Distinct().Count()} 種）");
            return statics;
        }

        private static VendorTier Tier(VendorTierData data) => data == null
            ? new VendorTier(null, null, null, null)
            : new VendorTier(
                data.vendorProducts?.Where(p => p != null).Select(p => p.itemId),
                data.notBuying,
                data.notSelling,
                data.townVendorProductInfos?.Where(p => p != null).Select(p => p.itemId));

        /// <summary>商人の id → 町の店の段階 1 の建物の id（同じ商人の建物のうち、どの建物の lvlUpId にも出てこないもの）。</summary>
        private static Dictionary<string, string> ShopIds(GameBalance balance)
        {
            var result = new Dictionary<string, string>();
            if (balance.townBuildingDefs == null)
            {
                return result;
            }

            var upgrades = new HashSet<string>(balance.townBuildingDefs.Where(d => d != null && !string.IsNullOrEmpty(d.lvlUpId)).Select(d => d.lvlUpId));
            foreach (TownBuildingDef def in balance.townBuildingDefs)
            {
                if (def == null || string.IsNullOrEmpty(def.vendorId) || string.IsNullOrEmpty(def.id))
                {
                    continue;
                }

                // 初めての商人なら覚える。覚えたのが段階 2 以上の建物で、こちらが段階 1 なら差し替える
                bool known = result.TryGetValue(def.vendorId, out string seen);
                if (!known || (upgrades.Contains(seen) && !upgrades.Contains(def.id)))
                {
                    result[def.vendorId] = def.id;
                }
            }

            return result;
        }

        /// <summary>VendorDef の private の icon（商人の絵の物の id）。読めなければ商人の id。</summary>
        private static string IconId(VendorDef def)
        {
            try
            {
                string icon = HarmonyLib.Traverse.Create(def).Field("icon").GetValue<string>();
                return string.IsNullOrEmpty(icon) ? def.id : icon;
            }
            catch (Exception)
            {
                return def.id;
            }
        }

        private static void LogFirst(List<VendorFacts> facts)
        {
            if (loggedFirst || facts.Count == 0)
            {
                return;
            }

            loggedFirst = true;
            Plugin.Log.LogInfo("商人: " + string.Join(" / ", facts.Select(f =>
                $"{f.Name}（{f.Id}・段階 {f.CurrentTier}・{(f.Built ? "場所 " + (f.Place ?? "不明") : "未建設")}）")));
        }
    }
}
