using System.Collections.Generic;

namespace TradeFinder.Core
{
    /// <summary>プレイヤーから見た向き。</summary>
    public enum TradeSide
    {
        /// <summary>買える（商人が売る）。</summary>
        Buy,

        /// <summary>売れる（商人が買い取る）。</summary>
        Sell,
    }

    /// <summary>いま取引できるか。</summary>
    public enum TradeState
    {
        /// <summary>いまの段階で扱っている。</summary>
        Now,

        /// <summary>町の店をまだ建てていない（建てると扱う。<see cref="TradeOffer.Tier"/> がいまの段階より上なら、その段階から）。</summary>
        NotBuilt,

        /// <summary>商人の段階が上がると扱う（<see cref="TradeOffer.Tier"/> の段階から）。</summary>
        LaterTier,
    }

    /// <summary>設定の「いつ出すか」。</summary>
    public enum ShowWhen
    {
        /// <summary>いつも出す。</summary>
        Always,

        /// <summary>キー（既定は Shift）を押している間だけ出す。</summary>
        WhileKey,

        /// <summary>出さない。</summary>
        Never,
    }

    /// <summary>商人の 1 つの段階（ゲームの VendorTierData の、この mod が見る所だけ）。</summary>
    public sealed class VendorTier
    {
        public VendorTier(IEnumerable<string> products, IEnumerable<string> notBuying, IEnumerable<string> notSelling, IEnumerable<string> happinessItems)
        {
            Products = new HashSet<string>(products ?? new string[0]);
            NotBuying = new HashSet<string>(notBuying ?? new string[0]);
            NotSelling = new HashSet<string>(notSelling ?? new string[0]);
            HappinessItems = new HashSet<string>(happinessItems ?? new string[0]);
        }

        /// <summary>扱う品（vendorProducts の itemId）。売るのも買い取るのもこの中だけ。</summary>
        public HashSet<string> Products { get; }

        /// <summary>扱うが買い取らない品。</summary>
        public HashSet<string> NotBuying { get; }

        /// <summary>扱うが売らない品。</summary>
        public HashSet<string> NotSelling { get; }

        /// <summary>売ると幸福が入る品（townVendorProductInfos の itemId）。</summary>
        public HashSet<string> HappinessItems { get; }

        public bool Handles(string itemId, TradeSide side)
        {
            if (!Products.Contains(itemId))
            {
                return false;
            }

            return side == TradeSide.Buy ? !NotSelling.Contains(itemId) : !NotBuying.Contains(itemId);
        }
    }

    /// <summary>商人 1 人（町の店も 1 人）のいまの様子。</summary>
    public sealed class VendorFacts
    {
        public VendorFacts(string id, string name, string place, bool built, int currentTier, IReadOnlyList<VendorTier> tiers)
        {
            Id = id;
            Name = name;
            Place = place;
            Built = built;
            CurrentTier = currentTier;
            Tiers = tiers ?? new VendorTier[0];
        }

        public string Id { get; }

        public string Name { get; }

        /// <summary>居る場所の名前（右上の地名の枠と同じ）。分からなければ null。</summary>
        public string Place { get; }

        /// <summary>取引できる状態か（町の店は建てたか。町の店でない商人はいつも true）。</summary>
        public bool Built { get; }

        /// <summary>いまの段階（1 から）。</summary>
        public int CurrentTier { get; }

        /// <summary>段階ごとの品（添字 0 が段階 1）。</summary>
        public IReadOnlyList<VendorTier> Tiers { get; }
    }

    /// <summary>ツールチップの 1 行（商人 1 人の、片方の向き）。</summary>
    public sealed class TradeOffer
    {
        public TradeOffer(string vendorId, string name, string place, TradeSide side, TradeState state, int tier, bool happiness)
        {
            VendorId = vendorId;
            Name = name;
            Place = place;
            Side = side;
            State = state;
            Tier = tier;
            Happiness = happiness;
        }

        public string VendorId { get; }

        public string Name { get; }

        public string Place { get; }

        public TradeSide Side { get; }

        public TradeState State { get; }

        /// <summary>扱う段階（いまなら、いまの段階）。</summary>
        public int Tier { get; }

        /// <summary>売ると幸福が入る（<see cref="TradeSide.Sell"/> だけ）。</summary>
        public bool Happiness { get; }

        /// <summary>1 個の値段（いま取引できるときだけ。0 は不明）。並べたあとに mod 本体が入れる。</summary>
        public int Price { get; set; }

        public bool IsNow => State == TradeState.Now;
    }
}
