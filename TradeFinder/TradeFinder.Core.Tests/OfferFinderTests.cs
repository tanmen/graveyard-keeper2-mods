using TradeFinder.Core;

namespace TradeFinder.Core.Tests;

public class OfferFinderTests
{
    // ゲームのデータ（v1.012.9）の木板に近い形: ヘルムは段階 2 から買い取るだけ（売らない）、建築士は売る・買い取る、
    // 家具職人は売る・買い取る・売ると幸福が入る
    private static VendorTier Tier(string[] products, string[] notBuying = null, string[] notSelling = null, string[] happiness = null) =>
        new VendorTier(products, notBuying, notSelling, happiness);

    private static readonly VendorTier[] HermTiers =
    {
        Tier(new[] { "bronze_scrap" }),
        Tier(new[] { "bronze_scrap", "wooden_plank" }, notSelling: new[] { "wooden_plank" }),
        Tier(new[] { "bronze_scrap", "wooden_plank" }, notSelling: new[] { "wooden_plank" }),
    };

    private static readonly VendorTier[] BuilderTiers =
    {
        Tier(new[] { "wooden_plank" }),
        Tier(new[] { "wooden_plank", "nails_iron" }),
        Tier(new[] { "wooden_plank", "nails_iron" }),
    };

    private static readonly VendorTier[] FurnitureTiers =
    {
        Tier(new[] { "wooden_plank", "trinkets_wood" }, notSelling: new[] { "trinkets_wood" }, happiness: new[] { "wooden_plank" }),
        Tier(new[] { "wooden_plank", "nails_iron" }, happiness: new[] { "wooden_plank" }),
        Tier(new[] { "wooden_plank", "nails_iron" }, happiness: new[] { "wooden_plank" }),
    };

    private static VendorFacts[] World(int hermTier = 1, bool furnitureBuilt = false) => new[]
    {
        new VendorFacts("npc_herm", "ヘルム", "ヘルムのお店", true, hermTier, HermTiers),
        new VendorFacts("t_b_builder", "建築士", "町", true, 1, BuilderTiers),
        new VendorFacts("t_b_furniturer", "家具職人", null, furnitureBuilt, 1, FurnitureTiers),
    };

    private static TradeOffer Get(List<TradeOffer> offers, string vendor, TradeSide side) =>
        offers.SingleOrDefault(o => o.VendorId == vendor && o.Side == side);

    [Fact]
    public void 建てた店は_いまの段階で扱う品を売って買い取る()
    {
        List<TradeOffer> offers = OfferFinder.Find("wooden_plank", World());

        Assert.Equal(TradeState.Now, Get(offers, "t_b_builder", TradeSide.Buy).State);
        Assert.Equal(TradeState.Now, Get(offers, "t_b_builder", TradeSide.Sell).State);
        Assert.Equal("町", Get(offers, "t_b_builder", TradeSide.Buy).Place);
    }

    [Fact]
    public void 建てていない店は_未建設で出す()
    {
        List<TradeOffer> offers = OfferFinder.Find("wooden_plank", World());

        TradeOffer sell = Get(offers, "t_b_furniturer", TradeSide.Sell);
        Assert.Equal(TradeState.NotBuilt, sell.State);
        Assert.Equal(1, sell.Tier);
        Assert.True(sell.Happiness);
        Assert.False(Get(offers, "t_b_furniturer", TradeSide.Buy).Happiness);
    }

    [Fact]
    public void 段階が上がると扱う品は_その段階から()
    {
        List<TradeOffer> offers = OfferFinder.Find("wooden_plank", World(hermTier: 1));

        TradeOffer sell = Get(offers, "npc_herm", TradeSide.Sell);
        Assert.Equal(TradeState.LaterTier, sell.State);
        Assert.Equal(2, sell.Tier);
    }

    [Fact]
    public void 売らない品の一覧にあれば_買えるには出さない()
    {
        List<TradeOffer> offers = OfferFinder.Find("wooden_plank", World(hermTier: 2));

        Assert.Null(Get(offers, "npc_herm", TradeSide.Buy));
        Assert.Equal(TradeState.Now, Get(offers, "npc_herm", TradeSide.Sell).State);
    }

    [Fact]
    public void 買い取らない品の一覧にあれば_売れるには出さない()
    {
        var tiers = new[] { Tier(new[] { "bag_food", "beer" }, notBuying: new[] { "bag_food" }) };
        List<TradeOffer> offers = OfferFinder.Find("bag_food", new[] { new VendorFacts("t_b_baker", "パン屋", "町", true, 1, tiers) });

        Assert.NotNull(Get(offers, "t_b_baker", TradeSide.Buy));
        Assert.Null(Get(offers, "t_b_baker", TradeSide.Sell));
    }

    [Fact]
    public void いまより下の段階にしか無い品は_出さない()
    {
        // 商人頭は段階 2 で品が減る（ゲームのデータで、段階 1 にだけ屑青銅がある）
        var tiers = new[] { Tier(new[] { "bronze_scrap" }), Tier(new string[0]), Tier(new string[0]) };
        List<TradeOffer> offers = OfferFinder.Find("bronze_scrap", new[] { new VendorFacts("npc_warehouse_manager", "商人頭", "町の倉庫", true, 2, tiers) });

        Assert.Empty(offers);
    }

    [Fact]
    public void 建てていない店の品が上の段階にしかなければ_その段階を持つ()
    {
        List<TradeOffer> offers = OfferFinder.Find("nails_iron", World());

        TradeOffer buy = Get(offers, "t_b_furniturer", TradeSide.Buy);
        Assert.Equal(TradeState.NotBuilt, buy.State);
        Assert.Equal(2, buy.Tier);
        Assert.Equal(TradeState.LaterTier, Get(offers, "t_b_builder", TradeSide.Buy).State);
    }

    [Fact]
    public void どこも扱わない品は_空()
    {
        Assert.Empty(OfferFinder.Find("gold_ore", World()));
        Assert.Empty(OfferFinder.Find(null, World()));
        Assert.Empty(OfferFinder.Find("wooden_plank", null));
    }

    [Fact]
    public void 段階の数を超えたいまの段階は_最後の段階として読む()
    {
        List<TradeOffer> offers = OfferFinder.Find("wooden_plank", new[] { new VendorFacts("t_b_builder", "建築士", "町", true, 9, BuilderTiers) });

        Assert.Equal(TradeState.Now, Get(offers, "t_b_builder", TradeSide.Buy).State);
        Assert.Equal(3, Get(offers, "t_b_builder", TradeSide.Buy).Tier);
    }
}
