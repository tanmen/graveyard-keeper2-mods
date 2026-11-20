using CraftTodo.Core;

namespace CraftTodo.Core.Tests;

public class TodoProgressTests
{
    private static readonly TodoEntry Kitchen = new(TodoKind.Build, "kitchen_1", "s", "", "", 1, new[]
    {
        new TodoMaterial("plank", 6, false),
        new TodoMaterial("gear", 4, false),
    });

    [Fact]
    public void Evaluate_CountsEachMaterial()
    {
        var have = new Dictionary<string, int> { ["plank"] = 2, ["gear"] = 9 };

        List<MaterialProgress> progress = TodoProgress.Evaluate(Kitchen, m => have.GetValueOrDefault(m.ItemId));

        Assert.Equal(new[] { (2, 6, false, 4), (9, 4, true, 0) }, progress.Select(p => (p.Have, p.Need, p.Done, p.Shortage)));
        Assert.False(TodoProgress.IsReady(progress));
    }

    [Fact]
    public void IsReady_WhenEverythingIsThere()
    {
        List<MaterialProgress> progress = TodoProgress.Evaluate(Kitchen, _ => 10);

        Assert.True(TodoProgress.IsReady(progress));
    }

    [Fact]
    public void Evaluate_ThrowingCounter_CountsAsZero()
    {
        List<MaterialProgress> progress = TodoProgress.Evaluate(Kitchen, m => m.ItemId == "gear" ? throw new InvalidOperationException() : 6);

        Assert.Equal(new[] { 6, 0 }, progress.Select(p => p.Have));
    }

    [Fact]
    public void Evaluate_NeedFollowsTimes()
    {
        var beams = new TodoEntry(TodoKind.Craft, "wooden_beam", "s", "", "", 4, new[]
        {
            new TodoMaterial("wood", 2, false),
            new TodoMaterial("saw", 1, false, scales: false),
        });

        List<MaterialProgress> progress = TodoProgress.Evaluate(beams, _ => 5);

        Assert.Equal(new[] { (8, false, 3), (1, true, 0) }, progress.Select(p => (p.Need, p.Done, p.Shortage)));
    }

    [Fact]
    public void IsReady_EntryWithoutMaterials_IsNotReady()
    {
        var empty = new TodoEntry(TodoKind.Craft, "r", "s", "", "", 1, null);

        Assert.False(TodoProgress.IsReady(TodoProgress.Evaluate(empty, _ => 0)));
    }
}
