namespace Satsuma.Core;

/// <summary>One segment of the wheel. Angles are degrees clockwise from twelve o'clock.</summary>
public readonly record struct WheelSlot(int Index, int Ring, double StartAngle, double Sweep, double InnerRadius, double OuterRadius)
{
    public double MidAngle => StartAngle + Sweep / 2;
    public double MidRadius => (InnerRadius + OuterRadius) / 2;

    /// <summary>The x/y offset from the middle of the wheel for a radius and angle.</summary>
    public static (double X, double Y) PointAt(double radius, double angle)
    {
        var radians = angle * Math.PI / 180;
        return (radius * Math.Sin(radians), -radius * Math.Cos(radians));
    }
}

/// <summary>
/// Lays out the segments around the hub. Up to <see cref="SingleRingLimit"/> fit on one ring. Past that we add a
/// second ring, and since it's bigger it gets more of the items.
/// </summary>
public sealed class WheelLayout
{
    public const double HubRadius = 46;
    public const double RingWidth = 74;
    public const int SingleRingLimit = 10;
    public const int MaxRings = 2;

    /// <summary>Width and height of a window that fits the largest wheel, with room for a shadow.</summary>
    public const double WindowSize = 2 * (HubRadius + MaxRings * RingWidth) + 24;

    public IReadOnlyList<WheelSlot> Slots { get; }
    public int Rings { get; }
    public double OuterRadius => HubRadius + Rings * RingWidth;

    WheelLayout(IReadOnlyList<WheelSlot> slots, int rings)
    {
        Slots = slots;
        Rings = rings;
    }

    public static WheelLayout For(int count)
    {
        if (count <= 0)
            return new WheelLayout([], 0);
        if (count <= SingleRingLimit)
            return new WheelLayout([.. Ring(0, 0, count)], 1);

        var innerMid = HubRadius + RingWidth / 2;
        var outerMid = HubRadius + RingWidth * 1.5;
        var inner = (int)Math.Ceiling(count * innerMid / (innerMid + outerMid));
        return new WheelLayout([.. Ring(0, 0, inner), .. Ring(1, inner, count - inner)], 2);
    }

    static IEnumerable<WheelSlot> Ring(int ring, int firstIndex, int count)
    {
        var sweep = 360.0 / count;
        var inner = HubRadius + ring * RingWidth;
        // First segment sits centered at 12 o'clock.
        for (var i = 0; i < count; i++)
            yield return new WheelSlot(firstIndex + i, ring, i * sweep - sweep / 2, sweep, inner, inner + RingWidth);
    }

    /// <summary>
    /// Which segment is under this point (measured from the middle)? Null over the hub, which means cancel.
    /// The outer ring keeps going past its edge, so a sloppy flick still lands where you meant.
    /// </summary>
    public WheelSlot? HitTest(double x, double y)
    {
        var distance = Math.Sqrt(x * x + y * y);
        if (Rings == 0 || distance < HubRadius)
            return null;

        var ring = Math.Min((int)((distance - HubRadius) / RingWidth), Rings - 1);
        var angle = Math.Atan2(x, -y) * 180 / Math.PI;
        foreach (var slot in Slots)
        {
            if (slot.Ring != ring)
                continue;
            var offset = ((angle - slot.StartAngle) % 360 + 360) % 360;
            if (offset < slot.Sweep)
                return slot;
        }
        return null;
    }
}
