using StardewValley;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Resets the persistent <see cref="Farmer"/> to a run baseline. Game1.loadForNewGame rebuilds the
    /// world but leaves the existing player's money/skills/inventory/relationships intact, so we clear
    /// them here. (Plan 07 will carve the Junimo Stash out of the inventory wipe.)
    /// </summary>
    internal static class FarmerReset
    {
        public static void ToBaseline(Farmer p, int startingMoney)
        {
            p.Money = startingMoney;

            // Inventory — wipe CONTENTS but preserve the slot count (Stash preservation is Plan 07).
            // p.Items.Clear() removes the slot list itself, which leaves MaxItems lookups
            // returning 0 → addItemToInventory always fails with "inventory full" (2026-05-26
            // round-3 playtest: 'I tried to pick up a daffodil and it said my inventory was
            // full, but it's empty'). Reset the backpack to the 12-slot baseline + null each
            // slot's contents.
            p.MaxItems = 12;
            p.Items.Clear();
            for (int i = 0; i < p.MaxItems; i++)
                p.Items.Add(null);

            // Skills: XP array + the six derived levels.
            for (int i = 0; i < p.experiencePoints.Count; i++)
                p.experiencePoints[i] = 0;
            p.farmingLevel.Value = 0;
            p.miningLevel.Value = 0;
            p.fishingLevel.Value = 0;
            p.foragingLevel.Value = 0;
            p.combatLevel.Value = 0;
            p.luckLevel.Value = 0;
            p.professions.Clear();

            // Relationships, mail, events, quests.
            p.friendshipData.Clear();
            p.mailReceived.Clear();
            p.eventsSeen.Clear();
            p.questLog.Clear();

            // Suppress the vanilla intro cutscene from replaying every loop (matches TitleMenu's new-game path).
            p.eventsSeen.Add("60367");

            // Vitals to full.
            p.stamina = p.maxStamina.Value;
            p.health = p.maxHealth;
        }
    }
}
