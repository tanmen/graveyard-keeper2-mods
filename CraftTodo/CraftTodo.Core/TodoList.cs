using System;
using System.Collections.Generic;

namespace CraftTodo.Core
{
    /// <summary>
    /// 製作メモの一覧。新しく足したものが先頭に来る。上限を超えたら古いものから落とす。
    /// 同じ鍵（同じレシピ・同じ作る場所）のメモは 1 件にまとめ、回数を足し引きする。
    /// </summary>
    public sealed class TodoList
    {
        private readonly List<TodoEntry> entries = new List<TodoEntry>();
        private int maxEntries;

        public TodoList(int maxEntries = 10)
        {
            this.maxEntries = Math.Max(1, maxEntries);
        }

        /// <summary>一覧が変わったとき（足す・消す・読み込み）。表示の作り直しと保存がこれを聞く。</summary>
        public event Action Changed;

        public IReadOnlyList<TodoEntry> Entries => entries;

        public int Count => entries.Count;

        public int MaxEntries
        {
            get => maxEntries;
            set
            {
                maxEntries = Math.Max(1, value);
                if (Trim())
                {
                    Changed?.Invoke();
                }
            }
        }

        /// <summary>
        /// <paramref name="entry"/> の回数分を足す。同じ鍵があれば回数を足し（位置はそのまま。素材の定義は新しい方にする）、
        /// 無ければ先頭に置く。足したあとのメモを返す。
        /// </summary>
        public TodoEntry AddCount(TodoEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            int existing = IndexOf(entry.Key);
            if (existing < 0)
            {
                return Insert(entry);
            }

            TodoEntry merged = entry.WithMultiplier(TodoEntry.ClampMultiplier((long)entries[existing].Multiplier + entry.Multiplier));
            entries[existing] = merged;
            Changed?.Invoke();
            return merged;
        }

        /// <summary>
        /// 少なくとも <paramref name="entry"/> の回数分をメモに残す（作れなかったときに自動で足す用。何度呼んでも増え続けない）。
        /// 同じ鍵があれば、回数は多い方・素材の定義は新しい方にする（位置はそのまま）。
        /// </summary>
        public TodoEntry EnsureCount(TodoEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            int existing = IndexOf(entry.Key);
            if (existing < 0)
            {
                return Insert(entry);
            }

            TodoEntry merged = entry.WithMultiplier(Math.Max(entries[existing].Multiplier, entry.Multiplier));
            entries[existing] = merged;
            Changed?.Invoke();
            return merged;
        }

        /// <summary>
        /// 回数を <paramref name="count"/> だけ減らす。0 以下になったら消す。
        /// 減らしたあとの回数（消えたら 0）を返す。そのメモが無ければ -1。
        /// </summary>
        public int SubtractCount(string key, int count)
        {
            int index = IndexOf(key);
            if (index < 0)
            {
                return -1;
            }

            int remaining = entries[index].Multiplier - Math.Max(1, count);
            if (remaining <= 0)
            {
                entries.RemoveAt(index);
                remaining = 0;
            }
            else
            {
                entries[index] = entries[index].WithMultiplier(remaining);
            }

            Changed?.Invoke();
            return remaining;
        }

        public bool Contains(string key) => IndexOf(key) >= 0;

        public TodoEntry Find(string key)
        {
            int index = IndexOf(key);
            return index < 0 ? null : entries[index];
        }

        /// <summary>回数に関わらず 1 件まるごと消す。</summary>
        public bool Remove(string key)
        {
            int index = IndexOf(key);
            if (index < 0)
            {
                return false;
            }

            entries.RemoveAt(index);
            Changed?.Invoke();
            return true;
        }

        public void Clear()
        {
            if (entries.Count == 0)
            {
                return;
            }

            entries.Clear();
            Changed?.Invoke();
        }

        /// <summary>保存してあった一覧で置き換える（並びは保存した順のまま）。</summary>
        public void ReplaceAll(IEnumerable<TodoEntry> items)
        {
            entries.Clear();
            if (items != null)
            {
                var seen = new HashSet<string>();
                foreach (TodoEntry item in items)
                {
                    if (item != null && seen.Add(item.Key))
                    {
                        entries.Add(item);
                    }
                }
            }

            Trim();
            Changed?.Invoke();
        }

        private TodoEntry Insert(TodoEntry entry)
        {
            entries.Insert(0, entry);
            Trim();
            Changed?.Invoke();
            return entry;
        }

        private int IndexOf(string key)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Key == key)
                {
                    return i;
                }
            }

            return -1;
        }

        private bool Trim()
        {
            if (entries.Count <= maxEntries)
            {
                return false;
            }

            entries.RemoveRange(maxEntries, entries.Count - maxEntries);
            return true;
        }
    }
}
