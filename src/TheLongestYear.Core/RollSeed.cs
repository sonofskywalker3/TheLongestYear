using System;

namespace TheLongestYear.Core;

/// <summary>One full-avalanche seed for a Randomizer roll. The legacy seeded <see cref="Random"/>
/// turns related seeds (for example <c>seed ^ week*7919 ^ theme*1031 ^ salt</c>) into related first
/// draws, which made the two cards of one week roll in lockstep (final review I1, 2026-10-06). This
/// runs the parts through MurmurHash3's 32-bit block mix and <c>fmix32</c> finalizer, so a change in
/// any one part changes every bit of the result.
/// Only the Randomizer rolls use it: the selection offer, goal sampling, the reroll stream, cart
/// days and the reward shuffle keep their own seeds so the everything-off game stays 0.18.145.</summary>
public static class RollSeed
{
    private const uint C1 = 0xCC9E2D51, C2 = 0x1B873593, BlockAdd = 0xE6546B64;
    private const uint Fmix1 = 0x85EBCA6B, Fmix2 = 0xC2B2AE35;
    private const uint Initial = 0x5EED_7A1E;
    private const int PositiveMask = 0x7FFFFFFF;

    /// <summary>A non-negative seed for <c>new Random(...)</c> from the given parts, order sensitive.</summary>
    public static int Mix(params int[] parts)
    {
        uint h = Initial;
        foreach (int part in parts)
        {
            uint k = unchecked((uint)part * C1);
            k = RotateLeft(k, 15);
            k = unchecked(k * C2);
            h ^= k;
            h = RotateLeft(h, 13);
            h = unchecked(h * 5 + BlockAdd);
        }
        h ^= (uint)parts.Length;
        return (int)(Fmix32(h) & PositiveMask);
    }

    /// <summary>A <see cref="Random"/> seeded from <see cref="Mix"/>.</summary>
    public static Random Rng(params int[] parts) => new(Mix(parts));

    private static uint Fmix32(uint h)
    {
        h ^= h >> 16;
        h = unchecked(h * Fmix1);
        h ^= h >> 13;
        h = unchecked(h * Fmix2);
        h ^= h >> 16;
        return h;
    }

    private static uint RotateLeft(uint x, int r) => (x << r) | (x >> (32 - r));
}
