using QuickStash.Core;

namespace QuickStash.Core.Tests;

public class StashPlannerTests
{
    [Fact]
    public void Decide_OnlyStackable_NotLocked_NotPinned()
    {
        var locks = new LockSet();
        locks.Toggle("bread");
        var pinned = new HashSet<string> { "potion" };

        Assert.Equal(StashDecision.Stash, StashPlanner.Decide("wood", true, locks, pinned));
        Assert.Equal(StashDecision.NotStackable, StashPlanner.Decide("shovel", false, locks, pinned));
        Assert.Equal(StashDecision.Locked, StashPlanner.Decide("bread", true, locks, pinned));
        Assert.Equal(StashDecision.Pinned, StashPlanner.Decide("potion", true, locks, pinned));
        Assert.Equal(StashDecision.NotStackable, StashPlanner.Decide(string.Empty, true, locks, pinned));
    }

    [Fact]
    public void Decide_LockWinsOverPinned_AndNullSetsAreEmpty()
    {
        var locks = new LockSet();
        locks.Toggle("potion");

        Assert.Equal(StashDecision.Locked, StashPlanner.Decide("potion", true, locks, new HashSet<string> { "potion" }));
        Assert.Equal(StashDecision.Stash, StashPlanner.Decide("potion", true, null, null));
    }

    [Fact]
    public void OrderTargets_MostFirst_TiesKeepOrder_DropsEmpty()
    {
        // 宝箱 0: 1 個・宝箱 1: 無し・宝箱 2: 500 個・宝箱 3: 1 個
        List<int> order = StashPlanner.OrderTargets(new[] { 1, 0, 500, 1 });

        Assert.Equal(new[] { 2, 0, 3 }, order);
    }

    [Fact]
    public void OrderTargets_NothingAnywhere_IsEmpty()
    {
        Assert.Empty(StashPlanner.OrderTargets(new[] { 0, 0 }));
        Assert.Empty(StashPlanner.OrderTargets(Array.Empty<int>()));
        Assert.Empty(StashPlanner.OrderTargets(null));
    }
}
