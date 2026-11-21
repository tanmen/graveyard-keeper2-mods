using CraftTodo.Core;

namespace CraftTodo.Core.Tests;

public class TodoFormatTests
{
    [Fact]
    public void RoundTrip_KeepsEverything()
    {
        var entries = new[]
        {
            new TodoEntry(TodoKind.Build, "kitchen_1", "0b6f5c1e-1111-2222-3333-444455556666", "builder_factory", "i_b_kitchen", 2, new[]
            {
                new TodoMaterial("plank", 6, false),
                new TodoMaterial("gear", 4, false),
                new TodoMaterial("any_stone", 4, true),
            }),
            new TodoEntry(TodoKind.Craft, "wooden_beam", "", "", "", 3, new[]
            {
                new TodoMaterial("wood", 1, false),
                new TodoMaterial("saw", 1, false, scales: false),
            }),
        };

        string text = TodoFormat.Write(entries);
        List<TodoEntry> read = TodoFormat.Read(text);

        Assert.StartsWith("# CraftTodo v2\n", text);
        Assert.Equal(2, read.Count);
        Assert.Equal(entries[0].Key, read[0].Key);
        Assert.Equal(2, read[0].Multiplier);
        Assert.Equal("builder_factory", read[0].StationDefId);
        Assert.Equal("i_b_kitchen", read[0].IconId);
        Assert.Equal(new[] { ("plank", 6, false, true), ("gear", 4, false, true), ("any_stone", 4, true, true) },
            read[0].Materials.Select(m => (m.ItemId, m.Count, m.IsGroup, m.Scales)));
        Assert.Equal(TodoKind.Craft, read[1].Kind);
        Assert.Equal(3, read[1].Multiplier);
        Assert.Equal("", read[1].StationId);
        Assert.Equal(new[] { 3, 1 }, read[1].Materials.Select(read[1].NeedOf));
    }

    [Fact]
    public void RoundTrip_EscapesSeparators()
    {
        var entry = new TodoEntry(TodoKind.Craft, "we\tird\\id\nx", "s", "d", "i", 1, new[] { new TodoMaterial("a\tb", 1, false) });

        TodoEntry read = TodoFormat.Read(TodoFormat.Write(new[] { entry })).Single();

        Assert.Equal("we\tird\\id\nx", read.RecipeId);
        Assert.Equal("a\tb", read.Materials.Single().ItemId);
    }

    [Fact]
    public void RoundTrip_TownBuildingCraft_IsSubtractedByTheCraftStartKey()
    {
        // 町の建物（0.7.1〜）はゲームの「town_building_craft:＋建物の id」の製作として持つ。id の「:」も読み書きでき、
        // 建てたとき（WgoData.OnCraftStart）に組む鍵 MakeKey(Craft, 製作の id, 机の id) で減る
        const string recipe = "town_building_craft:repair_big_house";
        const string station = "3f2a9c1e-0000-1111-2222-333344445555";
        var entry = new TodoEntry(TodoKind.Craft, recipe, station, "t_b_signboard_house", "i_b_house", 1, new[]
        {
            new TodoMaterial("plank", 16, false),
            new TodoMaterial("stone_block", 8, false),
            new TodoMaterial("nails", 4, false),
        });

        TodoEntry read = TodoFormat.Read(TodoFormat.Write(new[] { entry })).Single();
        var list = new TodoList();
        list.AddCount(read);

        Assert.Equal(recipe, read.RecipeId);
        Assert.Equal(new[] { 16, 8, 4 }, read.Materials.Select(read.NeedOf));
        Assert.Equal(0, list.SubtractCount(TodoEntry.MakeKey(TodoKind.Craft, recipe, station), 1));
        Assert.Equal(0, list.Count);
    }

    [Fact]
    public void Read_V1_TreatsCountsAsTotalsForOneTime()
    {
        // 0.2.0 が書いた形（本人のセーブにあるものと同じ形）。数は回数を掛けたあとの合計
        string text = string.Join("\n",
            "# CraftTodo v1",
            "entry\tcraft\tcook_vegetable_salad\ta813\tkitchen_table_t2\ti_vegetable_salad\t1",
            "mat\tgr_onions\t2\t1",
            "mat\tcarrot\t2\t0",
            "entry\tcraft\tnails\ts\td\ti\t3",
            "mat\tiron_ingot\t6\t0",
            "mat\thammer\t1\t0",
            "");

        List<TodoEntry> read = TodoFormat.Read(text);

        Assert.Equal(new[] { 1, 1 }, read.Select(e => e.Multiplier));
        Assert.Equal(new[] { ("gr_onions", 2, true, true), ("carrot", 2, false, true) },
            read[0].Materials.Select(m => (m.ItemId, read[0].NeedOf(m), m.IsGroup, m.Scales)));
        Assert.Equal(new[] { 6, 1 }, read[1].Materials.Select(read[1].NeedOf));
    }

    [Fact]
    public void Read_WithoutHeader_IsV1()
    {
        List<TodoEntry> read = TodoFormat.Read("entry\tcraft\tnails\ts\td\ti\t3\nmat\tiron_ingot\t6\t0\n");

        Assert.Equal(1, read.Single().Multiplier);
        Assert.Equal(6, read.Single().NeedOf(read.Single().Materials.Single()));
    }

    [Fact]
    public void Read_V2_MaterialWithoutScalesColumn_Scales()
    {
        List<TodoEntry> read = TodoFormat.Read("# CraftTodo v2\nentry\tcraft\tnails\ts\td\ti\t3\nmat\tiron_ingot\t2\t0\n");

        Assert.Equal(6, read.Single().NeedOf(read.Single().Materials.Single()));
    }

    [Fact]
    public void Read_SkipsBrokenLinesButKeepsTheRest()
    {
        string text = string.Join("\n",
            TodoFormat.Header,
            "entry\tbuild\tgood_1\ts\td\ti\t1",
            "mat\tplank\t6\t0\t1",
            "mat\tbroken\tsix\t0\t1",
            "mat\tweird_flag\t1\t2\t1",
            "mat\tweird_scales\t1\t0\tx",
            "entry\tunknown_kind\tx\ts\td\ti\t1",
            "mat\torphan\t1\t0\t1",
            "entry\tcraft\tgood_2\ts\td\ti\t2",
            "garbage line",
            "mat\tnail\t2\t0\t0",
            "");

        List<TodoEntry> read = TodoFormat.Read(text.Replace("\n", "\r\n"));

        Assert.Equal(new[] { "good_1", "good_2" }, read.Select(e => e.RecipeId));
        Assert.Equal("plank", read[0].Materials.Single().ItemId);
        Assert.Equal(("nail", false), (read[1].Materials.Single().ItemId, read[1].Materials.Single().Scales));
    }

    [Fact]
    public void Read_EmptyOrHeaderOnly_IsEmpty()
    {
        Assert.Empty(TodoFormat.Read(null));
        Assert.Empty(TodoFormat.Read(""));
        Assert.Empty(TodoFormat.Read(TodoFormat.Write(Array.Empty<TodoEntry>())));
    }
}
