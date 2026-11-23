using TradeFinder.Core;

namespace TradeFinder.Core.Tests;

public class TradeLayoutTests
{
    private static TradeOffer Now(string name, TradeSide side, int price, bool happiness = false) =>
        new TradeOffer(name, name, "町", side, TradeState.Now, 1, happiness) { Price = price };

    private static TradeOffer Later(string name, TradeSide side, TradeState state, int tier) =>
        new TradeOffer(name, name, null, side, state, tier, false);

    private static readonly TradeOffer[] Sample =
    {
        Later("ヘルム", TradeSide.Sell, TradeState.LaterTier, 2),
        Now("建築士", TradeSide.Sell, 9),
        Later("家具職人", TradeSide.Sell, TradeState.NotBuilt, 1),
        Now("家具職人", TradeSide.Buy, 15),
        Now("建築士", TradeSide.Buy, 12),
        Now("鍛冶場", TradeSide.Sell, 11),
        Later("酒場", TradeSide.Buy, TradeState.LaterTier, 3),
        Later("パン屋", TradeSide.Buy, TradeState.LaterTier, 2),
    };

    private static string[] Names(TradeGroup group) => group.Offers.Select(o => o.Name).ToArray();

    [Fact]
    public void 買える_売れるの順で_いまの所が先()
    {
        List<TradeGroup> groups = TradeLayout.Arrange(Sample, new TradeOptions());

        Assert.Equal(new[] { TradeSide.Buy, TradeSide.Sell }, groups.Select(g => g.Side));
        // 買えるは安い順、まだの所は段階の低い順
        Assert.Equal(new[] { "建築士", "家具職人", "パン屋", "酒場" }, Names(groups[0]));
        // 売れるは高い順、未建設が段階待ちより先
        Assert.Equal(new[] { "鍛冶場", "建築士", "家具職人", "ヘルム" }, Names(groups[1]));
    }

    [Fact]
    public void まだの所を出さない設定なら_いまの所だけ()
    {
        List<TradeGroup> groups = TradeLayout.Arrange(Sample, new TradeOptions { Unavailable = ShowWhen.Never });

        Assert.Equal(new[] { "建築士", "家具職人" }, Names(groups[0]));
        Assert.Equal(new[] { "鍛冶場", "建築士" }, Names(groups[1]));
    }

    [Fact]
    public void キーの間だけの設定は_キーを押していなければ出さない()
    {
        var options = new TradeOptions { Sell = ShowWhen.WhileKey, Unavailable = ShowWhen.WhileKey };

        List<TradeGroup> released = TradeLayout.Arrange(Sample, options);
        Assert.Single(released);
        Assert.Equal(TradeSide.Buy, released[0].Side);
        Assert.Equal(new[] { "建築士", "家具職人" }, Names(released[0]));

        options.KeyHeld = true;
        List<TradeGroup> held = TradeLayout.Arrange(Sample, options);
        Assert.Equal(2, held.Count);
        Assert.Equal(4, held[1].Offers.Count);
    }

    [Fact]
    public void 行の無い向きは出さない()
    {
        List<TradeGroup> groups = TradeLayout.Arrange(new[] { Now("建築士", TradeSide.Buy, 12) }, new TradeOptions());

        Assert.Single(groups);
        Assert.Empty(TradeLayout.Arrange(new TradeOffer[0], new TradeOptions()));
        Assert.Empty(TradeLayout.Arrange(Sample, new TradeOptions { Buy = ShowWhen.Never, Sell = ShowWhen.Never }));
    }

    [Fact]
    public void 値段の分からない所は後ろ()
    {
        var offers = new[] { Now("A", TradeSide.Buy, 0), Now("B", TradeSide.Buy, 20), Now("C", TradeSide.Sell, 0), Now("D", TradeSide.Sell, 3) };

        List<TradeGroup> groups = TradeLayout.Arrange(offers, new TradeOptions());

        Assert.Equal(new[] { "B", "A" }, Names(groups[0]));
        Assert.Equal(new[] { "D", "C" }, Names(groups[1]));
    }

    [Fact]
    public void キーを使う設定があるか()
    {
        Assert.False(new TradeOptions().UsesKey);
        Assert.True(new TradeOptions { Price = ShowWhen.WhileKey }.UsesKey);
        Assert.False(new TradeOptions { Price = ShowWhen.Never }.UsesKey);
    }
}
