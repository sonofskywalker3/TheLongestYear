using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>
/// Everything that survives a loop reset ("banked forever"): banked Junimo Points,
/// purchased upgrades, the Junimo Stash tier, and meta-condition accumulators
/// (e.g. animal species the player has ever owned across runs). Stored as per-save data and
/// committed with the game's own save (see MetaStore) — scoped to one playthrough.
/// </summary>
public sealed class MetaState
{
    public long JunimoPoints { get; set; }

    /// <summary>IDs of permanently purchased upgrades.</summary>
    public List<string> OwnedUpgrades { get; set; } = new();

    /// <summary>Tier of the Junimo Stash capacity upgrade (0 = base).</summary>
    public int StashCapacityTier { get; set; }

    /// <summary>True once the one-time pre-first-reset save backup has been taken (banked forever).</summary>
    public bool BackupDone { get; set; }

    /// <summary>
    /// Animal species the player has ever owned across all runs in this playthrough.
    /// Drives "Start with [animal]" upgrade availability via the species: meta-requirement
    /// prefix on <see cref="UpgradeDefinition.MetaRequirement"/>. Game-side hookup that
    /// adds to this list when an animal joins a coop/barn is part of a later plan.
    /// </summary>
    public List<string> AnimalSpeciesEverOwned { get; set; } = new();

    /// <summary>
    /// Number of completed resets in this playthrough. Incremented inside
    /// WorldResetService.PerformReset (Plan 06A). Backs the season:&lt;n&gt; meta-requirement
    /// namespace (player has done at least N resets).
    /// </summary>
    public int CompletedResets { get; set; }

    /// <summary>
    /// True once the player has chosen "Keep playing" after winning the loop (CC restored on
    /// Winter 28). Set inside the post-win JP-spend → choice flow; when true, the Winter 28
    /// Win evaluation skips the JP-spend popup AND the choice dialog so the player can play
    /// indefinitely without being asked again. Manual <c>tly_reset</c> still works.
    /// 2026-05-29 spec: continue-after-victory mode.
    /// </summary>
    public bool VictoryAcknowledged { get; set; }

    /// <summary>
    /// Quest ids the player has completed across all runs in this playthrough. Backs the
    /// quest:&lt;id&gt; meta-requirement namespace. Producer is part of a later plan; declared
    /// here so future plans don't have to touch the state class.
    /// </summary>
    public List<string> CompletedQuestsEver { get; set; } = new();

    /// <summary>
    /// Mail flags the player has ever received across all runs in this playthrough. Backs
    /// the mail:&lt;flag&gt; meta-requirement namespace. Producer is part of a later plan.
    /// </summary>
    public List<string> MailFlagsEverReceived { get; set; } = new();

    /// <summary>
    /// Cooking recipe IDs banked in the Cookbook across runs. Keys match
    /// <c>Farmer.cookingRecipes</c> dictionary keys (vanilla recipe id strings, e.g. "Fried_Egg").
    /// On reset, every entry is re-granted to <c>Farmer.cookingRecipes[id] = 0</c>
    /// (the vanilla "learned but never cooked" marker). Slot count is controlled by
    /// which Cookbook I/II/III upgrades the player owns.
    /// </summary>
    public List<string> CookbookRecipes { get; set; } = new();

    /// <summary>
    /// Crafting recipe IDs banked in the Craftbook across runs. Keys match
    /// <c>Farmer.craftingRecipes</c> dictionary keys (vanilla recipe id strings, e.g. "Wood Fence").
    /// On reset, every entry is re-granted to <c>Farmer.craftingRecipes[id] = 0</c>.
    /// </summary>
    public List<string> CraftbookRecipes { get; set; } = new();

    /// <summary>
    /// String IDs of indicator bubbles the player has already dismissed. Prevents the ?/!
    /// bubble from re-appearing after a reset. Values are "tly.cookbook", "tly.craftbook",
    /// and "tly.fireplace". Using <see cref="HashSet{T}"/> so duplicate dismissals are
    /// idempotent; the JSON serializer preserves this as a unique array.
    /// </summary>
    public HashSet<string> DismissedIndicators { get; set; } = new();

