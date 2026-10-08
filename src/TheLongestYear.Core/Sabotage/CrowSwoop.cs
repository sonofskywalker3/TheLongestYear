using System;

namespace TheLongestYear.Core.Sabotage;

/// <summary>How a crow comes in to land in the crows scene (designer, 2026-10-08: "they dropped
/// straight down onto the field. They must swoop in from the sides."). Each bird enters from off the
/// left or right edge of the frame, a few tiles above its crop, and flies a quadratic curve that
/// drops steeply at first and flattens out over the last stretch, so it glides in level. The timing
/// eases out: fast on entry, braking to the landing. Pure arithmetic in world pixels, no game types.</summary>
public static class CrowSwoop
{
    /// <summary>The curve's bend sits this far along the way across, at the landing's height less
    /// <see cref="FlareLiftPixels"/>: the start tangent points down at it (the dive) and the end
    /// tangent runs almost level from it (the glide in).</summary>
    public const float BendAcross = 0.55f;

    /// <summary>How far above the landing the bend sits, so the last stretch is a shallow descent and
    /// not dead level (half a tile).</summary>
    public const float FlareLiftPixels = 32f;

    /// <summary>The ease-out power: 1 is constant speed, 2 starts twice as fast and stops at zero.</summary>
    public const double EaseOutPower = 2.2;

    /// <summary>Which side a crow comes in from: alternating by its place in the stagger, so the
    /// flock arrives from both edges; the first goes in from the side its crop is on.</summary>
    public static bool FromLeft(int index, bool firstFromLeft) => (index % 2 == 0) == firstFromLeft;

    /// <summary>Where along the flight the crow is at fraction <paramref name="t"/> of its glide
    /// time (0 at entry, 1 on the ground): fast at first, slowing to land.</summary>
    public static float Progress(float t)
    {
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;
        return (float)(1.0 - Math.Pow(1.0 - t, EaseOutPower));
    }

    /// <summary>The curve's bend point for a flight from <paramref name="start"/> to
    /// <paramref name="landing"/>.</summary>
    public static (float X, float Y) Bend((float X, float Y) start, (float X, float Y) landing)
        => (start.X + (landing.X - start.X) * BendAcross, landing.Y - FlareLiftPixels);

    /// <summary>The crow's position at fraction <paramref name="t"/> of its glide time.</summary>
    public static (float X, float Y) At((float X, float Y) start, (float X, float Y) landing, float t)
    {
        float u = Progress(t);
        (float X, float Y) c = Bend(start, landing);
        float a = (1 - u) * (1 - u), b = 2 * u * (1 - u), d = u * u;
        return (a * start.X + b * c.X + d * landing.X, a * start.Y + b * c.Y + d * landing.Y);
    }
}
