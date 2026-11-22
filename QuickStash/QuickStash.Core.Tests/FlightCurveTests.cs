using QuickStash.Core;

namespace QuickStash.Core.Tests;

public class FlightCurveTests
{
    [Fact]
    public void Progress_StartsAtBodyEndsAtChest_AndNeverGoesBack()
    {
        Assert.Equal(0f, FlightCurve.Progress(0f));
        Assert.Equal(1f, FlightCurve.Progress(1f));
        Assert.Equal(0.5f, FlightCurve.Progress(0.5f), 3);
        Assert.Equal(0f, FlightCurve.Progress(-1f));
        Assert.Equal(1f, FlightCurve.Progress(2f));

        float before = 0f;
        for (int i = 1; i <= 100; i++)
        {
            float now = FlightCurve.Progress(i / 100f);
            Assert.True(now >= before);
            before = now;
        }
    }

    [Fact]
    public void Lift_IsZeroAtBothEnds_HighestInTheMiddle()
    {
        Assert.Equal(0f, FlightCurve.Lift(0f, 40f));
        Assert.Equal(0f, FlightCurve.Lift(1f, 40f));
        Assert.Equal(40f, FlightCurve.Lift(0.5f, 40f), 3);
        Assert.True(FlightCurve.Lift(0.25f, 40f) < FlightCurve.Lift(0.5f, 40f));
    }

    [Fact]
    public void ArcHeight_GrowsWithDistance_WithinLimits()
    {
        Assert.Equal(16f, FlightCurve.ArcHeight(0f));
        Assert.Equal(35f, FlightCurve.ArcHeight(100f), 3);
        Assert.Equal(72f, FlightCurve.ArcHeight(1000f));
    }

    [Fact]
    public void ScaleAndAlpha_PopInThenShrinkAndFadeIntoTheChest()
    {
        Assert.Equal(0.5f, FlightCurve.Scale(0f));
        Assert.Equal(1f, FlightCurve.Scale(0.5f));
        Assert.Equal(0.5f, FlightCurve.Scale(1f));
        Assert.Equal(1f, FlightCurve.Alpha(0f));
        Assert.Equal(1f, FlightCurve.Alpha(0.8f));
        Assert.Equal(0f, FlightCurve.Alpha(1f));
    }

    [Fact]
    public void Delay_StaggersInOrder()
    {
        Assert.Equal(0f, FlightCurve.Delay(0));
        Assert.Equal(FlightCurve.Stagger * 3, FlightCurve.Delay(3), 5);
        Assert.Equal(0f, FlightCurve.Delay(-2));
        Assert.True(FlightCurve.Delay(FlightCurve.MaxFlights - 1) + FlightCurve.Duration < 2f);
    }
}
