using System;
using System.Collections.Generic;

namespace CraftTodo.Core
{
    /// <summary>素材 1 つの集まり具合。</summary>
    public readonly struct MaterialProgress
    {
        public MaterialProgress(TodoMaterial material, int need, int have)
        {
            Material = material;
            Need = Math.Max(0, need);
            Have = Math.Max(0, have);
        }

        public TodoMaterial Material { get; }

        public int Have { get; }

        /// <summary>メモの回数分で要る数。</summary>
        public int Need { get; }

        public bool Done => Have >= Need;

        public int Shortage => Math.Max(0, Need - Have);
    }

    public static class TodoProgress
    {
        /// <summary>
        /// メモの素材ごとに、いま足りているかを数える。<paramref name="haveOf"/> は「作る場所に戻ったときに使える数」
        /// （持ち物＋作る場所のゾーンの収納）を返す。数えられなかった素材は 0 として扱う。
        /// </summary>
        public static List<MaterialProgress> Evaluate(TodoEntry entry, Func<TodoMaterial, int> haveOf)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            var result = new List<MaterialProgress>(entry.Materials.Count);
            foreach (TodoMaterial material in entry.Materials)
            {
                int have;
                try
                {
                    have = haveOf == null ? 0 : haveOf(material);
                }
                catch (Exception)
                {
                    have = 0;
                }

                result.Add(new MaterialProgress(material, entry.NeedOf(material), have));
            }

            return result;
        }

        /// <summary>
        /// メモ全部の集まり具合を、<paramref name="entries"/> と同じ順に返す。
        /// <paramref name="stack"/> が false なら件ごとに在庫を全部数える（<see cref="Evaluate"/> と同じ。同じ素材を使う件が
        /// 同じ在庫でどれもそろった扱いになる）。
        /// true なら同じ素材の要る数を積み上げる: 要る数は、その在庫を取り合う件の合計。
        /// <list type="bullet">
        /// <item>同じゾーンの件（どちらもゾーンが無い件どうしも）は要る数をそのまま足す</item>
        /// <item>別のゾーンの件は、その件のゾーンの収納で足りない分だけを足す（持ち物は全部の件で共通なので）</item>
        /// <item>グループの素材（どの種類でもよい）は、その中の決まった素材の要る数も足す（決まった素材を先に回す）。
        /// 決まった素材の方にはグループの分を足さない</item>
        /// <item>回数で増えない素材（耐久を使う道具）は積み上げない</item>
        /// </list>
        /// 数えられなかった在庫は 0 として扱う。
        /// </summary>
        public static List<List<MaterialProgress>> EvaluateAll(IReadOnlyList<TodoEntry> entries, ITodoStock stock, bool stack)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            var result = new List<List<MaterialProgress>>(entries.Count);
            if (!stack || stock == null)
            {
                foreach (TodoEntry entry in entries)
                {
                    TodoEntry e = entry;
                    result.Add(Evaluate(e, m => stock == null ? 0 : stock.Available(e, m)));
                }

                return result;
            }

            var zones = new string[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                zones[i] = Safe(() => stock.ZoneKey(entries[i]), null);
            }

            // 別のゾーンの件が持ち物から取る分を出すときの、その件のゾーンの収納（件・素材ごとに 1 度だけ数える）
            var zoneOnly = new Dictionary<(int, int), int>();

            for (int i = 0; i < entries.Count; i++)
            {
                TodoEntry entry = entries[i];
                var progress = new List<MaterialProgress>(entry.Materials.Count);
                foreach (TodoMaterial material in entry.Materials)
                {
                    int have = Safe(() => stock.Available(entry, material), 0);
                    int need = material.Scales
                        ? StackedNeed(entries, zones, i, material, stock, zoneOnly)
                        : entry.NeedOf(material);
                    progress.Add(new MaterialProgress(material, need, have));
                }

                result.Add(progress);
            }

            return result;
        }

        private static int StackedNeed(IReadOnlyList<TodoEntry> entries, string[] zones, int self, TodoMaterial material, ITodoStock stock,
            Dictionary<(int, int), int> zoneOnly)
        {
            long total = 0;
            for (int j = 0; j < entries.Count; j++)
            {
                TodoEntry other = entries[j];
                for (int k = 0; k < other.Materials.Count; k++)
                {
                    TodoMaterial m = other.Materials[k];
                    if (!m.Scales || !Competes(material, m, stock))
                    {
                        continue;
                    }

                    int amount = other.NeedOf(m);
                    if (j == self || string.Equals(zones[j], zones[self], StringComparison.Ordinal))
                    {
                        total += amount;
                        continue;
                    }

                    if (!zoneOnly.TryGetValue((j, k), out int own))
                    {
                        own = zones[j] == null ? 0 : Math.Max(0, Safe(() => stock.ZoneOnly(other, m), 0));
                        zoneOnly[(j, k)] = own;
                    }

                    total += Math.Max(0, amount - own);
                }
            }

            return (int)Math.Min(int.MaxValue, total);
        }

        /// <summary><paramref name="other"/> が <paramref name="mine"/> と同じ在庫を取り合うか。</summary>
        private static bool Competes(TodoMaterial mine, TodoMaterial other, ITodoStock stock)
        {
            if (mine.IsGroup == other.IsGroup)
            {
                return mine.ItemId == other.ItemId;
            }

            // グループの素材は、その中の決まった素材にも取られる。決まった素材の方はグループに取られない（グループは別の種類で埋められる）
            return mine.IsGroup && Safe(() => stock.GroupContains(mine.ItemId, other.ItemId), false);
        }

        private static T Safe<T>(Func<T> get, T fallback)
        {
            try
            {
                return get();
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        /// <summary>素材が全部そろったか。素材の無いメモ（窓の都合で取れなかった）はそろった扱いにしない。</summary>
        public static bool IsReady(IReadOnlyList<MaterialProgress> progress)
        {
            if (progress == null || progress.Count == 0)
            {
                return false;
            }

            foreach (MaterialProgress p in progress)
            {
                if (!p.Done)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
