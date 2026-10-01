using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Enchantments;
using StardewValley.Objects;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Item to <see cref="StashItemRecord"/> and back. The stash does not carry item instances across
    /// the loop: <see cref="JunimoStashService.BankToMeta"/> writes a record per item and
    /// <see cref="JunimoStashService.PopulateFromMeta"/> rebuilds a fresh item from it, so anything an
    /// item holds on its instance has to be written here and put back here.
    /// </summary>
    internal static class StashItemCodec
    {
        /// <summary>Snapshot one item (top-level or a tool attachment) into a record. Flavored/
        /// preserved goods (Smoked Fish, Wine, Jelly, Aged Roe, Honey, Bait, …) bake their source
        /// identity + sale price into preservedParentSheetIndex / preserve / price.Value. Recreating
        /// by base id alone loses ALL of it (a Smoked Legend comes back as a blank 57g smoked fish),
        /// so capture those fields when present. Plain items have no preserve identity → leave the
        /// fields null so restore doesn't touch them. A tool with slots records each slot in order
        /// (null for an empty one) so rod bait/tackle survive the loop.</summary>
        internal static StashItemRecord ToRecord(Item item)
        {
            var obj = item as StardewValley.Object;
            int quality = obj?.quality.Value ?? 0;

            bool hasPreserveIdentity = obj != null &&
                (!string.IsNullOrEmpty(obj.preservedParentSheetIndex.Value) || obj.preserve.Value.HasValue);

            List<StashItemRecord> attachments = null;
            List<StashEnchantmentRecord> enchantments = null;
            if (item is Tool tool)
            {
                if (tool.attachments.Count > 0)
                {
                    attachments = new List<StashItemRecord>(tool.attachments.Count);
                    foreach (StardewValley.Object slot in tool.attachments)
                        attachments.Add(slot == null ? null : ToRecord(slot));
                }
                if (tool.enchantments.Count > 0)
                {
                    enchantments = new List<StashEnchantmentRecord>(tool.enchantments.Count);
                    foreach (BaseEnchantment e in tool.enchantments)
                        enchantments.Add(new StashEnchantmentRecord(e.GetType().FullName, e.GetLevel()));
                }
            }

            StashClothingRecord clothing = item is Clothing shirt
                ? new StashClothingRecord(shirt.clothesColor.Value.PackedValue, shirt.dyeable.Value)
                : null;
            StashBootsRecord boots = item is Boots pair
                ? new StashBootsRecord(pair.appliedBootSheetIndex.Value, pair.indexInColorSheet.Value,
                    pair.defenseBonus.Value, pair.immunityBonus.Value)
                : null;
            List<StashItemRecord> innerRings = item is CombinedRing combined && combined.combinedRings.Count > 0
                ? combined.combinedRings.Where(r => r != null).Select(ToRecord).ToList()
                : null;
            int? trinketSeed = item is Trinket trinket ? trinket.generationSeed.Value : null;

            return new StashItemRecord(
                item.QualifiedItemId,
                item.Stack,
                quality,
                hasPreserveIdentity ? obj.preservedParentSheetIndex.Value : null,
                hasPreserveIdentity && obj.preserve.Value.HasValue ? (int)obj.preserve.Value.Value : null,
                hasPreserveIdentity ? obj.Price : null,
                attachments,
                enchantments,
                Clothing: clothing, Boots: boots, InnerRings: innerRings, TrinketSeed: trinketSeed);
        }

        /// <summary>Recreate one banked item from its record: registry lookup by id/stack/quality,
        /// the flavored-good identity, then a tool's attachment slots and enchantments. Null when the
        /// id is unknown to this game (mod item from a removed mod, typo).</summary>
        internal static Item CreateFromRecord(StashItemRecord record, IMonitor monitor)
        {
            Item item = ItemRegistry.Create(record.ItemId, record.Quantity, record.Quality,
                allowNull: true);
            if (item == null)
                return null;

            // A trinket's stats are rolled from its seed; the registry rolls a new random seed.
            if (record.TrinketSeed.HasValue && item is Trinket rolled)
                item = new Trinket(rolled.ItemId, record.TrinketSeed.Value);

            if (record.Clothing != null && item is Clothing clothes)
            {
                clothes.clothesColor.Value = new Microsoft.Xna.Framework.Color(record.Clothing.Color);
                clothes.dyeable.Value = record.Clothing.Dyeable;
            }

            // Tailored boots: the same four fields vanilla Boots.GetOneCopyFrom copies.
            if (record.Boots != null && item is Boots boots)
            {
                boots.appliedBootSheetIndex.Value = record.Boots.AppliedBootSheetIndex;
                boots.indexInColorSheet.Value = record.Boots.ColorIndex;
                boots.defenseBonus.Value = record.Boots.Defense;
                boots.immunityBonus.Value = record.Boots.Immunity;
            }

            if (record.InnerRings != null && item is CombinedRing combined)
            {
                combined.combinedRings.Clear();
                foreach (StashItemRecord inner in record.InnerRings)
                {
                    if (CreateFromRecord(inner, monitor) is Ring ring)
                        combined.combinedRings.Add(ring);
                    else
                        monitor?.Log($"StashItemCodec: could not recreate inner ring '{inner?.ItemId}' of a Combined Ring.", LogLevel.Warn);
                }
            }

            // Re-apply a flavored good's source identity + baked price (see BankToMeta). Mirrors
            // the game's own Object.GetOneCopyFrom, which copies exactly these three fields. Only
            // set when captured (null for plain items), so non-preserved items keep their
            // data-driven price untouched.
            if (item is StardewValley.Object obj)
            {
                if (record.PreservedParentSheetIndex != null)
                    obj.preservedParentSheetIndex.Value = record.PreservedParentSheetIndex;
                if (record.Preserve.HasValue)
                    obj.preserve.Value = (StardewValley.Object.PreserveType)record.Preserve.Value;
                if (record.Price.HasValue)
                    obj.Price = record.Price.Value;
            }

            // A stashed tool's slots (rod bait/tackle) are instance state the registry cannot
            // rebuild, same class as the kept-tier rod transplant in FarmerReset. Clamped to the
            // recreated tool's slot count so a stale record can never overflow.
            if (item is Tool tool && record.Attachments != null)
            {
                int slots = System.Math.Min(record.Attachments.Count, tool.attachments.Count);
                for (int i = 0; i < slots; i++)
                {
                    StashItemRecord slotRecord = record.Attachments[i];
                    if (slotRecord == null) continue;
                    if (CreateFromRecord(slotRecord, monitor) is StardewValley.Object attachment)
                        tool.attachments[i] = attachment;
                    else
                        monitor?.Log(
                            $"StashItemCodec: could not recreate attachment '{slotRecord.ItemId}' on '{record.ItemId}', slot left empty.",
                            LogLevel.Warn);
                }
            }

            if (item is Tool enchanted && record.Enchantments != null)
                RestoreEnchantments(enchanted, record, monitor);

            return item;
        }

        /// <summary>Put a banked tool's enchantments back. Mirrors vanilla Tool.CopyEnchantments
        /// (add the instance, then ApplyTo) rather than AddEnchantment, which bumps a forge one
        /// level per call instead of restoring the recorded level. Types resolve against the game
        /// assembly; an unknown one is logged and skipped so a removed mod can't break the whole
        /// stash.</summary>
        private static void RestoreEnchantments(Tool tool, StashItemRecord record, IMonitor monitor)
        {
            foreach (StashEnchantmentRecord e in record.Enchantments)
            {
                if (e?.Type == null) continue;
                System.Type type = System.Type.GetType(e.Type) ?? typeof(BaseEnchantment).Assembly.GetType(e.Type);
                if (type == null || !typeof(BaseEnchantment).IsAssignableFrom(type))
                {
                    monitor?.Log(
                        $"StashItemCodec: unknown enchantment type '{e.Type}' on '{record.ItemId}', skipping.",
                        LogLevel.Warn);
                    continue;
                }
                if (System.Activator.CreateInstance(type) is not BaseEnchantment enchantment) continue;
                enchantment.Level = e.Level;
                tool.enchantments.Add(enchantment);
                enchantment.ApplyTo(tool);
            }
        }
    }
}
