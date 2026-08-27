namespace TheLongestYear.Core;

/// <summary>One enchantment on a stashed tool, by runtime type name and level, so a banked rod
/// or weapon keeps its forge/enchantment state across the loop (same contract the kept-tier tool
/// transplant already honours in FarmerReset).</summary>
/// <param name="Type">Full type name, e.g. <c>StardewValley.Enchantments.AutoHookEnchantment</c>.
/// Resolved against the game assembly on restore; an unknown type (removed mod) is skipped with a
/// log line.</param>
/// <param name="Level">The enchantment's level (forge gems stack 1..3; most others are 1).</param>
public sealed record StashEnchantmentRecord(string Type, int Level);
