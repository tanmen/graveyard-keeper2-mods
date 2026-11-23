using System;
using System.Collections.Generic;
using System.Globalization;
using LazyBearTechnology;
using StockFinder.Core;
using TMPro;

namespace StockFinder
{
    /// <summary>
    /// ツールチップの一番下に足す「在庫」の部品: 区切り・「合計 N」・宝箱ごとの表（ゲームの文字の部品 UITooltipTextWidgetData を、合計と表の行ごとに 1 つ）。
    /// 表は場所を見出しの行にして、その下に宝箱ごとの行（字下げした宝箱の名前と、右揃えの数）を並べる（0.2.2〜。形は StockTable）。
    /// 所持品は先頭の行、上限を超えた分は「ほか N か所」の 1 行。どこにも無ければ「合計 0」だけ。
    /// </summary>
    internal static class StockSection
    {
        private const int MaxErrorLogs = 5;

        private static List<string> lastTable;
        private static Func<string, float> lastMeasure;
        private static bool loggedFirst;
        private static bool loggedTooWide;
        private static bool checkedRender;
        private static int errorLogs;

        public static void Append(List<LazyWidgetDataBase> list, ItemDef item)
        {
            if (list == null || item == null || string.IsNullOrEmpty(item.id) || Plugin.Instance == null || !Plugin.Instance.Enabled.Value)
            {
                return;
            }

            try
            {
                UITooltip tooltip = TooltipStyles.Instance();
                if (tooltip == null)
                {
                    return;
                }

                TextStyle small = TooltipStyles.Style(tooltip, TooltipStyles.Small);
                TextStyle selected = TooltipStyles.Style(tooltip, TooltipStyles.Selected);
                TextStyle bold = TooltipStyles.Style(tooltip, TooltipStyles.Bold);

                StockSummary summary = StockSummary.Create(StorageScanner.Places(item.id), Plugin.Instance.MaxRows.Value);

                if (list.Count > 0 && !(list[list.Count - 1] is UITooltipSeparatorWidgetData))
                {
                    list.Add(new UITooltipSeparatorWidgetData());
                }

                string total = Apply(selected, Strings.Total + " ") + Apply(bold, Number(summary.Total));
                list.Add(new UITooltipTextWidgetData(total, TextAlignmentOptions.Center, small));

                List<StockTableRow> rows = Rows(summary, small, selected);
                if (rows.Count == 0)
                {
                    LogFirst(item, summary, "表なし");
                    return;
                }

                // 1 行ずつ別の文字の部品にする（1 つの部品に改行で詰めると、小さい書体の行送りが字の高さより狭くて行が重なる）
                List<string> lines = null;
                string how = "Lines";
                if (Plugin.Instance.Layout.Value == StockLayout.Table && TextMeasurer.TryGet(small, out Func<string, float> measure))
                {
                    float digit = measure(Apply(small, "0"));
                    if (digit <= 0f)
                    {
                        digit = 4f;
                    }

                    // 見出しの行の右端の詰め物は、数と同じ形の「0」を透明にしたもの（色の書式の内側なら alpha が効く）
                    string pad = Apply(selected, "<alpha=#00>0");
                    lines = StockTable.Compose(rows, measure, digit * 2f, digit * 2f, pad);
                    WarnIfTooWide(item, measure(lines[0]), TooltipStyles.ContentWidth(tooltip));
                    lastTable = lines;
                    lastMeasure = measure;
                    how = $"Table（数字の幅 {digit:0.#}）";
                }

                if (lines == null)
                {
                    lines = Lines(rows);
                    lastTable = null;
                }

                foreach (string line in lines)
                {
                    list.Add(new UITooltipTextWidgetData(line, TextAlignmentOptions.Center, small));
                }

                LogFirst(item, summary, how);
            }
            catch (Exception e)
            {
                LogError("ツールチップに在庫を足せなかった（" + item.id + "）", e);
            }
        }

