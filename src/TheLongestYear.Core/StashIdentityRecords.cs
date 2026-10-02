namespace TheLongestYear.Core;

/// <summary>A stashed shirt's or pants' colour. Dyeing writes <c>Clothing.clothesColor</c> on the
/// instance, so a shirt rebuilt by id comes back undyed without this.</summary>
/// <param name="Color">The colour's packed RGBA value (<c>Color.PackedValue</c>).</param>
/// <param name="Dyeable">The instance's <c>dyeable</c> flag.</param>
public sealed record StashClothingRecord(uint Color, bool Dyeable);

/// <summary>Stashed boots' tailoring, the four fields vanilla <c>Boots.GetOneCopyFrom</c> copies.
/// Tailoring boots copies another pair's stats onto the instance.</summary>
public sealed record StashBootsRecord(string? AppliedBootSheetIndex, int ColorIndex, int Defense, int Immunity);
