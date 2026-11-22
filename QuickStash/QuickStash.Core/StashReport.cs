using System.Collections.Generic;

namespace QuickStash.Core
{
    /// <summary>
    /// 1 回しまったときの結果と、右下の知らせに出す文。
    /// ゲームの書体に「×」「・」が無い（訳文に 1 度も出てこない）ので使わない。
    /// </summary>
    public sealed class StashReport
    {
        private readonly Dictionary<string, int> moved = new Dictionary<string, int>();
        private readonly HashSet<string> locked = new HashSet<string>();
        private readonly HashSet<string> pinned = new HashSet<string>();
        private readonly HashSet<string> full = new HashSet<string>();

        /// <summary>しまう先にできる宝箱の数（その場所の、プレイヤーが品を入れられる宝箱）。</summary>
        public int Chests { get; set; }

        /// <summary>しまった品の種類の数。</summary>
        public int Kinds => moved.Count;

        /// <summary>しまった数の合計。</summary>
        public int Total { get; private set; }

        /// <summary>宝箱に同じ品があるのにロックしていて残した品の種類の数。</summary>
        public int LockedKept => locked.Count;

        /// <summary>宝箱に同じ品があるのにホットバーに置いていて残した品の種類の数。</summary>
        public int PinnedKept => pinned.Count;

        /// <summary>宝箱に同じ品があるのに、どこにも入りきらずに残った品の種類の数。</summary>
        public int FullKinds => full.Count;

        public IReadOnlyDictionary<string, int> Moved => moved;

        public void AddMoved(string id, int count)
        {
            if (string.IsNullOrEmpty(id) || count <= 0)
            {
                return;
            }

            moved.TryGetValue(id, out int before);
            moved[id] = before + count;
            Total += count;
        }

        public void AddKept(string id, StashDecision why)
        {
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            if (why == StashDecision.Locked)
            {
                locked.Add(id);
            }
            else if (why == StashDecision.Pinned)
            {
                pinned.Add(id);
            }
        }

        public void AddFull(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                full.Add(id);
            }
        }

        /// <summary>右下の知らせに出す 1 行。zone は場所の名前（空なら付けない）。</summary>
        public string Message(bool japanese, string zone)
        {
            bool hasZone = !string.IsNullOrEmpty(zone);
            if (Chests == 0)
            {
                return japanese
                    ? (hasZone ? zone + "には、しまえる宝箱が無い" : "この場所には、しまえる宝箱が無い")
                    : (hasZone ? "No chests to stash into in " + zone : "No chests to stash into here");
            }

            if (Total > 0)
            {
                string done = japanese
                    ? (hasZone ? zone + "の宝箱へしまった: " : "宝箱へしまった: ") + Kinds + " 種 " + Total + " 個"
                    : "Stashed " + Total + " items (" + Kinds + " kinds)" + (hasZone ? " in " + zone : string.Empty);
                if (FullKinds > 0)
                {
                    done += japanese ? "。入りきらない品 " + FullKinds + " 種" : ". " + FullKinds + " kinds did not fit";
                }

                return done;
            }

            if (FullKinds > 0)
            {
                return japanese ? "宝箱がいっぱいで、しまえなかった" : "The chests are full";
            }

            string none = japanese ? "しまえる品が無かった" : "Nothing to stash";
            int kept = LockedKept + PinnedKept;
            if (kept > 0)
            {
                none += japanese ? "。ロックとホットバーの品 " + kept + " 種は残した" : ". Kept " + kept + " locked or hotbar kinds";
            }

            return none;
        }
    }
}