        /// <summary>
        /// 2 行以上の表を初めて出したあと 1 度だけ、ツールチップの実物の文字の部品の幅を行ごとに読み、こちらの測り方と合っているか・
        /// 全部の行で同じ幅か（1 行ずつ中央に置かれるので、違えば行の頭がずれる）をログに出す。
        /// 窓の幅を超えて折り返した行は、部品の幅が窓の中身の幅に抑えられるので、ここで「測り方」とずれて見える。
        /// </summary>
        public static void CheckRendered(UITooltip tooltip)
        {
            if (checkedRender || lastTable == null || lastTable.Count < 2 || tooltip == null)
            {
                return;
            }

            checkedRender = true;
            try
            {
                var widths = new List<string>();
                float min = float.MaxValue;
                float max = float.MinValue;
                foreach (UITooltipTextWidget widget in tooltip.GetComponentsInChildren<UITooltipTextWidget>(false))
                {
                    TMP_Text real = TextMeasurer.WidgetLabel(widget);
                    if (real == null || !lastTable.Contains(real.text))
                    {
                        continue;
                    }

                    float width = real.rectTransform.rect.width;
                    min = Math.Min(min, width);
                    max = Math.Max(max, width);
                    widths.Add(width.ToString("0.#", CultureInfo.InvariantCulture));
                }

                if (widths.Count == 0)
                {
                    Plugin.Log.LogWarning("表を出したあと、ツールチップの中に表の文字の部品が見つからなかった（幅の確かめはしていない）");
                    return;
                }

                float mine = lastMeasure(lastTable[0]);
                string line = $"表の行の幅: こちらの測り方 {mine:0.#}・ツールチップの文字の部品 {string.Join(" / ", widths)}（{widths.Count} 行 / {lastTable.Count} 行）";
                if (max - min > 0.5f || Math.Abs(mine - max) > 0.5f || widths.Count != lastTable.Count)
                {
                    Plugin.Log.LogWarning(line + "。幅がそろっていないので、列がずれているかもしれない（設定 Layout = Lines で中央揃えに戻せる）");
                }
                else
                {
                    Plugin.Log.LogInfo(line);
                }
            }
            catch (Exception e)
            {
                LogError("表の幅を確かめられなかった", e);
            }
        }

        private static List<StockTableRow> Rows(StockSummary summary, TextStyle small, TextStyle selected)
        {
            var rows = new List<StockTableRow>();
            foreach (StockPlace place in summary.Rows)
            {
                string count = Apply(selected, Number(place.Count));
                rows.Add(place.IsPlayer
                    ? new StockTableRow(Apply(selected, place.Container), string.Empty, count)
                    : new StockTableRow(Apply(selected, place.Zone), Apply(small, place.Container), count));
            }

            if (summary.HiddenPlaces > 0)
            {
                rows.Add(new StockTableRow(Apply(small, Strings.MorePlaces(summary.HiddenPlaces)), string.Empty, Apply(selected, Number(summary.HiddenCount))));
            }

            return rows;
        }

        /// <summary>列にそろえないときの形: 1 行ずつ「場所 宝箱: 数」を中央揃え。</summary>
        private static List<string> Lines(List<StockTableRow> rows)
        {
            var lines = new List<string>(rows.Count);
            foreach (StockTableRow row in rows)
            {
                lines.Add(row.Place + (row.HasContainer ? " " + row.Container : string.Empty) + Strings.LinesSeparator + row.Count);
            }

            return lines;
        }

        private static string Apply(TextStyle style, string text) => style.ApplyStyleToString(text) ?? text;

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static void LogFirst(ItemDef item, StockSummary summary, string how)
        {
            if (loggedFirst)
            {
                return;
            }

            loggedFirst = true;
            Plugin.Log.LogInfo($"ツールチップに在庫を初めて足した: {item.id} 合計 {summary.Total}・行 {summary.Rows.Count}"
                + $"（ほか {summary.HiddenPlaces} か所）・並べ方 {how}");
        }

        /// <summary>表がツールチップの窓の中身の幅を超える（場所や宝箱の名前がとても長い）と、行が折り返して崩れる。1 度だけ知らせる。</summary>
        private static void WarnIfTooWide(ItemDef item, float width, float limit)
        {
            if (width <= limit || loggedTooWide)
            {
                return;
            }

            loggedTooWide = true;
            Plugin.Log.LogWarning($"表が窓に収まらない: {item.id} 表の幅 {width:0.#}・窓の中身 {limit:0.#}（行が折り返す）");
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
