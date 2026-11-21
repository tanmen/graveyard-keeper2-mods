using StockFinder.Core;

namespace StockFinder.Core.Tests;

public class StockSummaryTests
{
    private static StockPlace Player(int count) => new(null, "所持品", count, isPlayer: true);

    private static StockPlace Chest(string zone, string name, int count) => new(zone, name, count);

    private static string[] Lines(StockSummary summary) => summary.Rows.Select(r => $"{r.Zone}/{r.Container}/{r.Count}").ToArray();

    [Fact]
    public void Create_PlayerFirst_ThenZonesByTotal_ThenChestsByCount()
    {
        // 図の例（木板）: 所持品 5・芝地のシンプルな宝箱 2 つに 20 と 4・墓場の大きな宝箱 8
        var places = new[]
        {
            Chest("墓場", "大きな宝箱", 8),
            Chest("芝地", "シンプルな宝箱", 4),
            Player(5),
            Chest("芝地", "シンプルな宝箱", 20),
        };

        StockSummary summary = StockSummary.Create(places, 8);

        Assert.Equal(37, summary.Total);
        Assert.Equal(new[] { "/所持品/5", "芝地/シンプルな宝箱/20", "芝地/シンプルな宝箱/4", "墓場/大きな宝箱/8" }, Lines(summary));
        Assert.Equal(0, summary.HiddenPlaces);
    }

    [Fact]
    public void Create_DropsEmptyPlacesAndNulls()
    {
        var places = new[] { Player(0), Chest("芝地", "シンプルな宝箱", 0), null, Chest("墓場", "大きな宝箱", 3), Chest("墓場", "道具棚", -1) };

        StockSummary summary = StockSummary.Create(places, 8);

        Assert.Equal(3, summary.Total);
        Assert.Equal(new[] { "墓場/大きな宝箱/3" }, Lines(summary));
    }

    [Fact]
    public void Create_NothingAnywhere_IsZeroWithoutRows()
    {
        StockSummary summary = StockSummary.Create(new[] { Player(0) }, 8);

        Assert.Equal(0, summary.Total);
        Assert.Empty(summary.Rows);
        Assert.Equal(0, summary.HiddenPlaces);
    }

    [Fact]
    public void Create_SameTotals_KeepFoundOrder()
    {
        var places = new[] { Chest("庭", "a", 2), Chest("教会", "b", 2), Chest("庭", "c", 2), Chest("教会", "d", 2) };

        StockSummary summary = StockSummary.Create(places, 8);

        Assert.Equal(new[] { "庭/a/2", "庭/c/2", "教会/b/2", "教会/d/2" }, Lines(summary));
    }

    [Fact]
    public void Create_OverTheLimit_FoldsTheRestIntoOneRow()
    {
        var places = new[] { Player(1), Chest("芝地", "a", 9), Chest("芝地", "b", 5), Chest("墓場", "c", 3), Chest("教会", "d", 2) };

        StockSummary summary = StockSummary.Create(places, 3);

        Assert.Equal(20, summary.Total);
        Assert.Equal(new[] { "/所持品/1", "芝地/a/9" }, Lines(summary));
        Assert.Equal(3, summary.HiddenPlaces);
        Assert.Equal(10, summary.HiddenCount);
    }

    [Fact]
    public void Create_ExactlyAtTheLimit_ShowsEverything()
    {
        var places = new[] { Player(1), Chest("芝地", "a", 9), Chest("墓場", "c", 3) };

        StockSummary summary = StockSummary.Create(places, 3);

        Assert.Equal(3, summary.Rows.Count);
        Assert.Equal(0, summary.HiddenPlaces);
    }

    [Fact]
    public void Create_LimitBelowOne_ActsAsOne()
    {
        StockSummary summary = StockSummary.Create(new[] { Chest("芝地", "a", 9), Chest("墓場", "c", 3) }, 0);

        Assert.Empty(summary.Rows);
        Assert.Equal(2, summary.HiddenPlaces);
        Assert.Equal(12, summary.HiddenCount);
    }
}
