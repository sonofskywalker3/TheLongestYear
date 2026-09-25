using System.Collections.Generic;
using TheLongestYear.Core;

namespace TheLongestYear.Tests;

public class AnimalSpeciesTests
{
    [Theory]
    [InlineData("White Chicken", "Chicken")]
    [InlineData("Brown Chicken", "Chicken")]
    [InlineData("Blue Chicken", "Chicken")]
    [InlineData("Void Chicken", "VoidChicken")]
    [InlineData("Golden Chicken", "GoldenChicken")]
    [InlineData("White Cow", "Cow")]
    [InlineData("Brown Cow", "Cow")]
    [InlineData("Dairy Cow", "Cow")]
    [InlineData("Duck", "Duck")]
    [InlineData("Rabbit", "Rabbit")]
    [InlineData("Dinosaur", "Dinosaur")]
    [InlineData("Ostrich", "Ostrich")]
    [InlineData("Goat", "Goat")]
    [InlineData("Sheep", "Sheep")]
    [InlineData("Pig", "Pig")]
    [InlineData("Chicken", "Chicken")]
    [InlineData("VoidChicken", "VoidChicken")]
    [InlineData("Modded Llama", "Modded Llama")]
    public void Normalize_maps_vanilla_types_to_gate_species(string vanillaType, string expected)
        => Assert.Equal(expected, AnimalSpecies.Normalize(vanillaType));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_blank_is_empty(string? vanillaType)
        => Assert.Equal("", AnimalSpecies.Normalize(vanillaType));

    [Fact]
    public void Record_adds_the_normalized_species_once()
    {
        var owned = new List<string>();
        Assert.True(AnimalSpecies.Record(owned, "White Chicken"));
        Assert.False(AnimalSpecies.Record(owned, "Brown Chicken"));
        Assert.Equal(new[] { "Chicken" }, owned);
    }

    [Fact]
    public void Record_skips_a_species_an_old_save_stored_under_its_vanilla_name()
    {
        var owned = new List<string> { "White Cow" };
        Assert.False(AnimalSpecies.Record(owned, "Brown Cow"));
        Assert.Single(owned);
    }

    [Fact]
    public void Record_ignores_a_blank_type()
    {
        var owned = new List<string>();
        Assert.False(AnimalSpecies.Record(owned, null));
        Assert.Empty(owned);
    }

    [Fact]
    public void Void_chicken_is_not_a_chicken()
        => Assert.False(AnimalSpecies.Matches("Void Chicken", "Chicken"));
}
