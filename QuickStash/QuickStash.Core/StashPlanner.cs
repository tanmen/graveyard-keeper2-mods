using System.Collections.Generic;
using System.Linq;

namespace QuickStash.Core
{
    /// <summary>しまう品の選び方と、しまう先の宝箱の順。</summary>
    public static class StashPlanner
    {
        /// <summary>
        /// 所持品の品をしまう対象にするか。重ねられる品（ゲームの宝箱の窓の「同じ品を全部移す」ボタンと同じ範囲）で、
        /// ロックしておらず、ホットバーに置いていない品だけ。
        /// </summary>
        public static StashDecision Decide(string id, bool stackable, LockSet locks, ICollection<string> pinned)
        {
            if (string.IsNullOrEmpty(id) || !stackable)
            {
                return StashDecision.NotStackable;
            }

            if (locks != null && locks.Contains(id))
            {
                return StashDecision.Locked;
            }

            if (pinned != null && pinned.Contains(id))
            {
                return StashDecision.Pinned;
            }

            return StashDecision.Stash;
        }

        /// <summary>
        /// その品が入っている宝箱を、多く入っている順に並べる（同じ数なら元の順）。入っていない宝箱（数が 0 以下）は外す。
        /// 返すのは countsInChests の添字。同じ品が 2 つの宝箱にあるとき、まとめて置いてある方を先に埋めるため。
        /// </summary>
        public static List<int> OrderTargets(IReadOnlyList<int> countsInChests)
        {
            if (countsInChests == null)
            {
                return new List<int>();
            }

            return Enumerable.Range(0, countsInChests.Count)
                .Where(i => countsInChests[i] > 0)
                .OrderByDescending(i => countsInChests[i])
                .ThenBy(i => i)
                .ToList();
        }
    }

    public enum StashDecision
    {
        Stash,
        NotStackable,
        Locked,
        Pinned,
    }
}
