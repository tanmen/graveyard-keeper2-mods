using CraftTodo.Core;

namespace CraftTodo.Core.Tests;

public class TodoEntryTests
{
    [Fact]
    public void NeedOf_ScalesWithTimesExceptTools()
    {
        var entry = new TodoEntry(TodoKind.Craft, "beam", "s", "", "", 3, new[]
        {
            new TodoMaterial("wood", 2, false),
            new TodoMaterial("saw", 1, false, scales: false),
        });

        Assert.Equal(new[] { 6, 1 }, entry.Materials.Select(entry.NeedOf));
    }

    [Fact]
    public void Multiplier_IsClamped()
    {
        Assert.Equal(1, new TodoEntry(TodoKind.Craft, "r", "s", "", "", 0, null).Multiplier);
        Assert.Equal(1, new TodoEntry(TodoKind.Craft, "r", "s", "", "", -4, null).Multiplier);
        Assert.Equal(TodoEntry.MaxMultiplier, new TodoEntry(TodoKind.Craft, "r", "s", "", "", int.MaxValue, null).Multiplier);
    }

    [Fact]
    public void WithMultiplier_KeepsEverythingElse()
    {
        var entry = new TodoEntry(TodoKind.Build, "bed", "s", "desk", "i_bed", 1, new[] { new TodoMaterial("plank", 4, false) });

        TodoEntry copy = entry.WithMultiplier(3);

        Assert.Equal(entry.Key, copy.Key);
        Assert.Equal(("desk", "i_bed", 3), (copy.StationDefId, copy.IconId, copy.Multiplier));
        Assert.Equal(12, copy.NeedOf(copy.Materials.Single()));
        Assert.Equal(1, entry.Multiplier);
    }

    [Fact]
    public void Materials_MergeSameItemButKeepToolsApart()
    {
        var entry = new TodoEntry(TodoKind.Craft, "r", "s", "", "", 2, new[]
        {
            new TodoMaterial("plank", 2, false),
            new TodoMaterial("nail", 1, false),
            new TodoMaterial("plank", 3, false),
            new TodoMaterial("plank", 1, true),
            new TodoMaterial("plank", 1, false, scales: false),
            new TodoMaterial("", 5, false),
        });

        Assert.Equal(4, entry.Materials.Count);
        Assert.Equal(5, entry.Materials.Single(m => m.ItemId == "plank" && !m.IsGroup && m.Scales).Count);
        Assert.Equal(1, entry.Materials.Single(m => m.ItemId == "plank" && m.IsGroup).Count);
        Assert.Equal(1, entry.Materials.Single(m => m.ItemId == "plank" && !m.Scales).Count);
    }
}
