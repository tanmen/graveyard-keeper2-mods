using QuickStash.Core;

namespace QuickStash.Core.Tests;

public class LockSetTests
{
    [Fact]
    public void Toggle_LocksThenUnlocks()
    {
        var locks = new LockSet();

        Assert.True(locks.Toggle("wood"));
        Assert.True(locks.Contains("wood"));
        Assert.False(locks.Toggle("wood"));
        Assert.False(locks.Contains("wood"));
        Assert.Equal(0, locks.Count);
    }

    [Fact]
    public void Contains_IsCaseSensitive_AndDistinguishesQualityVariants()
    {
        // 品質（星）の違う品は id が違う（ゲームの ItemDef.id）。別々にロックする
        var locks = new LockSet();
        locks.Toggle("carrot");

        Assert.True(locks.Contains("carrot"));
        Assert.False(locks.Contains("carrot:1"));
        Assert.False(locks.Contains("Carrot"));
        Assert.False(locks.Contains(null));
        Assert.False(locks.Contains(string.Empty));
    }

    [Fact]
    public void Toggle_RejectsEmptyId()
    {
        Assert.Throws<ArgumentException>(() => new LockSet().Toggle(string.Empty));
    }

    [Fact]
    public void SerializeThenParse_RoundTrips_SortedWithHeader()
    {
        var locks = new LockSet();
        locks.Toggle("wood");
        locks.Toggle("bread");
        locks.Toggle("stone");

        string text = locks.Serialize("Quick Stash のロック\nセーブ枠 Steam_1");

        Assert.Equal("# Quick Stash のロック\n# セーブ枠 Steam_1\nbread\nstone\nwood\n", text);
        LockSet back = LockSet.Parse(text);
        Assert.Equal(new[] { "bread", "stone", "wood" }, back.Ids);
    }

    [Fact]
    public void Parse_SkipsCommentsBlankLinesAndTrimsCrLf()
    {
        LockSet locks = LockSet.Parse("# コメント\r\n\r\n  wood  \r\nbread\r\n#stone\r\n");

        Assert.Equal(new[] { "bread", "wood" }, locks.Ids);
    }

    [Fact]
    public void Parse_EmptyOrNull_IsEmpty()
    {
        Assert.Equal(0, LockSet.Parse(null).Count);
        Assert.Equal(0, LockSet.Parse(string.Empty).Count);
    }

    [Fact]
    public void ReplaceWith_CopiesAndNullClears()
    {
        var locks = new LockSet();
        locks.Toggle("wood");
        var other = new LockSet();
        other.Toggle("bread");

        locks.ReplaceWith(other);
        Assert.Equal(new[] { "bread" }, locks.Ids);

        locks.ReplaceWith(null);
        Assert.Equal(0, locks.Count);
    }
}
