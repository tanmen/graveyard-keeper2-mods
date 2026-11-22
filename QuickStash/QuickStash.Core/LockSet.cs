using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace QuickStash.Core
{
    /// <summary>
    /// ロックした品の種類（アイテムの id）。ロックした品は、しまうキーを押しても所持品に残る。
    /// 品質（星）の違う品は id が違うので、別々にロックする。
    /// ファイルには 1 行に 1 つの id を書く（# で始まる行と空行は読み飛ばす）。
    /// </summary>
    public sealed class LockSet
    {
        private readonly HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);

        public int Count => ids.Count;

        /// <summary>id の昇順（ファイルに書く順）。</summary>
        public IReadOnlyList<string> Ids => ids.OrderBy(id => id, StringComparer.Ordinal).ToList();

        public bool Contains(string id) => !string.IsNullOrEmpty(id) && ids.Contains(id);

        /// <summary>ロックしていなければロックし、していれば外す。ロックした状態になったら true。</summary>
        public bool Toggle(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("id が空", nameof(id));
            }

            if (ids.Remove(id))
            {
                return false;
            }

            ids.Add(id);
            return true;
        }

        public void ReplaceWith(LockSet other)
        {
            ids.Clear();
            if (other != null)
            {
                ids.UnionWith(other.ids);
            }
        }

        public void Clear() => ids.Clear();

        public string Serialize(string header = null)
        {
            var text = new StringBuilder();
            if (!string.IsNullOrEmpty(header))
            {
                foreach (string line in header.Replace("\r\n", "\n").Split('\n'))
                {
                    text.Append("# ").Append(line).Append('\n');
                }
            }

            foreach (string id in Ids)
            {
                text.Append(id).Append('\n');
            }

            return text.ToString();
        }

        public static LockSet Parse(string text)
        {
            var set = new LockSet();
            if (string.IsNullOrEmpty(text))
            {
                return set;
            }

            foreach (string raw in text.Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                set.ids.Add(line);
            }

            return set;
        }
    }
}
