using ModsMenu.Core;

namespace ModsMenu.Core.Tests;

public class TabStripTests
{
    private static readonly float[] Three = { 100f, 110f, 105f };

    [Fact]
    public void 全部入るなら全部並べて矢印を出さない()
    {
        TabWindow w = TabStrip.Fit(Three, 315f, 0f, 50f, 2, 0);

        Assert.Equal(0, w.First);
        Assert.Equal(3, w.Count);
        Assert.False(w.Overflow);
    }

    [Fact]
    public void タブの間も幅に数える()
    {
        // 100 + 4 + 110 + 4 + 105 = 323
        Assert.False(TabStrip.Fit(Three, 323f, 4f, 50f, 0, 0).Overflow);
        Assert.True(TabStrip.Fit(Three, 322f, 4f, 50f, 0, 0).Overflow);
    }

    [Fact]
    public void 入らなければ矢印の分を空けて選んだタブが見える範囲にする()
    {
        float[] widths = { 100f, 100f, 100f, 100f, 100f };

        // 帯 300、矢印 50 → 250 に 2 つ
        TabWindow w = TabStrip.Fit(widths, 300f, 0f, 50f, 0, 0);
        Assert.True(w.Overflow);
        Assert.Equal(0, w.First);
        Assert.Equal(2, w.Count);

        TabWindow right = TabStrip.Fit(widths, 300f, 0f, 50f, 3, 0);
        Assert.True(right.Contains(3));
        Assert.Equal(2, right.First);
    }

    [Fact]
    public void 前の範囲で見えているなら動かさない()
    {
        float[] widths = { 100f, 100f, 100f, 100f, 100f };

        TabWindow w = TabStrip.Fit(widths, 300f, 0f, 50f, 2, 1);

        Assert.Equal(1, w.First);
        Assert.True(w.Contains(2));
    }

    [Fact]
    public void 左のタブを選んだら先頭をそこまで戻す()
    {
        float[] widths = { 100f, 100f, 100f, 100f, 100f };

        TabWindow w = TabStrip.Fit(widths, 300f, 0f, 50f, 0, 3);

        Assert.Equal(0, w.First);
    }

    [Fact]
    public void 右の端では左を詰めて空きを残さない()
    {
        float[] widths = { 100f, 100f, 100f, 100f, 100f };

        // 前の先頭が 4（最後の 1 つだけ）でも、2 つ入るので 3 から並べる
        TabWindow w = TabStrip.Fit(widths, 300f, 0f, 50f, 4, 4);

        Assert.Equal(3, w.First);
        Assert.Equal(2, w.Count);
    }

    [Fact]
    public void 一つも入らない幅でも選んだタブは出す()
    {
        TabWindow w = TabStrip.Fit(Three, 80f, 0f, 50f, 1, 0);

        Assert.Equal(1, w.First);
        Assert.Equal(1, w.Count);
    }

    [Fact]
    public void タブが無ければ空()
    {
        TabWindow w = TabStrip.Fit(new float[0], 300f, 0f, 50f, 0, 0);

        Assert.Equal(0, w.Count);
        Assert.False(w.Overflow);
    }

    [Fact]
    public void 矢印で送る_右の端が見えていたら右へは送らない()
    {
        float[] widths = { 100f, 100f, 100f, 100f, 100f };

        Assert.Equal(1, TabStrip.Scroll(widths, 300f, 0f, 50f, 0, 1));
        Assert.Equal(3, TabStrip.Scroll(widths, 300f, 0f, 50f, 3, 1));
        Assert.Equal(0, TabStrip.Scroll(widths, 300f, 0f, 50f, 0, -1));
        Assert.Equal(2, TabStrip.Scroll(widths, 300f, 0f, 50f, 3, -1));
    }
}
