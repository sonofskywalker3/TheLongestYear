namespace TheLongestYear.Core;

/// <summary>One concrete slot of a bundle: its position in the Data/Bundles ingredient line
/// (category slots count in the numbering, so it lines up with the board's bool[]) and the
/// normalized qualified id it wants. A repeated id is two slots.</summary>
public readonly record struct BundleSlot(int IngredientIndex, string ItemId);
