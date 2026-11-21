using System;
using System.Collections.Generic;

namespace CraftTodo.Core
{
    /// <summary>
    /// どの窓から足したか。建物の窓（設置・拡張）と製作の窓で、同じ id でも別のものになる。
    /// 町の建物の窓（0.7.1〜）は製作（ゲームが建てるときに作る「town_building_craft:＋建物の id」の製作）として持つ。
    /// </summary>
    public enum TodoKind
    {
        Build = 0,
        Craft = 1,
    }

    /// <summary>
    /// 集める素材 1 つ。<see cref="Count"/> は 1 回分の数。要る数はメモの回数から <see cref="TodoEntry.NeedOf"/> で出す。
    /// </summary>
    public sealed class TodoMaterial
    {
        public TodoMaterial(string itemId, int count, bool isGroup, bool scales = true)
        {
            ItemId = itemId ?? string.Empty;
            Count = Math.Max(0, count);
            IsGroup = isGroup;
            Scales = scales;
        }

        /// <summary>アイテムの id。<see cref="IsGroup"/> のときはグループの id（どの種類でもよい素材）。</summary>
        public string ItemId { get; }

        /// <summary>1 回分の数。</summary>
        public int Count { get; }

        public bool IsGroup { get; }

        /// <summary>回数に応じて増えるか。耐久を使う道具（何回作っても 1 本あればよい）は false。</summary>
        public bool Scales { get; }
    }

    /// <summary>
    /// 製作メモの 1 件。名前・アイコン・在庫はゲームの中で id から引き直すので、ここには id だけを持つ
    /// （言語を切り替えても、保存したメモの表示が追随する）。
    /// </summary>
    public sealed class TodoEntry
    {
        /// <summary>回数の上限（ゲームの回数を選ぶ窓の上限と同じ）。</summary>
        public const int MaxMultiplier = 999;

        public TodoEntry(TodoKind kind, string recipeId, string stationId, string stationDefId, string iconId, int multiplier, IEnumerable<TodoMaterial> materials)
        {
            Kind = kind;
            RecipeId = recipeId ?? string.Empty;
            StationId = stationId ?? string.Empty;
            StationDefId = stationDefId ?? string.Empty;
            IconId = iconId ?? string.Empty;
            Multiplier = ClampMultiplier(multiplier);
            Materials = Merge(materials);
        }

        public TodoKind Kind { get; }

        /// <summary>建物なら BuildingDef の id、製作なら CraftDef の id（町の建物は town_building_craft:＋TownBuildingDef の id）。</summary>
        public string RecipeId { get; }

        /// <summary>作る場所（作業台・建築机）の一意な id。在庫を数えるゾーンをここから引く。分からなければ空。</summary>
        public string StationId { get; }

        /// <summary>作る場所の定義の id（名前の表示用）。</summary>
        public string StationDefId { get; }

        /// <summary>できあがる物のアイコンの id。</summary>
        public string IconId { get; }

        /// <summary>作る回数（建物なら建てる数）。1 以上 <see cref="MaxMultiplier"/> 以下。</summary>
        public int Multiplier { get; }

        /// <summary>素材（1 回分の数）。</summary>
        public IReadOnlyList<TodoMaterial> Materials { get; }

        /// <summary>重複を見分ける鍵。同じレシピでも作る場所が違えば別のメモ（数える在庫が違う）。</summary>
        public string Key => MakeKey(Kind, RecipeId, StationId);

        public static string MakeKey(TodoKind kind, string recipeId, string stationId)
        {
            return (kind == TodoKind.Build ? "build:" : "craft:") + (recipeId ?? string.Empty) + "@" + (stationId ?? string.Empty);
        }

        /// <summary>この素材が、メモの回数分でいくつ要るか。</summary>
        public int NeedOf(TodoMaterial material)
        {
            if (material == null)
            {
                return 0;
            }

            return material.Scales ? (int)Math.Min(int.MaxValue, (long)material.Count * Multiplier) : material.Count;
        }

        /// <summary>回数だけを変えた写し。</summary>
        public TodoEntry WithMultiplier(int multiplier)
        {
            return new TodoEntry(Kind, RecipeId, StationId, StationDefId, IconId, multiplier, Materials);
        }

        internal static int ClampMultiplier(long multiplier)
        {
            return (int)Math.Max(1, Math.Min(MaxMultiplier, multiplier));
        }

        private static IReadOnlyList<TodoMaterial> Merge(IEnumerable<TodoMaterial> materials)
        {
            var merged = new List<TodoMaterial>();
            if (materials == null)
            {
                return merged;
            }

            foreach (TodoMaterial material in materials)
            {
                if (material == null || material.ItemId.Length == 0)
                {
                    continue;
                }

                // 回数で増えるものと増えないもの（道具）は足し合わせられないので別に持つ
                int index = merged.FindIndex(m => m.ItemId == material.ItemId && m.IsGroup == material.IsGroup && m.Scales == material.Scales);
                if (index < 0)
                {
                    merged.Add(material);
                }
                else
                {
                    merged[index] = new TodoMaterial(material.ItemId, merged[index].Count + material.Count, material.IsGroup, material.Scales);
                }
            }

            return merged;
        }
    }
}
