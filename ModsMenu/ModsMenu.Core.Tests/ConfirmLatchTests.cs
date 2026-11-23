using ModsMenu.Core;

namespace ModsMenu.Core.Tests;

public class ConfirmLatchTests
{
    [Fact]
    public void 一度目は構えるだけで二度目に実行する()
    {
        var latch = new ConfirmLatch(3f);

        Assert.False(latch.Press(10f));
        Assert.True(latch.Armed);
        Assert.True(latch.Press(11f));
        Assert.False(latch.Armed);
    }

    [Fact]
    public void 時間切れのあとの押下は構え直し()
    {
        var latch = new ConfirmLatch(3f);

        latch.Press(10f);
        Assert.False(latch.Press(13.5f));
        Assert.True(latch.Armed);
    }

    [Fact]
    public void Tickは時間切れのときだけ解いてtrue()
    {
        var latch = new ConfirmLatch(3f);

        Assert.False(latch.Tick(0f));
        latch.Press(10f);
        Assert.False(latch.Tick(12f));
        Assert.True(latch.Tick(13f));
        Assert.False(latch.Armed);
        Assert.False(latch.Tick(14f));
    }

    [Fact]
    public void Resetで構えを解く()
    {
        var latch = new ConfirmLatch(3f);

        latch.Press(1f);
        latch.Reset();

        Assert.False(latch.Armed);
        Assert.False(latch.Press(2f));
    }
}
