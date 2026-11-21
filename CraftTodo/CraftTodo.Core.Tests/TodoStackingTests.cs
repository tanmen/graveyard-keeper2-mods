using CraftTodo.Core;

namespace CraftTodo.Core.Tests;

/// <summary>同じ素材を積み上げて数える（<see cref="TodoProgress.EvaluateAll"/> の stack = true）。</summary>
public class TodoStackingTests
{
    /// <summary>持ち物は全部の件で共通、ゾーンの収納はゾーンごと。</summary>
    private sealed class FakeStock : ITodoStock
    {
        public readonly Dictionary<string, int> Player = new();
        public readonly Dictionary<string, Dictionary<string, int>> Zones = new();
        public readonly Dictionary<string, string> ZoneOfStation = new();
        public readonly Dictionary<string, string[]> Groups = new();

        public int Available(TodoEntry entry, TodoMaterial material) =>
            Count(Player, material) + ZoneOnly(entry, material);

        public int ZoneOnly(TodoEntry entry, TodoMaterial material)
        {
            string zone = ZoneKey(entry);
            return zone != null && Zones.TryGetValue(zone, out Dictionary<string, int> stock) ? Count(stock, material) : 0;
        }

        public string ZoneKey(TodoEntry entry) => ZoneOfStation.GetValueOrDefault(entry.StationId);

        public bool GroupContains(string groupId, string itemId) =>
            Groups.TryGetValue(groupId, out string[] items) && items.Contains(itemId);

        private int Count(Dictionary<string, int> stock, TodoMaterial material) =>
            material.IsGroup
                ? Groups.GetValueOrDefault(material.ItemId, Array.Empty<string>()).Sum(i => stock.GetValueOrDefault(i))
                : stock.GetValueOrDefault(material.ItemId);
    }

    private static TodoEntry Entry(string recipe, string station, int times, params TodoMaterial[] materials) =>
        new(TodoKind.Craft, recipe, station, "anvil", "", times, materials);

    private static TodoMaterial Item(string id, int count) => new(id, count, false);

    // 2026-11-21 の本人の例: 鉄の地金 ×4 を使う件が 2 つあると、×4 持った時点で両方そろった扱いになる
    private static readonly TodoEntry Sword = Entry("sword_2", "anvil_a", 2, Item("stick", 2), Item("detail_1_iron", 3), Item("ingot_iron", 2));
    private static readonly TodoEntry Armor = Entry("armor_2", "anvil_a", 1, Item("detail_1_iron", 2), Item("ingot_iron", 4), Item("fabric_flax", 2));

    private static FakeStock Smithy()
    {
        var stock = new FakeStock();
        stock.Player["ingot_iron"] = 4;
        stock.Player["detail_1_iron"] = 6;
        stock.Player["fabric_flax"] = 5;
        stock.Player["stick"] = 10;
        stock.ZoneOfStation["anvil_a"] = "town";
        return stock;
    }

    [Fact]
    public void WithoutStacking_EachEntryCountsTheWholeStock()
    {
        List<List<MaterialProgress>> all = TodoProgress.EvaluateAll(new[] { Sword, Armor }, Smithy(), stack: false);

        Assert.Equal(new[] { (10, 4), (6, 6), (4, 4) }, all[0].Select(p => (p.Have, p.Need)));
        Assert.Equal(new[] { (6, 2), (4, 4), (5, 2) }, all[1].Select(p => (p.Have, p.Need)));
        Assert.True(TodoProgress.IsReady(all[0]));
        Assert.True(TodoProgress.IsReady(all[1]));
    }

    [Fact]
    public void Stacking_SameZone_NeedIsTheSumOfBoth()
    {
        List<List<MaterialProgress>> all = TodoProgress.EvaluateAll(new[] { Sword, Armor }, Smithy(), stack: true);

        // 剣 ×2: 木の棒 4（剣だけ）・鉄の部品 6+2・鉄の地金 4+4。よろい: 鉄の部品 2+6・鉄の地金 4+4・布 2（よろいだけ）
        Assert.Equal(new[] { (10, 4), (6, 8), (4, 8) }, all[0].Select(p => (p.Have, p.Need)));
        Assert.Equal(new[] { (6, 8), (4, 8), (5, 2) }, all[1].Select(p => (p.Have, p.Need)));
        Assert.False(TodoProgress.IsReady(all[0]));
        Assert.False(TodoProgress.IsReady(all[1]));
    }

    [Fact]
    public void Stacking_EnoughForBoth_BothAreReady()
    {
        FakeStock stock = Smithy();
        stock.Player["ingot_iron"] = 8;
        stock.Player["detail_1_iron"] = 8;

        List<List<MaterialProgress>> all = TodoProgress.EvaluateAll(new[] { Sword, Armor }, stock, stack: true);

        Assert.True(TodoProgress.IsReady(all[0]));
        Assert.True(TodoProgress.IsReady(all[1]));
    }

    [Fact]
    public void Stacking_OtherZone_AddsOnlyWhatItsOwnStorageLacks()
    {
        var stock = new FakeStock();
        stock.Player["ingot_iron"] = 4;
        stock.ZoneOfStation["anvil_a"] = "town";
        stock.ZoneOfStation["anvil_b"] = "graveyard";
        stock.Zones["graveyard"] = new Dictionary<string, int> { ["ingot_iron"] = 3 };
        TodoEntry inTown = Entry("a", "anvil_a", 1, Item("ingot_iron", 4));
        TodoEntry inGraveyard = Entry("b", "anvil_b", 1, Item("ingot_iron", 4));

        List<List<MaterialProgress>> all = TodoProgress.EvaluateAll(new[] { inTown, inGraveyard }, stock, stack: true);

        // 町の件: 持ち物 4。墓場の件は墓場の収納 3 で足りない 1 だけを持ち物から取る → 4/5
        Assert.Equal((4, 5), (all[0][0].Have, all[0][0].Need));
        // 墓場の件: 持ち物 4 ＋ 墓場 3。町の件は町に収納が無いので 4 全部を持ち物から取る → 7/8
        Assert.Equal((7, 8), (all[1][0].Have, all[1][0].Need));
    }

