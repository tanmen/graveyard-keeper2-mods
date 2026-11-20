using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CraftTodo.Core
{
    /// <summary>
    /// 製作メモの保存形式（タブ区切りの行）。
    ///
    /// <code>
    /// # CraftTodo v2
    /// entry	build	&lt;recipeId&gt;	&lt;stationId&gt;	&lt;stationDefId&gt;	&lt;iconId&gt;	&lt;回数&gt;
    /// mat	&lt;itemId&gt;	&lt;1 回分の数&gt;	&lt;0|1（グループか）&gt;	&lt;0|1（回数で増えるか）&gt;
    /// </code>
    ///
    /// mat の行は直前の entry に属する。読めない行は飛ばす（ゲームの更新で id の形が変わっても、残りのメモは読める）。
    ///
    /// v1（0.2.0 まで）は mat の数が回数を掛けたあとの合計で、回数で増えるかの列が無い。
    /// v1 を読むときは回数を 1 とし、数はそのまま 1 回分として読む（要る数の合計は変わらない）。
    /// 頭の行が無いものは v1 として読む。
    /// </summary>
    public static class TodoFormat
    {
        public const int CurrentVersion = 2;

        public const string HeaderPrefix = "# CraftTodo v";

        public static readonly string Header = HeaderPrefix + CurrentVersion.ToString(CultureInfo.InvariantCulture);

        public static string Write(IEnumerable<TodoEntry> entries)
        {
            var sb = new StringBuilder();
            sb.Append(Header).Append('\n');
            if (entries == null)
            {
                return sb.ToString();
            }

            foreach (TodoEntry entry in entries)
            {
                sb.Append("entry\t")
                    .Append(entry.Kind == TodoKind.Build ? "build" : "craft").Append('\t')
                    .Append(Escape(entry.RecipeId)).Append('\t')
                    .Append(Escape(entry.StationId)).Append('\t')
                    .Append(Escape(entry.StationDefId)).Append('\t')
                    .Append(Escape(entry.IconId)).Append('\t')
                    .Append(entry.Multiplier.ToString(CultureInfo.InvariantCulture)).Append('\n');
                foreach (TodoMaterial material in entry.Materials)
                {
                    sb.Append("mat\t")
                        .Append(Escape(material.ItemId)).Append('\t')
                        .Append(material.Count.ToString(CultureInfo.InvariantCulture)).Append('\t')
                        .Append(material.IsGroup ? '1' : '0').Append('\t')
                        .Append(material.Scales ? '1' : '0').Append('\n');
                }
            }

            return sb.ToString();
        }

        public static List<TodoEntry> Read(string text)
        {
            var result = new List<TodoEntry>();
            if (string.IsNullOrEmpty(text))
            {
                return result;
            }

            int version = 1;
            bool seenContent = false;
            PendingEntry pending = null;
            foreach (string rawLine in text.Split('\n'))
            {
                string line = rawLine.TrimEnd('\r');
                if (line.Length == 0)
                {
                    continue;
                }

                if (line[0] == '#')
                {
                    if (!seenContent && TryParseVersion(line, out int parsed))
                    {
                        version = parsed;
                    }

                    continue;
                }

                seenContent = true;
                string[] cells = line.Split('\t');
                if (cells[0] == "entry")
                {
                    Flush(pending, result);
                    pending = ParseEntry(cells, version);
                }
                else if (cells[0] == "mat" && pending != null)
                {
                    TodoMaterial material = ParseMaterial(cells, version);
                    if (material != null)
                    {
                        pending.Materials.Add(material);
                    }
                }
            }

            Flush(pending, result);
            return result;
        }

        private static bool TryParseVersion(string line, out int version)
        {
            version = 0;
            return line.StartsWith(HeaderPrefix, StringComparison.Ordinal)
                && int.TryParse(line.Substring(HeaderPrefix.Length).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out version)
                && version >= 1;
        }

        private static PendingEntry ParseEntry(string[] cells, int version)
        {
            if (cells.Length < 7)
            {
                return null;
            }

            TodoKind kind;
            if (cells[1] == "build")
            {
                kind = TodoKind.Build;
            }
            else if (cells[1] == "craft")
            {
                kind = TodoKind.Craft;
            }
            else
            {
                return null;
            }

            string recipeId = Unescape(cells[2]);
            if (recipeId.Length == 0 || !int.TryParse(cells[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out int multiplier))
            {
                return null;
            }

            return new PendingEntry
            {
                Kind = kind,
                RecipeId = recipeId,
                StationId = Unescape(cells[3]),
                StationDefId = Unescape(cells[4]),
                IconId = Unescape(cells[5]),
                // v1 の素材の数は回数を掛けたあとの合計なので、回数 1 として読む
                Multiplier = version < 2 ? 1 : multiplier,
            };
        }

        private static TodoMaterial ParseMaterial(string[] cells, int version)
        {
            if (cells.Length < 4 || !int.TryParse(cells[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int count))
            {
                return null;
            }

            string itemId = Unescape(cells[1]);
            if (itemId.Length == 0 || !IsFlag(cells[3]))
            {
                return null;
            }

            bool scales = true;
            if (version >= 2 && cells.Length >= 5)
            {
                if (!IsFlag(cells[4]))
                {
                    return null;
                }

                scales = cells[4] == "1";
            }

            return new TodoMaterial(itemId, count, cells[3] == "1", scales);
        }

        private static bool IsFlag(string cell) => cell == "0" || cell == "1";

        private static void Flush(PendingEntry pending, List<TodoEntry> result)
        {
            if (pending == null)
            {
                return;
            }

            result.Add(new TodoEntry(pending.Kind, pending.RecipeId, pending.StationId, pending.StationDefId, pending.IconId, pending.Multiplier, pending.Materials));
        }

        internal static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        internal static string Unescape(string value)
        {
            if (string.IsNullOrEmpty(value) || value.IndexOf('\\') < 0)
            {
                return value ?? string.Empty;
            }

            var sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c != '\\' || i == value.Length - 1)
                {
                    sb.Append(c);
                    continue;
                }

                char next = value[++i];
                switch (next)
                {
                    case 't':
                        sb.Append('\t');
                        break;
                    case 'n':
                        sb.Append('\n');
                        break;
                    case 'r':
                        sb.Append('\r');
                        break;
                    default:
                        sb.Append(next);
                        break;
                }
            }

            return sb.ToString();
        }

        private sealed class PendingEntry
        {
            public TodoKind Kind;
            public string RecipeId;
            public string StationId;
            public string StationDefId;
            public string IconId;
            public int Multiplier;
            public readonly List<TodoMaterial> Materials = new List<TodoMaterial>();
        }
    }
}
