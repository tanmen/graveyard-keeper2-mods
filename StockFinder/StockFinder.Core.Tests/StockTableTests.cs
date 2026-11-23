using System.Text.RegularExpressions;
using StockFinder.Core;

namespace StockFinder.Core.Tests;

public class StockTableTests
{
    // 測り方の代わり: 1 字 = 10（書式は付けない）
    private static float Measure(string s) => s.Length * 10f;

    /// <summary>
    /// 行の幅 = 最後の pos の位置＋そのあとの字の幅（1 行ずつ別の部品にして中央に置くので、全部の行で同じでないと行の頭がずれる）。
    /// </summary>
    private static float LineWidth(string line)
    {
        Match last = Regex.Matches(line, "<pos=([0-9]+)>").Last();
        return float.Parse(last.Groups[1].Value) + Measure(line.Substring(last.Index + last.Length));
    }

    [Fact]
    public void Compose_PutsPlaceOnItsOwnLineAndIndentsContainers()
    {
        var rows = new[]
        {
            new StockTableRow("所持品", "", "5"),
            new StockTableRow("庭", "庭の井戸", "284"),
            new StockTableRow("庭", "シンプルな宝箱", "248"),
            new StockTableRow("家", "宝箱", "4"),
        };

        List<string> lines = StockTable.Compose(rows, Measure, 20f, 10f, "_");

        // 左の端 = 20 + 70（シンプルな宝箱）= 90、数の列 = 100、数の幅の最大 30 → 右端 130。詰め物の幅 10
        Assert.Equal(new[]
        {
            "所持品<pos=120>5",
            "庭<pos=120>_",
            "<pos=20>庭の井戸<pos=100>284",
            "<pos=20>シンプルな宝箱<pos=100>248",
            "家<pos=120>_",
            "<pos=20>宝箱<pos=120>4",
        }, lines);
        Assert.All(lines, line => Assert.Equal(130f, LineWidth(line)));
    }

    [Fact]
    public void Compose_SinglePlaceSingleContainer_StillHasHeader()
    {
        // 宝箱が 1 つの場所も、見出しの行と宝箱の行の 2 行にする（本人が「いつもまとめた形」を選んだ。2026-11-23）
        List<string> lines = StockTable.Compose(new[] { new StockTableRow("芝地", "宝箱", "9") }, Measure, 20f, 10f, "_");

        // 左の端 = 20 + 20 = 40、数の列 = 50 → 右端 60
        Assert.Equal(new[] { "芝地<pos=50>_", "<pos=20>宝箱<pos=50>9" }, lines);
    }

    [Fact]
    public void Compose_LongPlaceWithoutContainer_PushesTheCountColumn()
    {
        var rows = new[]
        {
            new StockTableRow("ほか 12 か所", "", "40"),
            new StockTableRow("芝地", "箱", "3"),
        };

        List<string> lines = StockTable.Compose(rows, Measure, 20f, 10f, "_");

        // 左の端 = max(80, 20 + 10) = 80、数の列 = 90、数の幅の最大 20 → 右端 110
        Assert.Equal(new[] { "ほか 12 か所<pos=90>40", "芝地<pos=100>_", "<pos=20>箱<pos=100>3" }, lines);
        Assert.All(lines, line => Assert.Equal(110f, LineWidth(line)));
    }

    [Fact]
    public void Compose_NoContainers_HasNoHeader()
    {
        List<string> lines = StockTable.Compose(new[] { new StockTableRow("所持品", "", "7") }, Measure, 20f, 10f, "_");

        Assert.Equal(new[] { "所持品<pos=40>7" }, lines);
    }

    [Fact]
    public void Compose_SamePlaceAfterOtherPlace_StartsNewHeader()
    {
        // 並びは場所ごとにまとまっている前提だが、離れて同じ名前が来たら見出しを出し直す
        var rows = new[]
        {
            new StockTableRow("庭", "箱", "1"),
            new StockTableRow("家", "箱", "1"),
            new StockTableRow("庭", "箱", "1"),
        };

        List<string> lines = StockTable.Compose(rows, Measure, 20f, 10f, "_");

        Assert.Equal(6, lines.Count);
        Assert.StartsWith("庭<pos=", lines[0]);
        Assert.StartsWith("家<pos=", lines[2]);
        Assert.StartsWith("庭<pos=", lines[4]);
    }

    [Fact]
    public void Compose_LongHeader_WidensRightEdge()
    {
        var rows = new[] { new StockTableRow("執筆用の地下室", "箱", "1") };

        List<string> lines = StockTable.Compose(rows, Measure, 20f, 10f, "_");

        // 見出し 70 + 詰め物 10 = 80 が、宝箱の行（20 + 10 + 10 + 10 = 50）より長い
        Assert.Equal(new[] { "執筆用の地下室<pos=70>_", "<pos=20>箱<pos=70>1" }, lines);
    }

    [Fact]
    public void Compose_WaterTooltip_AllLinesSameWidth()
    {
        // 2026-11-23 に 1 行の表（場所  宝箱 … 数）で窓の幅を超えて崩れた水のツールチップ。1 行の表なら 200 になった
        var rows = new[]
        {
            new StockTableRow("庭", "庭の井戸 II", "284"),
            new StockTableRow("庭", "シンプルな宝箱", "248"),
            new StockTableRow("家", "宝箱", "4"),
            new StockTableRow("錬金術ラボ", "宝箱", "2"),
            new StockTableRow("執筆用の地下室", "シンプルな宝箱", "1"),
        };

        List<string> lines = StockTable.Compose(rows, Measure, 20f, 20f, "_");

        // 左の端 20 + 70 = 90・数の列 110 → 右端 140
        Assert.Equal(9, lines.Count);
        Assert.All(lines, line => Assert.Equal(140f, LineWidth(line)));
    }

    [Fact]
    public void Compose_RoundsFractionalWidthsUp()
    {
        List<string> lines = StockTable.Compose(new[] { new StockTableRow("ab", "c", "1") }, s => s.Length * 3.3f, 0.2f, 0.2f, "_");

        // 字下げ = ceil(0.2) = 1。左の端 = 1 + 3.3、数の列 = ceil(4.5) = 5 → 右端 8.3。見出しは ceil(6.6) + 3.3 = 10.3 で、こちらが広い
        Assert.Equal(new[] { "ab<pos=7>_", "<pos=1>c<pos=7>1" }, lines);
    }

    [Fact]
    public void Compose_Empty_IsEmpty()
    {
        Assert.Empty(StockTable.Compose(Array.Empty<StockTableRow>(), Measure, 20f, 10f, "_"));
    }

    [Fact]
    public void Pos_UsesInvariantDigits()
    {
        Assert.Equal("<pos=13>", StockTable.Pos(12.6f));
    }
}
