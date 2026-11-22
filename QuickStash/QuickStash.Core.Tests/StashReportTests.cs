using QuickStash.Core;

namespace QuickStash.Core.Tests;

public class StashReportTests
{
    [Fact]
    public void Message_Moved_CountsKindsAndTotal()
    {
        var report = new StashReport { Chests = 3 };
        report.AddMoved("wood", 20);
        report.AddMoved("stone", 5);
        report.AddMoved("wood", 7);

        Assert.Equal(2, report.Kinds);
        Assert.Equal(32, report.Total);
        Assert.Equal("芝地の宝箱へしまった: 2 種 32 個", report.Message(true, "芝地"));
        Assert.Equal("宝箱へしまった: 2 種 32 個", report.Message(true, null));
        Assert.Equal("Stashed 32 items (2 kinds) in Lawn", report.Message(false, "Lawn"));
    }

    [Fact]
    public void Message_MovedButSomeDidNotFit()
    {
        var report = new StashReport { Chests = 1 };
        report.AddMoved("wood", 3);
        report.AddFull("wood");
        report.AddFull("stone");

        Assert.Equal("宝箱へしまった: 1 種 3 個。入りきらない品 2 種", report.Message(true, string.Empty));
    }

    [Fact]
    public void Message_NoChests()
    {
        var report = new StashReport { Chests = 0 };

        Assert.Equal("芝地には、しまえる宝箱が無い", report.Message(true, "芝地"));
        Assert.Equal("この場所には、しまえる宝箱が無い", report.Message(true, null));
    }

    [Fact]
    public void Message_NothingMoved_TellsWhatWasKept()
    {
        var report = new StashReport { Chests = 2 };
        report.AddKept("bread", StashDecision.Locked);
        report.AddKept("bread", StashDecision.Locked);
        report.AddKept("potion", StashDecision.Pinned);
        report.AddKept("shovel", StashDecision.NotStackable);

        Assert.Equal(1, report.LockedKept);
        Assert.Equal(1, report.PinnedKept);
        Assert.Equal("しまえる品が無かった。ロックとホットバーの品 2 種は残した", report.Message(true, "芝地"));
        Assert.Equal("しまえる品が無かった", new StashReport { Chests = 2 }.Message(true, "芝地"));
    }

    [Fact]
    public void Message_AllFull()
    {
        var report = new StashReport { Chests = 2 };
        report.AddFull("wood");

        Assert.Equal("宝箱がいっぱいで、しまえなかった", report.Message(true, "芝地"));
    }

    [Fact]
    public void Message_AvoidsGlyphsMissingFromGameFont()
    {
        var report = new StashReport { Chests = 1 };
        report.AddMoved("wood", 1);
        report.AddFull("stone");
        report.AddKept("bread", StashDecision.Locked);

        foreach (string text in new[] { report.Message(true, "芝地"), new StashReport { Chests = 1 }.Message(true, "芝地") })
        {
            Assert.DoesNotContain("×", text);
            Assert.DoesNotContain("・", text);
        }
    }
}
