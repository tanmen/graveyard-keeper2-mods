namespace StockFinder.Core
{
    /// <summary>品が置いてある 1 か所（所持品か、1 つの宝箱）と、そこにある数。</summary>
    public sealed class StockPlace
    {
        public StockPlace(string zone, string container, int count, bool isPlayer = false)
        {
            Zone = zone ?? string.Empty;
            Container = container ?? string.Empty;
            Count = count;
            IsPlayer = isPlayer;
        }

        /// <summary>宝箱のある場所の名前（芝地・墓場など）。所持品なら空。</summary>
        public string Zone { get; }

        /// <summary>宝箱の名前。所持品なら「所持品」。</summary>
        public string Container { get; }

        public int Count { get; }

        public bool IsPlayer { get; }
    }
}
