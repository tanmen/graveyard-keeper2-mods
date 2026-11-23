using System;
using System.Collections.Generic;
using System.Linq;
using LazyBearTechnology;
using TMPro;
using TradeFinder.Core;
using UnityEngine.UI;

namespace TradeFinder
{
    /// <summary>
    /// ツールチップに足す「売り買いの場所」の部品: 区切りと、1 行ずつの文字の部品（ゲームの UITooltipTextWidgetData。小さい文の形で中央揃え）。
    /// 行は「買える: 名前（場所） 値段」→ 2 行目から「名前（場所） 値段」、「売れる: …」も同じ。まだ取引できない所は薄い字。
    /// </summary>
    internal static class TradeSection
    {
        private const int MaxErrorLogs = 5;

        /// <summary>まだ取引できない所の字の濃さ（TMP の alpha。ゲームの小さい文 946E44 を羊皮紙の地に 6 割で重ねる。ゲームにこの形は無い）。</summary>
        private const string DimAlpha = "<alpha=#99>";

        private static bool loggedFirst;
        private static int errorLogs;

        /// <summary>
        /// 足す部品の並び（区切りを含む）。出すものが無ければ空。<paramref name="needSeparator"/> は、足す位置の直前が区切りでないとき。
        /// </summary>
        public static List<LazyWidgetDataBase> Build(ItemDef item, bool keyHeld, bool needSeparator)
        {
            var block = new List<LazyWidgetDataBase>();
            Plugin plugin = Plugin.Instance;
            if (item == null || string.IsNullOrEmpty(item.id) || plugin == null)
            {
                return block;
            }

            try
            {
                TradeOptions options = plugin.Options(keyHeld);
                if (!TradeOptions.Shows(options.Buy, keyHeld) && !TradeOptions.Shows(options.Sell, keyHeld))
                {
                    return block;
                }

                UITooltip tooltip = TooltipStyles.Instance();
                if (tooltip == null)
                {
                    return block;
                }

                TextStyle small = TooltipStyles.Style(tooltip, TooltipStyles.Small);
                TextStyle selected = TooltipStyles.Style(tooltip, TooltipStyles.Selected);

                List<TradeOffer> offers = OfferFinder.Find(item.id, VendorScanner.Current());
                if (TradeOptions.Shows(options.Price, keyHeld))
                {
                    foreach (TradeOffer offer in offers.Where(o => o.IsNow))
                    {
                        offer.Price = VendorScanner.PriceOf(offer.VendorId, item, offer.Side);
                    }
                }

                List<TradeGroup> groups = TradeLayout.Arrange(offers, options);
                List<string> lines = TradeText.Lines(groups, options, Words(small, selected));
                if (lines.Count == 0)
                {
                    LogFirst(item, offers, 0);
                    return block;
                }

                if (needSeparator)
                {
                    block.Add(new UITooltipSeparatorWidgetData());
                }

                foreach (string line in lines)
                {
                    block.Add(new UITooltipTextWidgetData(line, TextAlignmentOptions.Center, small));
                }

                LogFirst(item, offers, lines.Count);
            }
            catch (Exception e)
            {
                block.Clear();
                LogError("ツールチップに売り買いの場所を足せなかった（" + item.id + "）", e);
            }

            return block;
        }

        private static TradeWords Words(TextStyle small, TextStyle selected) => new TradeWords
        {
            Label = Strings.Label,
            LabelSeparator = Strings.LabelSeparator,
            Paren = Strings.Paren,
            NotBuilt = Strings.NotBuilt,
            FromTier = Strings.FromTier,
            StateJoin = Strings.StateJoin,
            Money = price => Trading.FormatMoney(price, printZero: false, " "),
            HappinessIcon = "happiness".FontIcon(),
            Heading = s => Apply(selected, s),
            Plain = s => Apply(small, s),
            Dim = s => Apply(small, DimAlpha + s),
        };

        private static string Apply(TextStyle style, string text) => style.ApplyStyleToString(text) ?? text;

        private static void LogFirst(ItemDef item, List<TradeOffer> offers, int lines)
        {
            if (loggedFirst || lines == 0)
            {
                return; // 商人の扱わない品は多いので、行を足した最初の品だけ出す
            }

            loggedFirst = true;
            Plugin.Log.LogInfo($"ツールチップに売り買いの場所を初めて足した: {item.id} 行 {lines}（"
                + string.Join(" / ", offers.Select(o => $"{o.Side} {o.VendorId} {o.State} 段階 {o.Tier} 値段 {o.Price}{(o.Happiness ? " 幸福" : string.Empty)}"))
                + $"）・{Plugin.Instance.Describe()}");
        }

        private static void LogError(string what, Exception e)
        {
            if (errorLogs >= MaxErrorLogs)
            {
                return;
            }

            errorLogs++;
            Plugin.Log.LogError($"{what}: {e}" + (errorLogs == MaxErrorLogs ? "（以後は出さない）" : string.Empty));
        }
    }
}
