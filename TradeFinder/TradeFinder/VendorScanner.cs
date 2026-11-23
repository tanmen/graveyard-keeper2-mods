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

            /// <summary>最後に引いた居る場所（null は分からない）と、その引き方（ログ用）と、引いた時刻（Time.unscaledTime。負ならまだ引いていない）。</summary>
            public string Place;

            public string PlaceHow;

            public float PlaceAt = -1f;
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

                facts.Add(new VendorFacts(vendor.Id, name, built ? PlaceOf(vendor) : null, built, live.CurTier, vendor.Tiers));
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

        /// <summary>
        /// 商人の居る場所の名前。商人の絵の id（VendorDef.icon。NPC と町の店の店番の物の id）で世界の物を引き、その物の居るゾーンの名前。
        /// 引けなければ null（世界に物が無い商人も null。2026-11-22 のセーブでは司祭・ハロルドの物が世界に無かった）。
        /// NPC は歩くので、引いた結果は <see cref="PlaceTtl"/> 秒だけ覚えておく（ツールチップを出すたびに全員を引き直さない）。
        /// </summary>
        private static string PlaceOf(VendorStatic vendor)
        {
            float now = UnityEngine.Time.unscaledTime;
            if (vendor.PlaceAt >= 0f && now - vendor.PlaceAt < PlaceTtl)
            {
                return vendor.Place;
            }

            vendor.Place = FindPlace(vendor, out vendor.PlaceHow);
            vendor.PlaceAt = now;
            return vendor.Place;
        }

        private const float PlaceTtl = 10f;

        /// <summary>
        /// 物のゾーン → 物の位置を含むゾーン → 町の店ならその建物 → 町の範囲、の順に引く。根拠は TradeFinder/docs/game-api-notes.md「居る場所」。
        /// <paramref name="how"/> はログ用（どの引き方で何が取れたか）。
        /// </summary>
        private static string FindPlace(VendorStatic vendor, out string how)
        {
            WorldData world = MainGame.WorldData;
            if (world == null || string.IsNullOrEmpty(vendor.WgoId))
            {
                how = "世界がまだ無い";
                return null;
            }

            List<WgoData> found = world.GetWgoDataList(vendor.WgoId);
            if (found == null || found.Count == 0)
            {
                how = $"物 0（{vendor.WgoId}）";
                return null;
            }

            var notes = new List<string>();
            foreach (WgoData data in found)
            {
                if (data == null)
                {
                    continue;
                }

                string name = ZoneOf(data, world, out string zoneHow);
                notes.Add($"{data.WorldId} ({data.Position.x:0.#}, {data.Position.z:0.#}) {zoneHow}");
                if (name != null)
                {
                    how = $"物 {found.Count}・" + string.Join(" / ", notes);
                    return name;
                }

                WgoData building = data.LinkedToTownBuildingUniqueId != null ? world.GetWgoData(data.LinkedToTownBuildingUniqueId) : null;
                if (building != null)
                {
                    name = ZoneOf(building, world, out string buildingHow);
                    notes.Add($"建物 {building.WorldId} ({building.Position.x:0.#}, {building.Position.z:0.#}) {buildingHow}");
                    if (name != null)
                    {
                        how = $"物 {found.Count}・" + string.Join(" / ", notes);
                        return name;
                    }
                }
            }

            how = $"物 {found.Count}・" + string.Join(" / ", notes);
            return null;
        }

        /// <summary>1 つの物の居るゾーンの名前。右上の地名の枠と同じく、見せないゾーン（displayType Hidden）は飛ばす。</summary>
        private static string ZoneOf(WgoData data, WorldData world, out string how)
        {
            // 1. 物に結び付いたゾーン。ゲームは入れ物型（WorldZoneType.Default）で動いているゾーンにしか物を結び付けない（WorldZoneData.TryAddWgoData）
            WorldZoneData own = data.WorldZoneData;
            string name = NameOf(own);
            if (name != null)
            {
                how = "ゾーン " + own.id;
                return name;
            }

            // 2. 物の位置を含むゾーン（入れ物型でないゾーンも）。入れ物型を先に、入れ物型どうしは中心の近い方（プレイヤーのゾーンの決め方と同じ）
            GameSceneData scene = world.GetGameSceneDataById(data.WorldId);
            UnityEngine.Vector2 point = new UnityEngine.Vector2(data.Position.x, data.Position.z);
            WorldZoneData best = null;
            int zones = 0;
            if (scene?.worldZones != null)
            {
                zones = scene.worldZones.Count;
                foreach (WorldZoneData zone in scene.worldZones)
                {
                    if (NameOf(zone) == null || !zone.wholeZoneRect.Contains(point))
                    {
                        continue;
                    }

                    if (best == null || Better(zone, best, data.Position))
                    {
                        best = zone;
                    }
                }
            }

            if (best != null)
            {
                how = $"ゾーン {(own == null ? "空" : own.id)} → 位置で {best.id}（{best.worldZoneType}）";
                return NameOf(best);
            }

            // 3. 町（TownZone。右上の枠は町の中なら町と出す）。当たり判定は読み込まれている場面にしか無い
            if (InTown(data.Position))
            {
                how = $"ゾーン {(own == null ? "空" : own.id)} → 位置のゾーン無し（{zones} 個中）→ 町の範囲";
                return Strings.ZoneName("town");
            }

            how = $"ゾーン {(own == null ? "空" : own.id)} → 位置のゾーン無し（{zones} 個中）・町の範囲の外";
            return null;
        }

        private static bool Better(WorldZoneData zone, WorldZoneData best, UnityEngine.Vector3 position)
        {
            bool container = zone.worldZoneType == WorldZoneData.WorldZoneType.Default;
            bool bestContainer = best.worldZoneType == WorldZoneData.WorldZoneType.Default;
            if (container != bestContainer)
            {
                return container;
            }

            return UnityEngine.Vector3.Distance(zone.Center, position) < UnityEngine.Vector3.Distance(best.Center, position);
        }

        /// <summary>ゾーンの名前（訳文 wz_&lt;id&gt;）。見せないゾーン・訳文の無いゾーンは null。</summary>
        private static string NameOf(WorldZoneData zone)
        {
            if (zone == null || string.IsNullOrEmpty(zone.id))
            {
                return null;
            }

            try
            {
                if (zone.Definition != null && zone.Definition.displayType == WorldZoneDef.DisplayType.Hidden)
                {
                    return null;
                }
            }
            catch (Exception)
            {
                // 定義が引けなくても名前は出す
            }

            string name = Strings.ZoneName(zone.id);
            return string.IsNullOrEmpty(name) ? null : name;
        }

        private static bool InTown(UnityEngine.Vector3 position)
        {
            try
            {
                foreach (TownZone town in UnityEngine.Object.FindObjectsByType<TownZone>(UnityEngine.FindObjectsSortMode.None))
                {
                    if (town != null && town.Collider != null && town.Collider.bounds.Contains(position))
                    {
                        return true;
                    }
                }
            }
            catch (Exception)
            {
                // 町の当たり判定が引けなければ町とは言わない
            }

            return false;
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

            // 居る場所をどう引いたか（建てた商人だけ。未建設の店は引かない）
            foreach (VendorStatic vendor in Statics().Where(v => v.PlaceAt >= 0f))
            {
                Plugin.Log.LogInfo($"場所の引き方: {vendor.Id} → {vendor.Place ?? "不明"}（{vendor.PlaceHow}）");
            }
        }
    }
}
