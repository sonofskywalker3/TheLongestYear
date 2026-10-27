using System;
using System.IO;
using System.Text.Json;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

/// <summary>The unmodded game's obtainability inputs, dumped in-game with <c>tly_lexicon dumpinputs</c>
/// (Cart Catalog disabled) into <c>Fixtures/vanilla_obtainability_inputs.json</c>, so tests can build
/// the vanilla model offline.</summary>
internal static class VanillaInputsFixture
{
    public const string FileName = "vanilla_obtainability_inputs.json";

    public static ObtainabilityInputs Load()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", FileName);
        return JsonSerializer.Deserialize<ObtainabilityInputs>(File.ReadAllText(path))
            ?? throw new InvalidDataException($"{FileName} deserialised to null.");
    }
}
