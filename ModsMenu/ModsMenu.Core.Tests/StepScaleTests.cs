using ModsMenu.Core;

namespace ModsMenu.Core.Tests;

public class StepScaleTests
{
    [Fact]
    public void 整数の範囲は1刻み()
    {
        var scale = new StepScale(1, 30, 1);

        Assert.Equal(29, scale.Steps);
        Assert.Equal(7, scale.IndexOf(8));
        Assert.Equal(8, scale.ValueAt(7));
        Assert.Equal("8", scale.Format(8));
    }

    [Fact]
    public void 範囲の外の値はつまみを端に寄せる()
    {
        var scale = new StepScale(0, 60, 1);

        Assert.Equal(0, scale.IndexOf(-4));
        Assert.Equal(60, scale.IndexOf(99));
        Assert.Equal(0, scale.IndexOf(double.NaN));
    }

    [Fact]
    public void 小数の刻みは刻みの桁で丸めて出す()
    {
        var scale = new StepScale(0, 1, 0.1);

        Assert.Equal(10, scale.Steps);
        Assert.Equal(1, scale.Decimals);
        Assert.Equal(0.3, scale.ValueAt(3));
        Assert.Equal("0.3", scale.Format(scale.ValueAt(3)));
        Assert.Equal(3, scale.IndexOf(0.29));
    }

    [Fact]
    public void 刻みの桁()
    {
        Assert.Equal(0, new StepScale(0, 10, 1).Decimals);
        Assert.Equal(1, new StepScale(0, 10, 0.5).Decimals);
        Assert.Equal(2, new StepScale(0, 1, 0.05).Decimals);
        Assert.Equal(0, new StepScale(0, 100, 5).Decimals);
    }

    [Fact]
    public void 逆の範囲と0以下の刻みは直す()
    {
        var scale = new StepScale(10, 0, 0);

        Assert.Equal(0, scale.Min);
        Assert.Equal(10, scale.Max);
        Assert.Equal(1, scale.Step);
    }

    [Fact]
    public void 小数点はロケールに依らない()
    {
        var saved = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            Assert.Equal("0.5", new StepScale(0, 1, 0.5).Format(0.5));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = saved;
        }
    }
}
