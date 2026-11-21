namespace CraftTodo.Core
{
    /// <summary>
    /// 在庫の数え方。ゲームの側（持ち物とゾーンの収納）が実装し、<see cref="TodoProgress.EvaluateAll"/> が使う。
    /// </summary>
    public interface ITodoStock
    {
        /// <summary>作る場所に戻ったときに使える数（持ち物＋その件の作る場所のゾーンの収納）。グループは中の品の合計。</summary>
        int Available(TodoEntry entry, TodoMaterial material);

        /// <summary>その件の作る場所のゾーンの収納だけの数（持ち物を除く）。ゾーンが分からなければ 0。</summary>
        int ZoneOnly(TodoEntry entry, TodoMaterial material);

        /// <summary>同じ収納を数える件どうしの鍵（作る場所のゾーン）。分からなければ null（持ち物だけを数える件）。</summary>
        string ZoneKey(TodoEntry entry);

        /// <summary>グループの素材 <paramref name="groupId"/> に、アイテム <paramref name="itemId"/> が入るか。</summary>
        bool GroupContains(string groupId, string itemId);
    }
}
