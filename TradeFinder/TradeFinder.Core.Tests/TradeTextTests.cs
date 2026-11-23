using TradeFinder.Core;

namespace TradeFinder.Core.Tests;

public class TradeTextTests
{
    // 日本語の文言（mod 本体の Strings と同じ形）。文字の形は印で見分ける
    private static TradeWords Words() => new TradeWords
    {
        Label = side => side == TradeSide.Buy ? "買える" : "売れる",
        Paren = inner => "（" + inner + "）",
        NotBuilt = "未建設",
        FromTier = tier => "ステージ " + new[] { "", "I", "II", "III" }[tier] + " から",
        StateJoin = "、",
        Money = price => "$" + price,
        HappinessIcon = "<sprite name=\"happiness\">",
        Heading = s => "[H]" + s,
        Plain = s => s,
        Dim = s => "[D]" + s,
    };

    private static List<TradeGroup> Groups(params TradeOffer[] offers) => TradeLayout.Arrange(offers, new TradeOptions());

    [Fact]
    public void 一行目に見出し_二行目からは名前だけ()
    {
        var offers = new[]
        {
            new TradeOffer("b", "建築士", "町", TradeSide.Buy, TradeState.Now, 1, false) { Price = 12 },
            new TradeOffer("f", "家具職人", null, TradeSide.Buy, TradeState.NotBuilt, 1, false),
        };

        List<string> lines = TradeText.Lines(Groups(offers), new TradeOptions(), Words());

        Assert.Equal(new[] { "[H]買える: 建築士（町） $12", "[D]家具職人（未建設）" }, lines);
    }

    [Fact]
    public void 売れるは幸福のアイコンを最後に付ける()
    {
        var offers = new[]
        {
            new TradeOffer("f", "家具職人", "町", TradeSide.Sell, TradeState.Now, 1, true) { Price = 9 },
            new TradeOffer("h", "ヘルム", null, TradeSide.Sell, TradeState.LaterTier, 2, false),
        };

        List<string> lines = TradeText.Lines(Groups(offers), new TradeOptions(), Words());

        Assert.Equal(new[] { "[H]売れる: 家具職人（町） $9 <sprite name=\"happiness\">", "[D]ヘルム（ステージ II から）" }, lines);
    }

    [Fact]
    public void まだの所の幸福のアイコンは薄くする()
    {
        var offer = new TradeOffer("f", "家具職人", null, TradeSide.Sell, TradeState.NotBuilt, 1, true);

        Assert.Equal("[D]家具職人（未建設） [D]<sprite name=\"happiness\">", TradeText.Entry(offer, true, true, true, Words()));
    }

    [Fact]
    public void 建てていない店が上の段階から扱うなら_両方を書く()
    {
        var offer = new TradeOffer("f", "家具職人", null, TradeSide.Buy, TradeState.NotBuilt, 2, false);

        Assert.Equal("[D]家具職人（未建設、ステージ II から）", TradeText.Entry(offer, true, true, true, Words()));
    }

    [Fact]
    public void 場所と値段と幸福は設定で外せる()
    {
        var offer = new TradeOffer("f", "家具職人", "町", TradeSide.Sell, TradeState.Now, 1, true) { Price = 9 };
        var options = new TradeOptions { Place = ShowWhen.Never, Price = ShowWhen.WhileKey, Happiness = ShowWhen.Never };

        Assert.Equal(new[] { "[H]売れる: 家具職人" }, TradeText.Lines(Groups(offer), options, Words()));

        options.KeyHeld = true;
        Assert.Equal(new[] { "[H]売れる: 家具職人 $9" }, TradeText.Lines(Groups(offer), options, Words()));
    }

    [Fact]
    public void 場所の分からない所と値段の分からない所は_括弧と値段を付けない()
    {
        var offer = new TradeOffer("w", "ハロルド", null, TradeSide.Buy, TradeState.Now, 1, false) { Price = 0 };

        Assert.Equal("ハロルド", TradeText.Entry(offer, true, true, true, Words()));
    }

    [Fact]
    public void まだの所には値段を付けない()
    {
        var offer = new TradeOffer("h", "ヘルム", null, TradeSide.Buy, TradeState.LaterTier, 3, false) { Price = 50 };

        Assert.Equal("[D]ヘルム（ステージ III から）", TradeText.Entry(offer, true, true, true, Words()));
    }

    [Fact]
    public void 何も無ければ空()
    {
        Assert.Empty(TradeText.Lines(new List<TradeGroup>(), new TradeOptions(), Words()));
        Assert.Empty(TradeText.Lines(null, null, null));
    }
}
