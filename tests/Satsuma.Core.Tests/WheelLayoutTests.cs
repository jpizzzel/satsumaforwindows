namespace Satsuma.Core.Tests;

public sealed class WheelLayoutTests
{
    const double Inner = WheelLayout.HubRadius + WheelLayout.RingWidth / 2;
    const double Outer = WheelLayout.HubRadius + WheelLayout.RingWidth * 1.5;

    static int? Hit(WheelLayout layout, double radius, double angle)
    {
        var (x, y) = WheelSlot.PointAt(radius, angle);
        return layout.HitTest(x, y)?.Index;
    }

    [Fact]
    public void Empty_wheel_has_nothing_to_hit()
    {
        var layout = WheelLayout.For(0);
        Assert.Empty(layout.Slots);
        Assert.Null(Hit(layout, Inner, 0));
    }

    [Fact]
    public void Hub_cancels()
    {
        Assert.Null(WheelLayout.For(4).HitTest(0, 0));
        Assert.Null(Hit(WheelLayout.For(4), WheelLayout.HubRadius - 1, 90));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(44, 0)]
    [InlineData(46, 1)]
    [InlineData(90, 1)]
    [InlineData(180, 2)]
    [InlineData(270, 3)]
    [InlineData(316, 0)]
    [InlineData(-30, 0)]
    public void Four_segments_start_with_one_centered_at_the_top(double angle, int expected)
    {
        Assert.Equal(expected, Hit(WheelLayout.For(4), Inner, angle));
    }

    [Fact]
    public void A_single_entry_fills_the_ring()
    {
        var layout = WheelLayout.For(1);
        Assert.All([0.0, 90, 180, 270], a => Assert.Equal(0, Hit(layout, Inner, a)));
    }

    [Fact]
    public void Beyond_the_edge_still_counts_as_the_outer_ring()
    {
        Assert.Equal(1, Hit(WheelLayout.For(4), 500, 90));
    }

    [Fact]
    public void Ten_entries_fit_one_ring()
    {
        var layout = WheelLayout.For(10);
        Assert.Equal(1, layout.Rings);
        Assert.All(layout.Slots, s => Assert.Equal(36, s.Sweep, 6));
    }

    [Fact]
    public void Twelve_entries_split_across_two_rings_with_fewer_inside()
    {
        var layout = WheelLayout.For(12);

        Assert.Equal(2, layout.Rings);
        var inner = layout.Slots.Count(s => s.Ring == 0);
        Assert.Equal(5, inner);
        Assert.Equal(Enumerable.Range(0, 12), layout.Slots.Select(s => s.Index));
        Assert.Equal(0, Hit(layout, Inner, 0));
        Assert.Equal(inner, Hit(layout, Outer, 0));
        Assert.Equal(inner, Hit(layout, Outer, 350)); // still the segment centered at the top
        Assert.Equal(11, Hit(layout, Outer, 310));
    }

    [Fact]
    public void Every_slot_hits_itself_at_its_center()
    {
        foreach (var count in Enumerable.Range(1, 20))
        {
            var layout = WheelLayout.For(count);
            foreach (var slot in layout.Slots)
                Assert.Equal(slot.Index, Hit(layout, slot.MidRadius, slot.MidAngle));
        }
    }

    [Fact]
    public void Window_fits_the_largest_wheel()
    {
        Assert.True(WheelLayout.WindowSize / 2 > WheelLayout.For(20).OuterRadius);
    }
}