    /// <summary>
    /// Items banked in the Junimo Stash across runs. Serialized as part of MetaState
    /// on the game's Saving event (never eagerly). Restored into the world chest on
    /// every reset by <c>JunimoStashService.PopulateFromMeta</c>.
    /// </summary>
    public List<StashItemRecord> StashItems { get; set; } = new();

    /// <summary>
    /// Current slot capacity of the Junimo Stash. Every save gets 4 slots from day 1
    /// (the Junimos bring the chest with them); each owned stash_N upgrade adds 4 more.
    /// stash_1 / stash_2 / stash_3 → 8 / 12 / 16 slots total.
    /// </summary>
    public int StashSlotCount => 4 + HighestKeptTier("stash_", 3) * 4;

    public bool HasUpgrade(string id) => OwnedUpgrades.Contains(id);

    /// <summary>
    /// Return the highest integer N such that an upgrade with id "<paramref name="prefix"/>{N}"
    /// is owned, where 1 ≤ N ≤ <paramref name="maxTier"/>. Returns 0 if none in range are
    /// owned. Used by the reset baseline builder to find the highest "keep" tier the player
    /// has banked for a given tool/skill/floor chain — the cap-not-grant model.
    /// </summary>
    public int HighestKeptTier(string prefix, int maxTier)
    {
        int best = 0;
        foreach (string id in OwnedUpgrades)
        {
            if (!id.StartsWith(prefix, StringComparison.Ordinal))
                continue;
            string suffix = id.Substring(prefix.Length);
            if (!int.TryParse(suffix, out int n))
                continue;
            if (n < 1 || n > maxTier)
                continue;
            if (n > best)
                best = n;
        }
        return best;
    }

    /// <summary>
    /// Evaluate a meta-requirement string against current banked state. Format is "ns:value".
    /// Recognised namespaces:
    /// <list type="bullet">
    ///   <item><term>species:&lt;name&gt;</term><description>Animal species ever owned (case-insensitive). Vanilla Stardew is inconsistent about casing in Data/FarmAnimals vs FarmAnimal.type.</description></item>
    ///   <item><term>upgrade:&lt;id&gt;</term><description>Permanently purchased upgrade id (case-sensitive; our own lowercase ids).</description></item>
    ///   <item><term>quest:&lt;id&gt;</term><description>Quest id ever completed across all runs (case-sensitive; vanilla quest ids are lowercase by convention).</description></item>
    ///   <item><term>mail:&lt;flag&gt;</term><description>Mail flag ever received across all runs (case-insensitive). Vanilla CC code uses mixed casing for mail flags.</description></item>
    ///   <item><term>season:&lt;n&gt;</term><description>True when CompletedResets &gt;= n. "season" here means run-number / number of completed resets, NOT the in-game calendar season (Spring/Summer/Fall/Winter).</description></item>
    /// </list>
    /// Unknown namespaces return false (default-deny) so future-added namespaces remain
    /// forward-compatible with older code paths.
    /// </summary>
    public bool MeetsMetaRequirement(string? requirement)
    {
        if (string.IsNullOrEmpty(requirement))
            return true;
        int colon = requirement.IndexOf(':');
        if (colon <= 0)
            return false;
        string ns = requirement.Substring(0, colon);
        string value = requirement.Substring(colon + 1);
        return ns switch
        {
            "species" => AnimalSpeciesEverOwned.Contains(value, StringComparer.OrdinalIgnoreCase),
            "upgrade" => OwnedUpgrades.Contains(value, StringComparer.Ordinal),
            "quest"   => CompletedQuestsEver.Contains(value, StringComparer.Ordinal),
            "mail"    => MailFlagsEverReceived.Contains(value, StringComparer.OrdinalIgnoreCase),
            // "season" = run number (CompletedResets), NOT the in-game calendar season.
            "season"  => int.TryParse(value, out int threshold) && CompletedResets >= threshold,
            _ => false
        };
    }
}
