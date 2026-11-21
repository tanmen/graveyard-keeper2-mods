using System.Text.RegularExpressions;
using StockFinder.Core;

namespace StockFinder.Core.Tests;

public class StockTableTests
{
    // 測り方の代わり: 1 字 = 10（書式は付けない）
    private static float Measure(string s) => s.Length * 10f;

    /// <summary>行の幅 = 最後の pos の位置＋数の幅（1 行ずつ別の部品にして中央に置くので、全部の行で同じでないと列がずれる）。</summary>
    private static float LineWidth(string line, string count)
    {
        MatchCollection pos = Regex.Matches(line, "<pos=([0-9]+)>");
        Assert.NotEmpty(pos);
        return float.Parse(pos[pos.Count - 1].Groups[1].Value) + Measure(count);
    }

    [Fact]
    public void Compose_AlignsContainersAndRightAlignsCounts()
    {
        var rows = new[]
        {
            new StockTableRow("所持品", "", "5"),
            new StockTableRow("芝地", "シンプルな宝箱", "20"),
            new StockTableRow("墓場", "大きな宝箱", "8"),
        };

        List<string> lines = StockTable.Compose(rows, Measure, 5f, 10f);

        // 宝箱の列 = 場所の名前の最大 20 + 5 = 25。左の端 = 25 + 70 = 95（所持品の 30 より長い）。数の列 = 95 + 10 = 105、数の幅の最大 20
        Assert.Equal(new[]
        {
            "所持品<pos=115>5",
            "芝地<pos=25>シンプルな宝箱<pos=105>20",
            "墓場<pos=25>大きな宝箱<pos=115>8",
        }, lines);
        Assert.All(lines.Zip(rows), x => Assert.Equal(125f, LineWidth(x.First, x.Second.Count)));
    }

    [Fact]
    public void Compose_LongPlaceWithoutContainer_PushesOnlyTheCountColumn()
    {
        var rows = new[]
        {
            new StockTableRow("ほか 12 か所", "", "40"),
            new StockTableRow("芝地", "箱", "3"),
        };

        List<string> lines = StockTable.Compose(rows, Measure, 5f, 10f);

        // 宝箱の列は芝地（20）だけで決まる = 25。左の端 = max(25 + 10, 80) = 80
        Assert.Equal(new[] { "ほか 12 か所<pos=90>40", "芝地<pos=25>箱<pos=100>3" }, lines);
        Assert.All(lines.Zip(rows), x => Assert.Equal(110f, LineWidth(x.First, x.Second.Count)));
    }

    [Fact]
    public void Compose_NoContainers_HasNoContainerColumn()
    {
        List<string> lines = StockTable.Compose(new[] { new StockTableRow("所持品", "", "7") }, Measure, 5f, 10f);

        Assert.Equal(new[] { "所持品<pos=40>7" }, lines);
    }

    [Fact]
    public void Compose_RoundsFractionalWidthsUp()
    {
        List<string> lines = StockTable.Compose(new[] { new StockTableRow("ab", "c", "1") }, s => s.Length * 3.3f, 0.2f, 0.2f);

        // 宝箱の列 = ceil(6.6 + 0.2) = 7、左の端 = 7 + 3.3、数の列 = ceil(10.5) = 11
        Assert.Equal(new[] { "ab<pos=7>c<pos=11>1" }, lines);
    }

    [Fact]
    public void Compose_Empty_IsEmpty()
    {
        Assert.Empty(StockTable.Compose(Array.Empty<StockTableRow>(), Measure, 5f, 10f));
    }

    [Fact]
    public void Pos_UsesInvariantDigits()
    {
        Assert.Equal("<pos=13>", StockTable.Pos(12.6f));
    }
}
