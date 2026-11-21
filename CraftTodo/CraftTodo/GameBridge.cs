using System;
using System.Collections.Generic;
using CraftTodo.Core;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;

namespace CraftTodo
{
    /// <summary>
    /// ゲームの型との境目。メモの組み立て・名前とアイコンの引き方・在庫の数え方をここに集める
    /// （ゲームの更新で形が変わったら、直すのはここと Patches.cs）。
    /// 調べた根拠は CraftTodo/docs/game-api-notes.md。
    /// </summary>
    internal static class GameBridge
    {
        private static readonly List<ItemDef> NoItems = new List<ItemDef>();
        private static readonly Dictionary<string, List<ItemDef>> GroupCache = new Dictionary<string, List<ItemDef>>();

        /// <summary>遊んでいるセーブ枠の名前（Steam_1 など）。メニューやロード中は null。</summary>
        public static string CurrentSlotName()
        {
            try
            {
                MainGame game = MainGame.Instance;
                if (game == null || MainGame.PlayerData == null || game.GameSave == null)
                {
                    return null;
                }

                SaveSlotData slot = game.SaveSlotData;
                return slot == null || string.IsNullOrEmpty(slot.slotName) ? null : slot.slotName;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 遊んでいるセーブ（GameSave。ロード・新しいゲームのたびに別のものになる。メモリのキャッシュから読んでも作り直す）と、その枠。
        /// メニューやロード中は false。新しいゲームの枠は保存の日時（saveDateTime）が空。
        /// </summary>
        public static bool TryGetSession(out GameSave save, out SaveSlotData slot)
        {
            save = null;
            slot = null;
            try
            {
                MainGame game = MainGame.Instance;
                if (game == null || MainGame.PlayerData == null || game.GameSave == null || game.SaveSlotData == null)
                {
                    return false;
                }

                save = game.GameSave;
                slot = game.SaveSlotData;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>その GameSave が、いま遊んでいるセーブか（ゲームが枠を写すときの保存を除くため）。</summary>
        public static bool IsCurrentSave(GameSave save)
        {
            try
            {
                MainGame game = MainGame.Instance;
                return game != null && save != null && ReferenceEquals(game.GameSave, save);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static string CurrentLang()
        {
            try
            {
                return LLBase.CurrentLang;
            }
            catch (Exception)
            {
                return "en";
            }
        }

        // ---- メモの組み立て ----

        /// <summary>建物の窓（設置・拡張）の 1 行から。1 つ分。素材はグループ（どの種類でもよい）のまま持つ。</summary>
        public static TodoEntry FromBuild(BuildData buildData, WgoData station)
        {
            BuildingDef def = buildData?.Definition;
            if (def == null)
            {
                return null;
            }

            var materials = new List<TodoMaterial>();
            if (buildData.NeedItems != null)
            {
                foreach (NeedItemData need in buildData.NeedItems)
                {
                    if (need != null && !string.IsNullOrEmpty(need.Id))
                    {
                        materials.Add(new TodoMaterial(need.Id, need.GetCount(), need.IsGroup));
                    }
                }
            }

            return new TodoEntry(TodoKind.Build, def.id, StationIdOf(station), station?.id, buildData.IconId, 1, materials);
        }

        /// <summary>製作の窓（回数を選ぶ窓）から。回数は窓で選んでいる回数、素材の数は 1 回分。</summary>
        public static TodoEntry FromCraft(UIBaseCraftSelectionWindowData data)
        {
            CraftDef def = data?.CraftDefinition;
            if (def == null)
            {
                return null;
            }

            int times = Math.Max(1, data.CraftsCount);
            var materials = new List<TodoMaterial>();
            if (data.CraftItemCellsData != null)
            {
                foreach (UICraftItemCellData cell in data.CraftItemCellsData)
                {
                    NeedItemData need = cell?.initialItem ?? cell?.currentItem;
                    if (need == null || string.IsNullOrEmpty(need.Id))
                    {
                        continue;
                    }

                    // 耐久を使う道具は 1 本あればよい（回数で増やさない）
                    bool isTool = cell.needDurability > 0f;
                    materials.Add(isTool
                        ? new TodoMaterial(need.Id, 1, need.IsGroup, scales: false)
                        : new TodoMaterial(need.Id, need.GetCount(data.WgoData), need.IsGroup));
                }
            }

            string icon = null;
            try
            {
                icon = def.GetOutputPreview(data.WgoData)?.IconId;
            }
            catch (Exception)
            {
                // アイコンが無くてもメモは作る
            }

            return new TodoEntry(TodoKind.Craft, def.id, StationIdOf(data.WgoData), data.WgoData?.id, icon, times, materials);
        }

        /// <summary>
        /// レシピの定義から（回数を選ぶ窓を通らないとき: レシピのアイコン・キューの行など）。
        /// 素材はグループのまま、数は 1 回分。耐久を使う道具は 1 本（回数で増やさない）。
        /// </summary>
        public static TodoEntry FromCraftDef(CraftDefBase def, WgoData station, int times)
        {
            if (def == null)
            {
                return null;
            }

            times = Math.Max(1, times);
            var materials = new List<TodoMaterial>();
            if (def.needItems != null)
            {
                foreach (NeedItemData need in def.needItems)
                {
                    if (need != null && !string.IsNullOrEmpty(need.Id))
                    {
                        materials.Add(new TodoMaterial(need.Id, need.GetCount(station), need.IsGroup));
                    }
                }
            }

            if (def is CraftDef craft && craft.hasDurabilityUseItem && craft.durabilityUseItem != null && !string.IsNullOrEmpty(craft.durabilityUseItem.Id))
            {
                materials.Add(new TodoMaterial(craft.durabilityUseItem.Id, 1, craft.durabilityUseItem.IsGroup, scales: false));
            }

            string icon = null;
            try
            {
                icon = def.GetOutputPreview(station)?.IconId;
            }
            catch (Exception)
            {
                // アイコンが無くてもメモは作る
            }

            return new TodoEntry(TodoKind.Craft, def.id, StationIdOf(station), station?.id, icon, times, materials);
        }

        public static string StationIdOf(WgoData station)
        {
            try
            {
                return station?.UniqueId?.ToString() ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>建物の行が載っている窓から、その窓を開いた建築机を引く。</summary>
        public static WgoData StationOfBuildingWidget(Component widget)
        {
            UIBuildingWindow window = widget == null ? null : widget.GetComponentInParent<UIBuildingWindow>();
            if (window == null)
            {
                return null;
            }

            UIBuildingWindowData data = Traverse.Create(window).Field("data").GetValue<UIBuildingWindowData>();
            Wgo wgo = data?.AssignedWgo;
            return wgo == null ? null : wgo.Data;
        }

        /// <summary>いま設置している建物を選んだ建築机（BuildManager の currentBuildDesk）。建物の窓を開いた机と同じ。</summary>
        public static WgoData CurrentBuildDesk()
        {
            try
            {
                BuildManager manager = LazySingleton<BuildManager>.Instance;
                Wgo desk = manager == null ? null : Traverse.Create(manager).Field("currentBuildDesk").GetValue<Wgo>();
                return desk == null ? null : desk.Data;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static UIBaseCraftSelectionWindowData DataOf(UIBaseCraftSelectionWindow window)
        {
            return window == null ? null : Traverse.Create(window).Field("data").GetValue<UIBaseCraftSelectionWindowData>();
        }

        // ---- 在庫 ----

        /// <summary>
        /// 「作る場所に戻ったときに使える数」を数える関数を作る。持ち物＋作る場所のゾーンの収納
        /// （ゲームの作業台が素材を探す範囲 WgoData.GetCraftableMultiInventory と同じ考え方）。
        /// 作る場所が引けなければ持ち物だけ。
        /// </summary>
        public static Func<TodoMaterial, int> CreateCounter(TodoEntry entry)
        {
            MultiInventory inventory = CreateInventory(entry);
            return material =>
            {
                if (!material.IsGroup)
                {
                    return inventory.GetTotalCount(material.ItemId);
                }

                int sum = 0;
                foreach (ItemDef item in GroupItems(material.ItemId))
                {
                    sum += inventory.GetTotalCount(item.id);
                }

                return sum;
            };
        }

        /// <summary>
        /// 札の数え方の範囲（持ち物＋作る場所のゾーンの収納）。<see cref="CreateCounter"/> と、セルのツールチップの素材の並び
        /// （持っている数/要る数）が使う。
        /// </summary>
        public static MultiInventory CreateInventory(TodoEntry entry)
        {
            var inventory = new MultiInventory();
            PlayerData player = MainGame.PlayerData;
            if (player?.Inventory != null)
            {
                inventory.Add(player.Inventory);
            }

            WorldZoneData zone = ZoneOf(ResolveStation(entry));
            if (zone != null)
            {
                inventory.Add(new MultiInventory(zone));
            }

            return inventory;
        }

        private static WgoData ResolveStation(TodoEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.StationId))
            {
                return null;
            }

            try
            {
                return MainGame.WorldData?.GetWgoData(new SGuid(entry.StationId));
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static WorldZoneData ZoneOf(WgoData station)
        {
            try
            {
                return station?.WorldZoneData;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---- 名前とアイコン ----

        public static string RecipeName(TodoEntry entry) => Localize(entry.RecipeId);

        public static string StationName(TodoEntry entry) => string.IsNullOrEmpty(entry.StationDefId) ? string.Empty : Localize(entry.StationDefId);

        public static string MaterialName(TodoMaterial material)
        {
            if (!material.IsGroup)
            {
                ItemDef def = Item(material.ItemId);
                return def != null ? Header(def) : Localize(material.ItemId);
            }

            if (HasLocale(material.ItemId))
            {
                return Localize(material.ItemId);
            }

            List<ItemDef> items = GroupItems(material.ItemId);
            return items.Count > 0 ? Header(items[0]) + Strings.GroupSuffix : material.ItemId;
        }

        public static Sprite MaterialIcon(TodoMaterial material)
        {
            ItemDef def = RepresentativeItem(material);
            return def == null ? null : Sprite(def.iconId);
        }

        /// <summary>素材のセルに出すアイテム。グループ（どの種類でもよい）はその最初のアイテム（アイコンとツールチップが使う）。</summary>
        public static ItemDef RepresentativeItem(TodoMaterial material)
        {
            if (!material.IsGroup)
            {
                return Item(material.ItemId);
            }

            List<ItemDef> items = GroupItems(material.ItemId);
            return items.Count > 0 ? items[0] : null;
        }

        public static Sprite Sprite(string spriteId)
        {
            if (string.IsNullOrEmpty(spriteId))
            {
                return null;
            }

            try
            {
                EasySpritesCollection sprites = LazySingletonSO<EasySpritesCollection>.Instance;
                return sprites == null ? null : sprites.GetSprite(spriteId);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static List<ItemDef> GroupItems(string groupId)
        {
            if (GroupCache.TryGetValue(groupId, out List<ItemDef> cached))
            {
                return cached;
            }

            List<ItemDef> result = NoItems;
            try
            {
                var need = new NeedItemData(groupId, 1);
                if (need.TryGetGroupItemDefs(out List<ItemDef> defs) && defs != null)
                {
                    result = defs;
                }
            }
            catch (Exception)
            {
                result = NoItems;
            }

            GroupCache[groupId] = result;
            return result;
        }

        private static ItemDef Item(string itemId)
        {
            try
            {
                return GameBalance.Me?.GetDataOrNull<ItemDef>(itemId);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Header(ItemDef def)
        {
            try
            {
                return def.GetHeader();
            }
            catch (Exception)
            {
                return def.id;
            }
        }

        private static string Localize(string id)
        {
            try
            {
                return LLBase.L(id);
            }
            catch (Exception)
            {
                return id;
            }
        }

        private static bool HasLocale(string id)
        {
            try
            {
                return LLBase.HasL(id);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// レシピ・建物の素材のセルが何個になるかの分布（制作メニューの幅を決める手がかり。製作の窓と同じく、材料＋耐久を使う道具）。
        /// 2026-11-21 にゲームの資産を走査したときは材料が最大 3 種だった。版が上がって増えたらここで分かる。
        /// </summary>
        public static string DescribeRecipeShapes()
        {
            try
            {
                GameBalance balance = GameBalance.Me;
                if (balance == null)
                {
                    return "レシピの素材の数: ゲームの定義がまだ読めない";
                }

                var crafts = new SortedDictionary<int, int>();
                var builds = new SortedDictionary<int, int>();
                string widestCraft = "-";
                string widestBuild = "-";
                int craftMax = 0;
                int buildMax = 0;
                int tools = 0;
                foreach (CraftDef def in balance.craftDefs)
                {
                    if (def == null)
                    {
                        continue;
                    }

                    int cells = (def.needItems?.Count ?? 0) + (def.hasDurabilityUseItem ? 1 : 0);
                    tools += def.hasDurabilityUseItem ? 1 : 0;
                    crafts[cells] = (crafts.TryGetValue(cells, out int n) ? n : 0) + 1;
                    if (cells > craftMax)
                    {
                        craftMax = cells;
                        widestCraft = def.id;
                    }
                }

                foreach (BuildingDef def in balance.buildingDefs)
                {
                    if (def == null)
                    {
                        continue;
                    }

                    int cells = def.needItems?.Count ?? 0;
                    builds[cells] = (builds.TryGetValue(cells, out int n) ? n : 0) + 1;
                    if (cells > buildMax)
                    {
                        buildMax = cells;
                        widestBuild = def.id;
                    }
                }

                return $"レシピの素材のセルの数（材料＋耐久を使う道具）: 製作 {Distribution(crafts)}（最大 {craftMax}: {widestCraft}。道具を別に持つもの {tools} 件）"
                    + $" / 建物 {Distribution(builds)}（最大 {buildMax}: {widestBuild}）";
            }
            catch (Exception e)
            {
                return "レシピの素材の数を数えられなかった: " + e.Message;
            }
        }

        private static string Distribution(SortedDictionary<int, int> counts)
        {
            var parts = new List<string>();
            foreach (KeyValuePair<int, int> pair in counts)
            {
                parts.Add(pair.Key + " 個 " + pair.Value + " 件");
            }

            return parts.Count == 0 ? "0 件" : string.Join("・", parts);
        }

        // ---- 画面 ----

        /// <summary>ゲームの UI の根（倍率と表示・非表示がゲームの UI と揃う）。ゲームの Workshop Creator の窓と同じ取り方。</summary>
        public static Transform FindUiRoot()
        {
            GUIElements gui = GUIElements.Instance;
            if (gui == null)
            {
                return null;
            }

            Component fitter = Traverse.Create(gui).Field("uiFitter").GetValue<Component>();
            return fitter != null ? fitter.transform : gui.Root;
        }

        public static HUD FindHud() => UnityEngine.Object.FindAnyObjectByType<HUD>();

        /// <summary>登録・解除の手応え。ゲームのボタンと同じ音（LazyAudio の "gui_click"）。</summary>
        public static void PlayClick()
        {
            try
            {
                LazyAudio.PlayAndForget("gui_click");
            }
            catch (Exception)
            {
                // 音が鳴らなくても登録は済んでいる
            }
        }

        /// <summary>HUD の左上の枠（顔・エネルギー・評判）。製作メモはこのすぐ下に置く。</summary>
        public static RectTransform HudLeftGroup(HUD hud)
        {
            if (hud == null)
            {
                return null;
            }

            GameObject group = Traverse.Create(hud).Field("leftUpGroup").GetValue<GameObject>();
            return group == null ? null : group.transform as RectTransform;
        }

        /// <summary>
        /// 左上の「？」（チュートリアルの一覧を開くボタン。HUD の tutorialListBtn）。日の輪の左下の角に掛かっていて、
        /// 左上の枠（leftUpGroup）の下端より 22 下まで出ている。チュートリアルを見たことがあり、マウスで遊んでいて、
        /// 操作できる間だけ出る（HUD.UpdateTutorialListButton）。
        /// </summary>
        public static RectTransform HudTutorialButton(HUD hud)
        {
            if (hud == null)
            {
                return null;
            }

            Component button = Traverse.Create(hud).Field("tutorialListBtn").GetValue<Component>();
            return button == null ? null : button.transform as RectTransform;
        }
    }
}
