using System;
using System.IO;
using System.Text.Json;
using TheLongestYear.Core.Lexicon;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

/// <summary>The unmodded game's inputs, dumped in-game with <c>tly_lexicon dumpinputs</c> (Cart Catalog
/// disabled) into <c>Fixtures/</c>, so tests can build the vanilla answers offline: the obtainability
/// inputs (<c>vanilla_obtainability_inputs.json</c>) and the availability inputs
/// (<c>vanilla_availability_inputs.json</c>) of a fresh save, and the obtainability inputs of a save
/// that owns the <c>pierre_year2_seeds</c> upgrade (<c>vanilla_obtainability_inputs_pierre.json</c>),
/// whose Data/Shops TLY edits. That save's availability inputs differ from the fresh save's only in
/// <see cref="AvailabilityInputs.YearTwoCapped"/> (checked when the fixtures were dumped), so one
/// file serves both.</summary>
internal static class VanillaInputsFixture
{
    public const string FileName = "vanilla_obtainability_inputs.json";
    public const string PierreFileName = "vanilla_obtainability_inputs_pierre.json";
    public const string AvailabilityFileName = "vanilla_availability_inputs.json";

    public static ObtainabilityInputs Load() => LoadObtainability(FileName);

    /// <summary>The obtainability inputs of a save that owns the Pierre year-2 seeds upgrade.</summary>
    public static ObtainabilityInputs LoadPierre() => LoadObtainability(PierreFileName);

    /// <summary>The availability inputs of a fresh save, or (<paramref name="pierre"/>) of one that owns
    /// the Pierre year-2 seeds upgrade: the same inputs with the year-two crops uncapped.</summary>
    public static AvailabilityInputs LoadAvailability(bool pierre)
    {
        AvailabilityInputs fresh = JsonSerializer.Deserialize<AvailabilityInputs>(File.ReadAllText(PathOf(AvailabilityFileName)), LexiconJson.Options)
            ?? throw new InvalidDataException($"{AvailabilityFileName} deserialised to null.");
        return pierre ? fresh with { YearTwoCapped = false } : fresh;
    }

    private static ObtainabilityInputs LoadObtainability(string name)
        => JsonSerializer.Deserialize<ObtainabilityInputs>(File.ReadAllText(PathOf(name)))
            ?? throw new InvalidDataException($"{name} deserialised to null.");

    private static string PathOf(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
}
