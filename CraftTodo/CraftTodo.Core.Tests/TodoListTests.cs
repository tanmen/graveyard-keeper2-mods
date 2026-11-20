using CraftTodo.Core;

namespace CraftTodo.Core.Tests;

public class TodoListTests
{
    private static TodoEntry Entry(string recipe, string station = "s1", int times = 1, params (string id, int count)[] mats)
    {
        return new TodoEntry(TodoKind.Craft, recipe, station, "table", "icon", times,
            mats.Select(m => new TodoMaterial(m.id, m.count, false)));
    }

    private static string Key(string recipe, string station = "s1") => TodoEntry.MakeKey(TodoKind.Craft, recipe, station);

    [Fact]
    public void AddCount_PutsNewestFirst()
    {
        var list = new TodoList();
        list.AddCount(Entry("salad"));
        list.AddCount(Entry("beam"));

        Assert.Equal(new[] { "beam", "salad" }, list.Entries.Select(e => e.RecipeId));
    }

    [Fact]
    public void AddCount_SameKey_AddsTimesInPlace()
    {
        var list = new TodoList();
        list.AddCount(Entry("salad", times: 1, mats: ("onion", 2)));
        list.AddCount(Entry("beam"));

        TodoEntry result = list.AddCount(Entry("salad", times: 3, mats: ("onion", 2)));

        Assert.Equal(2, list.Count);
        Assert.Equal(new[] { "beam", "salad" }, list.Entries.Select(e => e.RecipeId));
        Assert.Equal(4, result.Multiplier);
        Assert.Same(result, list.Entries[1]);
        Assert.Equal(8, result.NeedOf(result.Materials.Single()));
    }

    [Fact]
    public void AddCount_TakesMaterialsFromNewEntry()
    {
        var list = new TodoList();
        list.AddCount(Entry("salad", mats: ("onion", 2)));

        TodoEntry result = list.AddCount(Entry("salad", "s1", 1, ("onion", 3), ("carrot", 1)));

        Assert.Equal(2, result.Multiplier);
        Assert.Equal(new[] { ("onion", 3), ("carrot", 1) }, result.Materials.Select(m => (m.ItemId, m.Count)));
    }

    [Fact]
    public void AddCount_StopsAtMax()
    {
        var list = new TodoList();
        list.AddCount(Entry("salad", times: 998));

        TodoEntry result = list.AddCount(Entry("salad", times: 5));

        Assert.Equal(TodoEntry.MaxMultiplier, result.Multiplier);
    }

    [Fact]
    public void SubtractCount_LowersThenRemovesAtZero()
    {
        var list = new TodoList();
        list.AddCount(Entry("salad", times: 3));
        int changed = 0;
        list.Changed += () => changed++;

        Assert.Equal(2, list.SubtractCount(Key("salad"), 1));
        Assert.Equal(2, list.Find(Key("salad")).Multiplier);
        Assert.Equal(0, list.SubtractCount(Key("salad"), 5));
        Assert.False(list.Contains(Key("salad")));
        Assert.Equal(2, changed);
    }

    [Fact]
    public void SubtractCount_UnknownKey_DoesNotNotify()
    {
        var list = new TodoList();
        list.AddCount(Entry("salad"));
        int changed = 0;
        list.Changed += () => changed++;

        Assert.Equal(-1, list.SubtractCount(Key("beam"), 1));
        Assert.Equal(-1, list.SubtractCount(Key("salad", "s2"), 1));

        Assert.Equal(0, changed);
        Assert.Equal(1, list.Count);
    }

    [Fact]
    public void SubtractCount_KeepsPosition()
    {
        var list = new TodoList();
        list.AddCount(Entry("a", times: 2));
        list.AddCount(Entry("b"));

        list.SubtractCount(Key("a"), 1);

        Assert.Equal(new[] { "b", "a" }, list.Entries.Select(e => e.RecipeId));
    }

    [Fact]
    public void EnsureCount_DoesNotGrowOnRepeat()
    {
        var list = new TodoList();
        list.EnsureCount(Entry("salad", times: 2));
        list.EnsureCount(Entry("salad", times: 2));
        Assert.Equal(2, list.Find(Key("salad")).Multiplier);

        list.EnsureCount(Entry("salad", times: 1));
        Assert.Equal(2, list.Find(Key("salad")).Multiplier);

        list.EnsureCount(Entry("salad", times: 5));
        Assert.Equal(5, list.Find(Key("salad")).Multiplier);
        Assert.Equal(1, list.Count);
    }

    [Fact]
    public void SameRecipeAtOtherStation_IsSeparate()
    {
        var list = new TodoList();
        list.AddCount(Entry("salad", "s1"));
        list.AddCount(Entry("salad", "s2"));

        Assert.Equal(2, list.Count);
    }

    [Fact]
    public void BuildAndCraftWithSameId_AreSeparate()
    {
        var list = new TodoList();
        list.AddCount(new TodoEntry(TodoKind.Build, "x", "s1", "", "", 1, null));
        list.AddCount(new TodoEntry(TodoKind.Craft, "x", "s1", "", "", 1, null));

        Assert.Equal(2, list.Count);
    }

    [Fact]
    public void AddCount_OverMax_DropsOldest()
    {
        var list = new TodoList(maxEntries: 2);
        list.AddCount(Entry("a"));
        list.AddCount(Entry("b"));
        list.AddCount(Entry("c"));

        Assert.Equal(new[] { "c", "b" }, list.Entries.Select(e => e.RecipeId));
    }

    [Fact]
    public void LoweringMax_TrimsAndNotifies()
    {
        var list = new TodoList();
        list.AddCount(Entry("a"));
        list.AddCount(Entry("b"));
        list.AddCount(Entry("c"));
        int changed = 0;
        list.Changed += () => changed++;

        list.MaxEntries = 1;

        Assert.Equal("c", list.Entries.Single().RecipeId);
        Assert.Equal(1, changed);
    }

    [Fact]
    public void Remove_DropsWholeEntryRegardlessOfTimes()
    {
        var list = new TodoList();
        list.AddCount(Entry("a", times: 7));
        int changed = 0;
        list.Changed += () => changed++;

        Assert.False(list.Remove(Key("zzz")));
        Assert.True(list.Remove(Key("a")));

        Assert.Equal(1, changed);
        Assert.Equal(0, list.Count);
    }

    [Fact]
    public void ReplaceAll_KeepsOrderAndDropsDuplicates()
    {
        var list = new TodoList();
        list.ReplaceAll(new[] { Entry("a"), Entry("b"), Entry("a") });

        Assert.Equal(new[] { "a", "b" }, list.Entries.Select(e => e.RecipeId));
    }
}
