using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Every chain a focus card can be about, in table order (the order the dealer shuffles from).</summary>
public static class FocusChains
{
    private static readonly string[] MetalToolSlugs = { "hoe", "pickaxe", "axe", "watering_can" };
    private static readonly string[] KnackSlugs =
        { "green_thumb", "coal_vein", "foragers_eye", "quick_bite", "jp_boost", "shop_discount" };

    private const int MetalToolRungs = 4;
    private const int RodFirstTier = 0;
    private const int RodLastTier = 2;
    private const int KnackRungs = 5;
    private const int WeatherSageRungs = 6;
    private const int CartSlotFirstTier = 2;
    private const int CartSlotLastTier = 10;
    private const int StashRungs = 3;
    private const int BackpackRungs = 2;

    private static readonly IReadOnlyList<FocusChain> _all = Build();
    private static readonly IReadOnlyDictionary<string, FocusChain> _byId =
        _all.ToDictionary(c => c.Id, StringComparer.Ordinal);

    public static IReadOnlyList<FocusChain> All => _all;

    public static IReadOnlyList<FocusChain> ForTheme(FocusTheme theme)
        => _all.Where(c => c.Theme == theme).ToList();

    public static FocusChain? TryGet(string id)
        => id != null && _byId.TryGetValue(id, out FocusChain? c) ? c : null;

    private static string[] Numbered(string prefix, int from, int to)
        => Enumerable.Range(from, to - from + 1).Select(n => $"{prefix}_{n}").ToArray();

    private static IReadOnlyList<FocusChain> Build()
    {
        var list = new List<FocusChain>();
        foreach (string slug in MetalToolSlugs)
            list.Add(new FocusChain(slug, FocusTheme.Tools, Numbered("keep_" + slug, 1, MetalToolRungs)));
        list.Add(new FocusChain("fishing_rod", FocusTheme.Tools, Numbered("keep_fishing_rod", RodFirstTier, RodLastTier)));

        list.Add(new FocusChain("coop", FocusTheme.Buildings, new[] { "keep_coop", "keep_big_coop", "keep_deluxe_coop" }));
        list.Add(new FocusChain("barn", FocusTheme.Buildings, new[] { "keep_barn", "keep_big_barn", "keep_deluxe_barn" }));
        list.Add(new FocusChain("silo", FocusTheme.Buildings, new[] { "keep_silo" }));
        list.Add(new FocusChain("fish_pond", FocusTheme.Buildings, new[] { FishPondKeep.UpgradeId }));
        list.Add(new FocusChain("house", FocusTheme.Buildings, new[] { "keep_kitchen", "keep_basement" }));
        list.Add(new FocusChain("shortcuts", FocusTheme.Buildings, new[] { "keep_shortcuts" }));

        foreach (string slug in KnackSlugs)
            list.Add(new FocusChain(slug, FocusTheme.Knacks, Numbered(slug, 1, KnackRungs)));
        list.Add(new FocusChain("weather_sage", FocusTheme.Knacks, Numbered("weather_sage", 1, WeatherSageRungs)));
        list.Add(new FocusChain("cart_slot", FocusTheme.Knacks, Numbered("cart_slot", CartSlotFirstTier, CartSlotLastTier)));
        list.Add(new FocusChain("stash", FocusTheme.Knacks, Numbered("stash", 1, StashRungs)));
        list.Add(new FocusChain("backpack", FocusTheme.Knacks, Numbered("backpack", 1, BackpackRungs)));
        return list;
    }
}
