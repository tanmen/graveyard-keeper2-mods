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
