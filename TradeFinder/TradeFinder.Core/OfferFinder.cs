using System;
using System.Collections.Generic;

namespace TradeFinder.Core
{
    /// <summary>
    /// 品ごとに、どの商人が売る・買い取るかを決める。ゲームの判定（Vendor.CanSellItemToPlayer / CanBuyItemFromPlayer）と同じく、
    /// いまの段階の扱う品の中で、売らない・買い取らない品の一覧に無いもの。
    /// </summary>
    public static class OfferFinder
    {
        public static List<TradeOffer> Find(string itemId, IEnumerable<VendorFacts> vendors)
        {
            var offers = new List<TradeOffer>();
            if (string.IsNullOrEmpty(itemId) || vendors == null)
            {
                return offers;
            }

            foreach (VendorFacts vendor in vendors)
            {
                if (vendor == null || vendor.Tiers.Count == 0)
                {
                    continue;
                }

                foreach (TradeSide side in new[] { TradeSide.Buy, TradeSide.Sell })
                {
                    TradeOffer offer = Offer(itemId, vendor, side);
                    if (offer != null)
                    {
                        offers.Add(offer);
                    }
                }
            }

            return offers;
        }

        private static TradeOffer Offer(string itemId, VendorFacts vendor, TradeSide side)
        {
            int current = Math.Max(1, Math.Min(vendor.CurrentTier, vendor.Tiers.Count));
            int tier = FirstTier(itemId, vendor, side, current);
            if (tier == 0)
            {
                return null;
            }

            TradeState state = !vendor.Built ? TradeState.NotBuilt : tier == current ? TradeState.Now : TradeState.LaterTier;
            bool happiness = side == TradeSide.Sell && vendor.Tiers[tier - 1].HappinessItems.Contains(itemId);
            return new TradeOffer(vendor.Id, vendor.Name, vendor.Place, side, state, tier, happiness);
        }

        /// <summary>いまの段階から上で、初めてその品を扱う段階（1 から）。無ければ 0。いまより下の段階は見ない（段階は戻らない）。</summary>
        private static int FirstTier(string itemId, VendorFacts vendor, TradeSide side, int current)
        {
            for (int tier = current; tier <= vendor.Tiers.Count; tier++)
            {
                VendorTier data = vendor.Tiers[tier - 1];
                if (data != null && data.Handles(itemId, side))
                {
                    return tier;
                }
            }

            return 0;
        }
    }
}
