using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

public enum YearRuling { YearOne, YearTwo, NeverSell }

/// <summary>Hand-checked year rulings for vanilla items the obtainability model cannot read, or reads
/// past their intent: routes that live in game code rather than data (volcano and mine chests, Mr. Qi's and the volcano
/// dwarf's shops, event and quest rewards, tailoring), checked against the decompiled 1.6 source
/// (2026-10-27). <see cref="YearRuling.NeverSell"/> marks items no shop should offer: currencies and
/// placeholders. Mod items never appear here; they go by the model's rule alone.</summary>
public static class VanillaYearRulings
{
    private static readonly IReadOnlyDictionary<string, YearRuling> Rulings =
        new Dictionary<string, YearRuling>(StringComparer.Ordinal)
        {
            // Year 2 by design: Pierre sells them only from year 2. The model finds edge routes in year 1
            // (Winter Star Garlic through a Seed Maker, the cart's rarely armed Red Cabbage guarantee).
            ["(O)476"] = YearRuling.YearTwo,   // Garlic Seeds
            ["(O)485"] = YearRuling.YearTwo,   // Red Cabbage Seeds
            ["(O)489"] = YearRuling.YearTwo,   // Artichoke Seeds
            // Year 2: volcano chests and dwarf shop, Qi's shop and dangerous floors, the island, the Forge.
            ["(B)854"] = YearRuling.YearTwo,   // Mermaid Boots
            ["(B)855"] = YearRuling.YearTwo,   // Dragonscale Boots
            ["(O)822"] = YearRuling.YearTwo,   // Fossilized Tail
            ["(O)823"] = YearRuling.YearTwo,   // Fossilized Leg
            ["(O)827"] = YearRuling.YearTwo,   // Mummified Bat
            ["(O)828"] = YearRuling.YearTwo,   // Mummified Frog
            ["(O)829"] = YearRuling.YearTwo,   // Ginger
            ["(O)860"] = YearRuling.YearTwo,   // Hot Java Ring
            ["(O)861"] = YearRuling.YearTwo,   // Protection Ring
            ["(O)862"] = YearRuling.YearTwo,   // Soul Sapper Ring
            ["(O)863"] = YearRuling.YearTwo,   // Phoenix Ring
            ["(O)880"] = YearRuling.YearTwo,   // Combined Ring
            ["(O)886"] = YearRuling.YearTwo,   // Warp Totem: Island
            ["(O)891"] = YearRuling.YearTwo,   // Mushroom Tree Seed
            ["(O)896"] = YearRuling.YearTwo,   // Galaxy Soul
            ["(O)903"] = YearRuling.YearTwo,   // Ginger Ale
            ["(O)909"] = YearRuling.YearTwo,   // Radioactive Ore
            ["(O)910"] = YearRuling.YearTwo,   // Radioactive Bar
            ["(O)911"] = YearRuling.YearTwo,   // Horse Flute
            ["(O)913"] = YearRuling.YearTwo,   // Enricher
            ["(O)915"] = YearRuling.YearTwo,   // Pressure Nozzle
            ["(O)917"] = YearRuling.YearTwo,   // Qi Seasoning
            ["(O)918"] = YearRuling.YearTwo,   // Hyper Speed-Gro
            ["(W)54"] = YearRuling.YearTwo,   // Dwarf Sword
            ["(W)55"] = YearRuling.YearTwo,   // Dwarf Hammer
            ["(W)56"] = YearRuling.YearTwo,   // Dwarf Dagger
            ["(W)57"] = YearRuling.YearTwo,   // Dragontooth Cutlass
            ["(W)58"] = YearRuling.YearTwo,   // Dragontooth Club
            ["(W)59"] = YearRuling.YearTwo,   // Dragontooth Shiv
            ["(W)61"] = YearRuling.YearTwo,   // Iridium Needle
            ["(W)62"] = YearRuling.YearTwo,   // Infinity Blade
            ["(W)63"] = YearRuling.YearTwo,   // Infinity Gavel
            ["(W)64"] = YearRuling.YearTwo,   // Infinity Dagger
            // Year 1: mine drops and chests, events, tailoring, green rain, trains, the Forest pylon.
            ["(O)399"] = YearRuling.YearOne,   // Spring Onion: Forest forage spawned in code
            ["(O)445"] = YearRuling.YearOne,   // Caviar: Sturgeon Roe in a Preserves Jar
            ["(B)804"] = YearRuling.YearOne,   // Emily's Magic Boots
            ["(B)806"] = YearRuling.YearOne,   // Leprechaun Shoes
            ["(B)878"] = YearRuling.YearOne,   // Crystal Shoes
            ["(BC)55"] = YearRuling.YearOne,   // Stone Junimo
            ["(O)326"] = YearRuling.YearOne,   // Dwarvish Translation Guide
            ["(O)460"] = YearRuling.YearOne,   // Mermaid's Pendant
            ["(O)859"] = YearRuling.YearOne,   // Lucky Ring
            ["(O)887"] = YearRuling.YearOne,   // Immunity Band
            ["(O)Book_Mystery"] = YearRuling.YearOne,   // Book of Mysteries
            ["(O)FarAwayStone"] = YearRuling.YearOne,   // Far Away Stone
            ["(O)MossySeed"] = YearRuling.YearOne,   // Mossy Seed
            ["(P)10"] = YearRuling.YearOne,   // Baggy Pants
            ["(P)11"] = YearRuling.YearOne,   // Simple Dress
            ["(P)12"] = YearRuling.YearOne,   // Relaxed Fit Pants
            ["(P)13"] = YearRuling.YearOne,   // Relaxed Fit Shorts
            ["(P)5"] = YearRuling.YearOne,   // Dinosaur Pants
            ["(P)6"] = YearRuling.YearOne,   // Grass Skirt
            ["(P)8"] = YearRuling.YearOne,   // Genie Pants
            ["(P)9"] = YearRuling.YearOne,   // Tight Pants
            ["(P)998"] = YearRuling.YearOne,   // Prismatic Pants
            ["(P)999"] = YearRuling.YearOne,   // Prismatic Genie Pants
            ["(W)15"] = YearRuling.YearOne,   // Forest Sword
            ["(W)16"] = YearRuling.YearOne,   // Carving Knife
            ["(W)18"] = YearRuling.YearOne,   // Burglar's Shank
            ["(W)19"] = YearRuling.YearOne,   // Shadow Dagger
            ["(W)2"] = YearRuling.YearOne,   // Dark Sword
            ["(W)22"] = YearRuling.YearOne,   // Wind Spire
            ["(W)24"] = YearRuling.YearOne,   // Wood Club
            ["(W)26"] = YearRuling.YearOne,   // Lead Rod
            ["(W)28"] = YearRuling.YearOne,   // The Slammer
            ["(W)3"] = YearRuling.YearOne,   // Holy Blade
            ["(W)45"] = YearRuling.YearOne,   // Wicked Kris
            ["(W)46"] = YearRuling.YearOne,   // Kudgel
            ["(W)48"] = YearRuling.YearOne,   // Yeti Tooth
            ["(W)52"] = YearRuling.YearOne,   // Tempered Broadsword
            ["(W)6"] = YearRuling.YearOne,   // Iron Edge
            ["(W)60"] = YearRuling.YearOne,   // Ossified Blade
            ["(W)65"] = YearRuling.YearOne,   // Meowmere
            // Never sold: currencies, notes, crates, placeholders and decor nothing hands out.
            ["(B)515"] = YearRuling.NeverSell,   // Cowboy Boots
            ["(BC)0"] = YearRuling.NeverSell,   // House Plant
            ["(BC)1"] = YearRuling.NeverSell,   // House Plant
            ["(BC)2"] = YearRuling.NeverSell,   // House Plant
            ["(BC)22"] = YearRuling.NeverSell,   // Table Piece L
            ["(BC)23"] = YearRuling.NeverSell,   // Table Piece R
            ["(BC)26"] = YearRuling.NeverSell,   // Wood Chair
            ["(BC)27"] = YearRuling.NeverSell,   // Wood Chair
            ["(BC)28"] = YearRuling.NeverSell,   // Skeleton Model
            ["(BC)29"] = YearRuling.NeverSell,   // Obelisk
            ["(BC)3"] = YearRuling.NeverSell,   // House Plant
            ["(BC)31"] = YearRuling.NeverSell,   // Chicken Statue
            ["(BC)4"] = YearRuling.NeverSell,   // House Plant
            ["(BC)5"] = YearRuling.NeverSell,   // House Plant
            ["(BC)56"] = YearRuling.NeverSell,   // Slime Ball
            ["(BC)6"] = YearRuling.NeverSell,   // House Plant
            ["(BC)64"] = YearRuling.NeverSell,   // Bookcase
            ["(BC)65"] = YearRuling.NeverSell,   // Fancy Table
            ["(BC)66"] = YearRuling.NeverSell,   // Ancient Table
            ["(BC)67"] = YearRuling.NeverSell,   // Ancient Stool
            ["(BC)68"] = YearRuling.NeverSell,   // Grandfather Clock
            ["(BC)69"] = YearRuling.NeverSell,   // Teddy Timer
            ["(BC)7"] = YearRuling.NeverSell,   // House Plant
            ["(O)73"] = YearRuling.NeverSell,   // Golden Walnut
            ["(O)79"] = YearRuling.NeverSell,   // Secret Note
            ["(O)842"] = YearRuling.NeverSell,   // Journal Scrap
            ["(O)858"] = YearRuling.NeverSell,   // Qi Gem
            ["(O)892"] = YearRuling.NeverSell,   // Warp Totem: Qi's Arena
            ["(O)922"] = YearRuling.NeverSell,   // SupplyCrate
            ["(O)923"] = YearRuling.NeverSell,   // SupplyCrate
            ["(O)924"] = YearRuling.NeverSell,   // SupplyCrate
            ["(O)925"] = YearRuling.NeverSell,   // Slime Crate
            ["(O)927"] = YearRuling.NeverSell,   // Camping Stove
            ["(O)929"] = YearRuling.NeverSell,   // Hedge
            ["(O)GoldCoin"] = YearRuling.NeverSell,   // Gold Coin
            ["(P)14"] = YearRuling.NeverSell,   // Polka Dot Shorts
            ["(P)15"] = YearRuling.NeverSell,   // Trimmed Lucky Purple Shorts
            ["(W)49"] = YearRuling.NeverSell,   // Rapier
        };

    public static YearRuling? Ruling(string qualifiedId)
        => Rulings.TryGetValue(qualifiedId, out YearRuling r) ? r : null;
}
