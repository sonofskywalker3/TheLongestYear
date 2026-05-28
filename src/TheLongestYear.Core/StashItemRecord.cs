namespace TheLongestYear.Core;

/// <summary>
/// Serializable snapshot of a single item in the Junimo Stash. Round-trips through
/// System.Text.Json via <see cref="MetaState.StashItems"/>. Only stores what is
/// needed to recreate the item via ItemRegistry.Create on restore.
/// </summary>
public sealed record StashItemRecord(string ItemId, int Quantity, int Quality);
