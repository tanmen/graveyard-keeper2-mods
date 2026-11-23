using System;
using System.Collections.Generic;
using System.Reflection;
using CraftTodo.Core;
using HarmonyLib;
using LazyBearTechnology;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CraftTodo
{
    /// <summary>
    /// 制作メニューのセルにゲームのツールチップ（UITooltip）を出す（0.6.0）。窓はゲームのものを 1 つ使い、中身の部品の並び
    /// （List&lt;LazyWidgetDataBase&gt;）を組み立てて public の <c>UITooltip.Show</c> に渡す。部品の組み立てはゲームの private static の
    /// メソッドと、窓が持つ文字の形（TextStyle）を反射で引いて使う（調べた根拠は docs/game-api-notes.md「ゲームのツールチップ」）。
    ///   作る物のセル: A（所持品のアイテムと同じ: 名前・説明・効果・作成場所。建物は名前と説明）
    ///   素材のセル: 作り方があれば B＋作成場所（制作の窓のレシピと同じ: 名前＋技能のアイコン＋要るレベル・効果・作成場所・要る拡張・
    ///     素材の並び（持っている数/要る数）。何通りもあれば「ほかの作り方: あと N 通り」）。作り方が無ければ A
    /// 持っている数は札と同じく、持ち物＋その札の制作機のゾーンの収納（<see cref="GameBridge.CreateInventory"/>）で数える。
    /// 引けない部品があればログに出して、ツールチップは出さない（制作メニューは壊さない）。
    /// </summary>
    internal static class TooltipBridge
    {
        private const int MaxErrorLogs = 5;

        private delegate void AddItemWidgetsFn(List<LazyWidgetDataBase> list, ItemDef itemDef, bool addHeader, string headerPrefix,
            bool notShowStudyWidget, bool nowShowCraftedAt, IReadOnlyList<ItemDef> qualityVariants);

        private delegate void AddItemEffectWidgetsFn(List<LazyWidgetDataBase> list, ItemDef itemDef, IReadOnlyList<ItemDef> qualityVariants);

        private delegate void TryAddRequiredExtensionWidgetFn(List<LazyWidgetDataBase> list, CraftDef craftDef, WgoData wgoData);

        private delegate void AddBuildingWidgetsFn(List<LazyWidgetDataBase> list, BuildingDef buildingDef, bool addHeader, string headerPrefix);

        private delegate bool TryAddDescriptionWidgetFn(List<LazyWidgetDataBase> list, string id);

        private static readonly string[] StyleNames =
        {
            "headerTextStyle", "headerBoldTextStyle", "headerBoldTextStyleNotEnough", "headerBoldTextStyleGold",
            "smallDescriptionTextStyle", "selectedSmallDescriptionTextStyle",
        };

        private static bool resolved;
        private static bool usable;
        private static FieldInfo instanceField;
        private static FieldInfo canvasField;
        private static readonly Dictionary<string, FieldInfo> StyleFields = new Dictionary<string, FieldInfo>();
        private static AddItemWidgetsFn addItemWidgets;
        private static AddItemEffectWidgetsFn addItemEffectWidgets;
        private static TryAddRequiredExtensionWidgetFn tryAddRequiredExtensionWidget;
        private static AddBuildingWidgetsFn addBuildingWidgets;
        private static TryAddDescriptionWidgetFn tryAddDescriptionWidget;

        // 作り方の索引: アイテムの id → それを作るレシピ（ゲームの順）。ゲームの「作成場所」の索引
        // （GameBalance.CreateCraftInItemsCache の craftInItemsCacheShownInTooltips）と同じ選び方
        private static Dictionary<string, List<CraftDef>> recipesByItem;
        private static bool loggedShown;
        private static bool loggedKnowledge;
        private static int errorLogs;

        /// <summary>反射で引くものと作り方の索引を用意して、結果をログに出す（制作メニューを初めて組むときに 1 度）。</summary>
        public static void Prepare()
        {
            Resolve();
            EnsureIndex();
        }

        /// <summary>
        /// <paramref name="target"/>（セル）にツールチップを出す。<paramref name="materialIndex"/> が負なら作る物、そうでなければその素材。
        /// 出せたら true。
        /// </summary>
        public static bool Show(TodoEntry entry, int materialIndex, RectTransform target)
        {
            if (entry == null || target == null || !UIMouseTooltip.IsAvailable || !Resolve())
            {
                return false;
            }

            try
            {
                UITooltip tooltip = Instance();
                if (tooltip == null)
                {
                    return false;
                }

                var list = new List<LazyWidgetDataBase>();
                if (materialIndex < 0)
                {
                    AddProduct(list, entry, tooltip);
                }
                else if (materialIndex < entry.Materials.Count)
                {
                    AddMaterial(list, entry, entry.Materials[materialIndex], tooltip);
                }

                if (list.Count == 0)
                {
                    return false;
                }

                UITooltip.Show(list, target);
                LogFirstShow(tooltip, list.Count);
                return true;
            }
            catch (Exception e)
            {
                LogError("ツールチップを組めなかった（" + entry.Key + " の " + (materialIndex < 0 ? "作る物" : "素材 " + materialIndex) + "）", e);
                return false;
            }
        }

        /// <summary>自分のセルに出ているときだけ消す（ほかの部品が出したツールチップは消さない）。</summary>
        public static void HideAt(RectTransform target, bool immediately)
        {
            if (!usable || target == null)
            {
                return;
            }

            try
            {
                if (Instance() == null || !UITooltip.IsTooltipShowingAtTarget(target))
                {
                    return;
                }

                if (immediately)
                {
                    UITooltip.HideImmediately();
                }
                else
                {
                    UITooltip.Hide();
                }
            }
            catch (Exception e)
            {
                LogError("ツールチップを消せなかった", e);
            }
        }

        // ---- 中身 ----

        /// <summary>
        /// A: 作る物。レシピはアイテムの説明（ゲームの ShowCraftOutputCell と同じ）、建物は名前と説明。
        /// 町の建物も名前と説明（ゲームの町の建物の窓はツールチップを出さないので、建物の窓と同じ形にした。建てる製作の出力は
        /// 建ったときに落ちる物で、作る物ではないので引かない）。
        /// </summary>
        private static void AddProduct(List<LazyWidgetDataBase> list, TodoEntry entry, UITooltip tooltip)
        {
            GameBalance balance = GameBalance.Me;
            if (balance == null || string.IsNullOrEmpty(entry.RecipeId))
            {
                return;
            }

            string townId = GameBridge.TownBuildingIdOf(entry);
            if (townId != null)
            {
                TownBuildingDef town = balance.GetDataOrNull<TownBuildingDef>(townId);
                if (town == null)
                {
                    return;
                }

                list.Add(new UITooltipTextWidgetData(town.GetHeader(), TextAlignmentOptions.Center, Style(tooltip, "headerTextStyle")));
                int count = list.Count;
                if (tryAddDescriptionWidget(list, town.id))
                {
                    list.Insert(count, new UITooltipSeparatorWidgetData());
                }

                return;
            }

            if (entry.Kind == TodoKind.Build)
            {
                BuildingDef building = balance.GetDataOrNull<BuildingDef>(entry.RecipeId);
                if (building == null)
                {
                    return;
                }

                addBuildingWidgets(list, building, true, string.Empty);
                int count = list.Count;
                if (tryAddDescriptionWidget(list, building.id) && count > 0)
                {
                    list.Insert(count, new UITooltipSeparatorWidgetData());
                }

                return;
            }

            CraftDefBase craft = CraftOf(entry.RecipeId);
            ItemDef item = craft == null ? null : ResultItemOf(craft);
            if (item == null)
            {
                return;
            }

            AddItemDescription(list, item, (craft as CraftDef)?.GetPossibleResultingItemDefs(false));
        }

        /// <summary>素材: 作り方があれば B＋作成場所、無ければ A。グループの素材は、アイコンに出している最初のアイテムで決める。</summary>
        private static void AddMaterial(List<LazyWidgetDataBase> list, TodoEntry entry, TodoMaterial material, UITooltip tooltip)
        {
            ItemDef item = GameBridge.RepresentativeItem(material);
            if (item == null)
            {
                return;
            }

            List<CraftDef> recipes = RecipesOf(item.id);
            if (recipes == null || recipes.Count == 0)
            {
                AddItemDescription(list, item, null);
                return;
            }

            AddRecipe(list, entry, item, recipes, tooltip);
        }

        /// <summary>
        /// A（アイテムの説明）。ゲームの所持品のツールチップと同じ部品（AddItemWidgets）から、制作に関係の無い研究とルーンの行を除く
        /// （本人の求め、0.7.4〜。0.6.0〜0.7.3 は所持品のツールチップと同じく出していた）。未完了の研究はゲームの notShowStudyWidget で止め、
        /// ルーン（調べ終えた「ルーン: …」と、まだの「ルーン:???」）と分解でもらえる科学の行は、できた行の頭の文字で外す。
        /// </summary>
        private static void AddItemDescription(List<LazyWidgetDataBase> list, ItemDef item, IReadOnlyList<ItemDef> variants)
        {
            int start = list.Count;
            addItemWidgets(list, item, true, string.Empty, true, false, variants);
            int removed = RemoveKnowledgeLines(list, start);
            if (!loggedKnowledge)
            {
                loggedKnowledge = true;
                Plugin.Log.LogInfo($"ツールチップの説明から研究とルーンの行を外す（初めての 1 回: {item.id} で {removed} 行外した・残りの部品 {list.Count - start} 個）");
            }
        }

        private static int RemoveKnowledgeLines(List<LazyWidgetDataBase> list, int start)
        {
            var prefixes = new List<string>();
            foreach (string key in new[] { "item_runes", "tut_survey_runes", "hint_science_decompose" })
            {
                string text = LLBase.L(key);
                if (!string.IsNullOrEmpty(text))
                {
                    prefixes.Add(text);
                }
            }

            int removed = 0;
            for (int i = list.Count - 1; i >= start; i--)
            {
                if (list[i] is UITooltipTextWidgetData line && line.Text != null && prefixes.Exists(p => line.Text.StartsWith(p, StringComparison.Ordinal)))
                {
                    list.RemoveAt(i);
                    removed++;
                }
            }

            if (removed > 0)
            {
                // 外した行の前後に残った区切りを 1 本にし、最後の区切りは落とす
                for (int i = list.Count - 1; i >= Math.Max(start, 1); i--)
                {
                    if (list[i] is UITooltipSeparatorWidgetData && (i == list.Count - 1 || list[i - 1] is UITooltipSeparatorWidgetData))
                    {
                        list.RemoveAt(i);
                    }
                }
            }

            return removed;
        }

        /// <summary>
        /// B＋作成場所（ゲームの ShowCraftInfo の組み立てを写し、「作成場所」の行を足す）。作り方が何通りもあれば、置いてある制作機で
        /// 作れるレシピを優先し、無ければゲームの順で最初の 1 つ。最後に「ほかの作り方: あと N 通り」。
        /// 効果はセルの素材（<paramref name="item"/>）で出す。レシピの主な出力（ゲームの TryGetResultingItemDef。出力の最初の 1 つ）が
        /// その素材でないレシピがある（伐採屋のゾンビの作業は丸太と木の棒を作り、主な出力は丸太）ので、主な出力の効果を出すのは
        /// 素材がそれと同じ（か品質の違い）のときだけ。
        /// </summary>
        private static void AddRecipe(List<LazyWidgetDataBase> list, TodoEntry entry, ItemDef item, List<CraftDef> recipes, UITooltip tooltip)
        {
            CraftDef craft = PickRecipe(recipes, out List<WgoData> placed);
            TextStyle header = Style(tooltip, "headerTextStyle");
            TextStyle small = Style(tooltip, "smallDescriptionTextStyle");
            TextStyle selected = Style(tooltip, "selectedSmallDescriptionTextStyle");

            list.Add(new UITooltipTextWidgetData(RecipeHeader(craft, item, placed, tooltip), TextAlignmentOptions.Center, header));

            List<ItemDef> variants = null;
            ItemDef result = item;
            if (IsMainOutput(craft, item))
            {
                result = craft.TryGetResultingItemDef(false) ?? item;
                variants = craft.GetPossibleResultingItemDefs(false);
            }

            if (result != null && result.type != ItemType.Preach)
            {
                int count = list.Count;
                addItemEffectWidgets(list, result, variants);
                if (list.Count > count)
                {
                    list.Insert(count, new UITooltipSeparatorWidgetData());
                }
            }

            // 作成場所の行（技術ツリーのセルと同じ書き方）。要る拡張はゲームがこの行に続けて書く
            string line = (selected.ApplyStyleToString(LLBase.L("ui_crafted_at") + ": ") ?? string.Empty) + StationNames(craft);
            AddSeparator(list);
            list.Add(new UITooltipTextWidgetData(line, TextAlignmentOptions.Center, small));
            tryAddRequiredExtensionWidget(list, craft, StationWithExtension(craft, placed));

            // 素材の並び（持っている数/要る数）。持っている数は札と同じ数え方
            var needs = new UITooltipNeedsItemWidgetData(craft, GameBridge.CreateInventory(entry));
            if (needs.CraftItemCellsData.Count > 0)
            {
                AddSeparator(list);
                list.Add(needs);
            }

            if (recipes.Count > 1)
            {
                string others = (selected.ApplyStyleToString(Strings.OtherRecipesLabel) ?? string.Empty) + Strings.OtherRecipesCount(recipes.Count - 1);
                list.Add(new UITooltipTextWidgetData(others, TextAlignmentOptions.Center, small));
            }
        }

        /// <summary>
        /// 「レシピ名 [技能のアイコン 要るレベル]」。届いていなければ数字が赤、スター制作は金の「1-N」（ゲームと同じ）。
        /// 技能と習熟は、置いてある台（作業者の付いた台を優先）で見る。
        /// </summary>
        private static string RecipeHeader(CraftDef craft, ItemDef item, List<WgoData> placed, UITooltip tooltip)
        {
            string name = RecipeName(craft, item);
            GameBalance balance = GameBalance.Me;
            WgoData at = HeaderStation(placed);
            WGODef station = at != null ? at.Definition : (craft.craftsIn.Count > 0 ? balance.GetDataOrNull<WGODef>(craft.craftsIn[0]) : null);
            TalentDef talent = station == null || string.IsNullOrEmpty(station.talent) ? null : balance.GetDataOrNull<TalentDef>(station.talent);
            if (talent == null)
            {
                return name;
            }

            string level;
            if (craft.isStarCraft)
            {
                level = Style(tooltip, "headerBoldTextStyleGold").ApplyStyleToString("1-" + craft.talentLock);
            }
            else
            {
                // ゲームの ShowCraftInfo と同じく、台に付いている作業者（ゾンビ）の習熟で見る。いなければプレイヤー
                IWorker worker = at?.Worker;
                if (worker == null)
                {
                    worker = MainGame.PlayerController;
                }

                bool reached = worker != null && worker.GetMasteryLevelForTalentBranch(station.talent, craft) >= craft.talentLock;
                TextStyle style = Style(tooltip, reached ? "headerBoldTextStyle" : "headerBoldTextStyleNotEnough");
                level = style.ApplyStyleToString(craft.talentLock.ToString());
            }

            return name + " " + (talent.id.FontIcon() + " " + (level ?? string.Empty)).NOBR();
        }

        /// <summary>
        /// レシピの名前。訳があればそれ（ゲームの製作の窓と同じ）、無ければセルの素材の名前（ゲームの技術ツリーの見出し
        /// LinkedEntityWidgetData.GetLinkedEntityHeader は作る物の名前にするが、主な出力が素材と違うレシピがあるので素材の名前）。
        /// ゾンビの作業場のレシピ（伐採屋の sawmill_wood_zombie_craft など）は訳が無く、0.7.1 までは id がそのまま見出しに出ていた。
        /// </summary>
        private static string RecipeName(CraftDef craft, ItemDef item)
        {
            if (LLBase.HasL(craft.id))
            {
                return LLBase.L(craft.id);
            }

            return item != null ? item.GetHeader() : LLBase.L(craft.id);
        }

        /// <summary>
        /// 素材がレシピの主な出力（か、そのスターの品質の違い）か。品質の違いは、主な出力と同じスターの品のまとまり
        /// （GameBalance.starGroupItemsCache）に入っている品。ゲームの GetPossibleResultingItemDefs はレシピの出力を全部並べる
        /// （伐採屋のゾンビの作業なら丸太と木の棒）ので、そこに入っているかでは見ない（0.7.3〜0.7.4 はそれで木の棒を丸太の品質の
        /// 違いと読み、効果を丸太で組み立てていた。在庫を足す mod の Stock Finder はその品を数えるので、丸太の数が出ていた）。
        /// </summary>
        private static bool IsMainOutput(CraftDef craft, ItemDef item)
        {
            ItemDef result = craft.TryGetResultingItemDef(false);
            if (result == null || item == null)
            {
                return false;
            }

            if (result.id == item.id)
            {
                return true;
            }

            if (result.qualityType != ItemDef.QualityType.Star || item.qualityType != ItemDef.QualityType.Star)
            {
                return false;
            }

            foreach (List<ItemDef> group in GameBalance.Me.starGroupItemsCache.Values)
            {
                if (group != null && group.Exists(i => i != null && i.id == result.id) && group.Exists(i => i != null && i.id == item.id))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>見出しの技能と習熟を取る台: 置いてある台のうち作業者の付いた最初のもの、無ければ最初の台（置いていなければ null）。</summary>
        private static WgoData HeaderStation(List<WgoData> placed)
        {
            foreach (WgoData station in placed)
            {
                if (station?.Worker != null)
                {
                    return station;
                }
            }

            return placed.Count > 0 ? placed[0] : null;
        }

        private static string StationNames(CraftDef craft)
        {
            var names = new List<string>();
            foreach (string station in craft.craftsIn)
            {
                names.Add(LLBase.L(station));
            }

            return string.Join(", ", names);
        }

        /// <summary>置いてある台のうち、要る拡張が付いているもの（無ければ null。ゲームは null なら「要る拡張」の行を出す）。</summary>
        private static WgoData StationWithExtension(CraftDef craft, List<WgoData> placed)
        {
            if (string.IsNullOrEmpty(craft.extensionNeedId))
            {
                return null;
            }

            foreach (WgoData station in placed)
            {
                if (station?.CraftComponent != null && station.CraftComponent.IsCraftAllowedByAttachedExtensions(craft))
                {
                    return station;
                }
            }

            return null;
        }

        private static CraftDef PickRecipe(List<CraftDef> recipes, out List<WgoData> placed)
        {
            foreach (CraftDef craft in recipes)
            {
                placed = PlacedStations(craft);
                if (placed.Count > 0)
                {
                    return craft;
                }
            }

            placed = new List<WgoData>();
            return recipes[0];
        }

        /// <summary>そのレシピを作れる制作機のうち、世界に置いてある台（WorldData の id は定義の id）。</summary>
        private static List<WgoData> PlacedStations(CraftDef craft)
        {
            var result = new List<WgoData>();
            WorldData world = MainGame.WorldData;
            if (world == null)
            {
                return result;
            }

            foreach (string station in craft.craftsIn)
            {
                List<WgoData> found = world.GetWgoDataList(station);
                if (found != null)
                {
                    result.AddRange(found);
                }
            }

            return result;
        }

        private static void AddSeparator(List<LazyWidgetDataBase> list)
        {
            if (list.Count > 0 && !(list[list.Count - 1] is UITooltipSeparatorWidgetData))
            {
                list.Add(new UITooltipSeparatorWidgetData());
            }
        }

        /// <summary>レシピの id から定義（製作・説教・調査。錬金の調合は分かっているものだけ）。名札の名前（GameBridge.RecipeName）も使う。</summary>
        internal static CraftDefBase CraftOf(string id)
        {
            if (id.StartsWith("mix", StringComparison.Ordinal))
            {
                return AlchemyMixDef.IsUnknownMixResult(id) ? null : GameBalance.GetCraftDef(id);
            }

            GameBalance balance = GameBalance.Me;
            return (CraftDefBase)balance.GetDataOrNull<CraftDef>(id) ?? (CraftDefBase)balance.GetDataOrNull<SermonDef>(id) ?? balance.GetDataOrNull<SurveyDef>(id);
        }

        /// <summary>レシピが作るアイテム（ゲームの ShowCraftOutputCell と同じく、出力の最初の 1 つ）。</summary>
        internal static ItemDef ResultItemOf(CraftDefBase craft)
        {
            if (craft is CraftDef def)
            {
                return def.TryGetResultingItemDef(false);
            }

            OutputItems output = craft.outputItems;
            if (output == null)
            {
                return null;
            }

            if (output.chanceOutputItems.Count > 0)
            {
                return ItemOf(output.chanceOutputItems[0]);
            }

            foreach (GroupChanceOutputItem group in output.groupChanceOutputItems)
            {
                if (group.chanceItems.Count > 0)
                {
                    return ItemOf(group.chanceItems[0]);
                }
            }

            return null;
        }

        private static ItemDef ItemOf(ChanceOutputItem output)
        {
            GameBalance balance = GameBalance.Me;
            if (!output.isStarGroup)
            {
                return balance.GetDataOrNull<ItemDef>(output.id);
            }

            return balance.starGroupItemsCache.TryGetValue(output.id, out List<ItemDef> items) && items.Count > 0 ? items[0] : null;
        }

        // ---- 作り方の索引 ----

        private static List<CraftDef> RecipesOf(string itemId)
        {
            EnsureIndex();
            return recipesByItem != null && recipesByItem.TryGetValue(itemId, out List<CraftDef> recipes) ? recipes : null;
        }

        /// <summary>
        /// ゲームの CreateCraftInItemsCache と同じく、隠しレシピ・rem_・set_・town_building_craft: と、ツールチップに出さないもの
        /// （doNotShowInTooltips）を除き、出力（outputItems・addItemsToWgoOnStart / OnFinish・dropFromWgoItemsEnd / Start）の
        /// アイテムごとにレシピを並べる。作成場所の無いもの・技術ツリーの仮の項目（fake_）・錬金の調合（mix。窓の形が違う）も除く。
        /// </summary>
        private static void EnsureIndex()
        {
            if (recipesByItem != null)
            {
                return;
            }

            GameBalance balance = GameBalance.Me;
            if (balance == null || balance.craftDefs == null)
            {
                return;
            }

            try
            {
                var index = new Dictionary<string, List<CraftDef>>();
                int crafts = 0;
                int unnamed = 0;
                string unnamedExample = null;
                int secondary = 0;
                string secondaryExample = null;
                foreach (CraftDef craft in balance.craftDefs)
                {
                    if (!Indexable(craft))
                    {
                        continue;
                    }

                    var outputs = new List<string>();
                    ScanOutput(balance, craft.outputItems, outputs);
                    ScanOutput(balance, craft.addItemsToWgoOnStart, outputs);
                    ScanOutput(balance, craft.addItemsToWgoOnFinish, outputs);
                    ScanNeeds(balance, craft.dropFromWgoItemsEnd, outputs);
                    ScanNeeds(balance, craft.dropFromWgoItemsStart, outputs);
                    if (outputs.Count > 0)
                    {
                        crafts++;
                        if (!LLBase.HasL(craft.id))
                        {
                            unnamed++;
                            unnamedExample = unnamedExample ?? craft.id;
                        }
                    }

                    foreach (string id in outputs)
                    {
                        if (!IsMainOutput(craft, balance.GetDataOrNull<ItemDef>(id)))
                        {
                            secondary++;
                            secondaryExample = secondaryExample ?? id + "←" + craft.id;
                        }

                        if (!index.TryGetValue(id, out List<CraftDef> recipes))
                        {
                            recipes = new List<CraftDef>();
                            index[id] = recipes;
                        }

                        if (!recipes.Contains(craft))
                        {
                            recipes.Add(craft);
                        }
                    }
                }

                int several = 0;
                int most = 0;
                string mostId = "-";
                foreach (KeyValuePair<string, List<CraftDef>> pair in index)
                {
                    several += pair.Value.Count > 1 ? 1 : 0;
                    if (pair.Value.Count > most)
                    {
                        most = pair.Value.Count;
                        mostId = pair.Key;
                    }
                }

                recipesByItem = index;
                if (index.Count == 0)
                {
                    // 0 件は成功ではない（ゲームの形が変わって出力を拾えていない）
                    Plugin.Log.LogWarning($"作り方の索引が 0 品目（レシピ {balance.craftDefs.Count} 件を見た）。素材のツールチップはアイテムの説明だけになる");
                }
                else
                {
                    Plugin.Log.LogInfo($"作り方の索引: {index.Count} 品目（2 通り以上 {several} 品目、最多 {most} 通り: {mostId}）・レシピ {crafts} 件 / 全 {balance.craftDefs.Count} 件");
                    // 名前の訳が無いレシピは、見出しを素材の名前にする（全部が訳無しなら、言語がまだ読めていない）。
                    // 主な出力でない品として入った組は、効果をその品で出す
                    Plugin.Log.LogInfo($"名前の訳が無いレシピ {unnamed} 件 / {crafts} 件（見出しは素材の名前にする。例: {unnamedExample ?? "-"}）・"
                        + $"主な出力でない品として入った組 {secondary} 組（効果はその品で出す。例: {secondaryExample ?? "-"}）");
                }
            }
            catch (Exception e)
            {
                recipesByItem = new Dictionary<string, List<CraftDef>>();
                LogError("作り方の索引を作れなかった。素材のツールチップはアイテムの説明だけになる", e);
            }
        }

        private static bool Indexable(CraftDef craft)
        {
            if (craft == null || craft.isHidden || craft.doNotShowInTooltips || craft.craftsIn == null || craft.craftsIn.Count == 0 || string.IsNullOrEmpty(craft.id))
            {
                return false;
            }

            string id = craft.id;
            return !id.StartsWith("rem_", StringComparison.Ordinal) && !id.StartsWith("set_", StringComparison.Ordinal)
                && !id.StartsWith("town_building_craft:", StringComparison.Ordinal) && !id.StartsWith("fake_", StringComparison.Ordinal)
                && !id.StartsWith("mix", StringComparison.Ordinal);
        }

        private static void ScanOutput(GameBalance balance, OutputItems output, List<string> ids)
        {
            if (output == null)
            {
                return;
            }

            foreach (ChanceOutputItem item in output.chanceOutputItems)
            {
                AddChance(balance, item, ids);
            }

            foreach (GroupChanceOutputItem group in output.groupChanceOutputItems)
            {
                foreach (ChanceOutputItem item in group.chanceItems)
                {
                    AddChance(balance, item, ids);
                }
            }
        }

        private static void AddChance(GameBalance balance, ChanceOutputItem item, List<string> ids)
        {
            if (item == null || string.IsNullOrEmpty(item.id))
            {
                return;
            }

            if (!item.isStarGroup)
            {
                AddId(ids, item.id);
                return;
            }

            if (balance.starGroupItemsCache.TryGetValue(item.id, out List<ItemDef> stars))
            {
                foreach (ItemDef star in stars)
                {
                    AddId(ids, star?.id);
                }
            }
        }

        private static void ScanNeeds(GameBalance balance, List<NeedItemData> needs, List<string> ids)
        {
            if (needs == null)
            {
                return;
            }

            foreach (NeedItemData need in needs)
            {
                if (need == null || string.IsNullOrEmpty(need.id))
                {
                    continue;
                }

                Dictionary<string, List<ItemDef>> groups = need.groupType == ItemGroup.Star ? balance.starGroupItemsCache
                    : need.groupType == ItemGroup.Common ? balance.groupItemsCache : null;
                if (groups == null)
                {
                    AddId(ids, need.id);
                }
                else if (groups.TryGetValue(need.id, out List<ItemDef> items))
                {
                    foreach (ItemDef item in items)
                    {
                        AddId(ids, item?.id);
                    }
                }
            }
        }

        private static void AddId(List<string> ids, string id)
        {
            if (!string.IsNullOrEmpty(id) && !ids.Contains(id))
            {
                ids.Add(id);
            }
        }

        // ---- 反射 ----

        private static UITooltip Instance() => instanceField?.GetValue(null) as UITooltip;

        private static TextStyle Style(UITooltip tooltip, string name)
        {
            TextStyle style = StyleFields.TryGetValue(name, out FieldInfo field) ? field.GetValue(tooltip) as TextStyle : null;
            if (style == null)
            {
                throw new InvalidOperationException("ゲームのツールチップの文字の形 " + name + " が空");
            }

            return style;
        }

        /// <summary>ゲームの private のものを引く（1 度だけ）。1 つでも引けなければ、ツールチップは出さない。</summary>
        private static bool Resolve()
        {
            if (resolved)
            {
                return usable;
            }

            resolved = true;
            var missing = new List<string>();
            try
            {
                Type type = typeof(UITooltip);
                instanceField = AccessTools.Field(type, "instance");
                if (instanceField == null || !instanceField.IsStatic)
                {
                    missing.Add("instance");
                }

                canvasField = AccessTools.Field(type, "canvas");
                foreach (string name in StyleNames)
                {
                    FieldInfo field = AccessTools.Field(type, name);
                    if (field == null || field.FieldType != typeof(TextStyle))
                    {
                        missing.Add(name);
                    }
                    else
                    {
                        StyleFields[name] = field;
                    }
                }

                addItemWidgets = Bind<AddItemWidgetsFn>(type, "AddItemWidgets", missing,
                    typeof(List<LazyWidgetDataBase>), typeof(ItemDef), typeof(bool), typeof(string), typeof(bool), typeof(bool), typeof(IReadOnlyList<ItemDef>));
                addItemEffectWidgets = Bind<AddItemEffectWidgetsFn>(type, "AddItemEffectWidgets", missing,
                    typeof(List<LazyWidgetDataBase>), typeof(ItemDef), typeof(IReadOnlyList<ItemDef>));
                tryAddRequiredExtensionWidget = Bind<TryAddRequiredExtensionWidgetFn>(type, "TryAddRequiredExtensionWidget", missing,
                    typeof(List<LazyWidgetDataBase>), typeof(CraftDef), typeof(WgoData));
                addBuildingWidgets = Bind<AddBuildingWidgetsFn>(type, "AddBuildingWidgets", missing,
                    typeof(List<LazyWidgetDataBase>), typeof(BuildingDef), typeof(bool), typeof(string));
                tryAddDescriptionWidget = Bind<TryAddDescriptionWidgetFn>(type, "TryAddDescriptionWidget", missing,
                    typeof(List<LazyWidgetDataBase>), typeof(string));
            }
            catch (Exception e)
            {
                missing.Add(e.GetType().Name + ": " + e.Message);
            }

            usable = missing.Count == 0;
            if (usable)
            {
                Plugin.Log.LogInfo($"ゲームのツールチップを使う（部品の組み立て 5・文字の形 {StyleFields.Count} を引いた）");
            }
            else
            {
                Plugin.Log.LogWarning("ゲームのツールチップの部品が引けないので、セルにツールチップを出さない: " + string.Join(", ", missing));
            }

            return usable;
        }

        private static T Bind<T>(Type type, string name, List<string> missing, params Type[] parameters) where T : Delegate
        {
            MethodInfo method = AccessTools.Method(type, name, parameters);
            if (method == null || !method.IsStatic)
            {
                missing.Add(name);
                return null;
            }

            try
            {
                return (T)Delegate.CreateDelegate(typeof(T), method);
            }
            catch (ArgumentException)
            {
                missing.Add(name + "（形が違う）");
                return null;
            }
        }

        private static void LogFirstShow(UITooltip tooltip, int widgets)
        {
            if (loggedShown)
            {
                return;
            }

            loggedShown = true;
            Canvas canvas = canvasField?.GetValue(tooltip) as Canvas;
            string order = canvas == null ? "?" : canvas.sortingOrder.ToString();
            Plugin.Log.LogInfo($"ゲームのツールチップを初めて出した（部品 {widgets} 個。窓の並び順 {order}、制作メニューは {TodoOverlay.SortingOrder}）");
        }

        private static void LogError(string what, Exception e)
        {
            if (errorLogs >= MaxErrorLogs)
            {
                return;
            }

            errorLogs++;
            Plugin.Log.LogError($"{what}: {e}" + (errorLogs == MaxErrorLogs ? "（以後は出さない）" : string.Empty));
        }
    }
}