    [Fact]
    public void Stacking_OtherZoneWithEnoughStorage_DoesNotTakeFromThePlayer()
    {
        var stock = new FakeStock();
        stock.Player["ingot_iron"] = 4;
        stock.ZoneOfStation["anvil_a"] = "town";
        stock.ZoneOfStation["anvil_b"] = "graveyard";
        stock.Zones["graveyard"] = new Dictionary<string, int> { ["ingot_iron"] = 4 };
        TodoEntry inTown = Entry("a", "anvil_a", 1, Item("ingot_iron", 4));
        TodoEntry inGraveyard = Entry("b", "anvil_b", 1, Item("ingot_iron", 4));

        List<List<MaterialProgress>> all = TodoProgress.EvaluateAll(new[] { inTown, inGraveyard }, stock, stack: true);

        Assert.Equal((4, 4), (all[0][0].Have, all[0][0].Need));
        Assert.Equal((8, 8), (all[1][0].Have, all[1][0].Need));
    }

    [Fact]
    public void Stacking_EntriesWithoutZone_ShareThePlayerInventory()
    {
        var stock = new FakeStock();
        stock.Player["plank"] = 6;
        TodoEntry a = Entry("a", "", 1, Item("plank", 4));
        TodoEntry b = Entry("b", "", 1, Item("plank", 4));

        List<List<MaterialProgress>> all = TodoProgress.EvaluateAll(new[] { a, b }, stock, stack: true);

        Assert.Equal(new[] { (6, 8), (6, 8) }, all.Select(p => (p[0].Have, p[0].Need)));
    }

    [Fact]
    public void Stacking_GroupAlsoCountsTheSpecificItemsInIt_ButNotTheOtherWay()
    {
        var stock = new FakeStock();
        stock.Groups["gr_veg"] = new[] { "carrot", "onion" };
        stock.Player["carrot"] = 2;
        stock.Player["onion"] = 2;
        TodoEntry salad = Entry("salad", "", 1, new TodoMaterial("gr_veg", 2, true));
        TodoEntry soup = Entry("soup", "", 1, Item("carrot", 2));

        List<List<MaterialProgress>> all = TodoProgress.EvaluateAll(new[] { salad, soup }, stock, stack: true);

        // サラダ（どの野菜でも 2）: 野菜 4、要るのはサラダ 2 ＋ スープのニンジン 2 → 4/4
        Assert.Equal((4, 4), (all[0][0].Have, all[0][0].Need));
        // スープ（ニンジン 2）: グループの分は足さない → 2/2
        Assert.Equal((2, 2), (all[1][0].Have, all[1][0].Need));
        Assert.True(TodoProgress.IsReady(all[0]));

        stock.Player["onion"] = 0;
        all = TodoProgress.EvaluateAll(new[] { salad, soup }, stock, stack: true);
        Assert.Equal((2, 4), (all[0][0].Have, all[0][0].Need));
        Assert.True(TodoProgress.IsReady(all[1]));
    }

    [Fact]
    public void Stacking_ToolsThatDoNotScale_AreNotStacked()
    {
        var stock = new FakeStock();
        stock.Player["saw"] = 1;
        var tool = new TodoMaterial("saw", 1, false, scales: false);
        TodoEntry a = Entry("a", "", 3, tool);
        TodoEntry b = Entry("b", "", 1, tool);

        List<List<MaterialProgress>> all = TodoProgress.EvaluateAll(new[] { a, b }, stock, stack: true);

        Assert.Equal(new[] { (1, 1), (1, 1) }, all.Select(p => (p[0].Have, p[0].Need)));
    }

    [Fact]
    public void Stacking_FollowsTheTimesOfEachEntry()
    {
        var stock = new FakeStock();
        stock.Player["wood"] = 10;
        TodoEntry beams = Entry("beam", "", 3, Item("wood", 2));
        TodoEntry fence = Entry("fence", "", 2, Item("wood", 1));

        List<List<MaterialProgress>> all = TodoProgress.EvaluateAll(new[] { beams, fence }, stock, stack: true);

        Assert.Equal(new[] { 8, 8 }, all.Select(p => p[0].Need));
    }

    [Fact]
    public void Stacking_ThrowingStock_CountsAsZero()
    {
        var stock = new ThrowingStock();

        List<List<MaterialProgress>> all = TodoProgress.EvaluateAll(new[] { Sword, Armor }, stock, stack: true);

        Assert.All(all.SelectMany(p => p), p => Assert.Equal(0, p.Have));
        Assert.Equal(8, all[0][2].Need);
    }

    private sealed class ThrowingStock : ITodoStock
    {
        public int Available(TodoEntry entry, TodoMaterial material) => throw new InvalidOperationException();

        public int ZoneOnly(TodoEntry entry, TodoMaterial material) => throw new InvalidOperationException();

        public string ZoneKey(TodoEntry entry) => throw new InvalidOperationException();

        public bool GroupContains(string groupId, string itemId) => throw new InvalidOperationException();
    }
}
