using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Enchantments;
using StardewValley.Objects;
using StardewValley.Objects.Trinkets;
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
        /// <summary>Deepest container nesting ToRecord walks. Vanilla never nests this deep; the cap
        /// only guards against a mod container that holds itself.</summary>
        internal const int MaxNestingDepth = 16;

        /// <summary>Log sink for the static paths that have no caller monitor (ToRecord, the mod
        /// item-list lookup). Set by JunimoStashService.</summary>
        internal static IMonitor Monitor;

        /// <summary>Snapshot one item (top-level or a tool attachment) into a record. Flavored/
        /// preserved goods (Smoked Fish, Wine, Jelly, Aged Roe, Honey, Bait, …) bake their source
        /// identity + sale price into preservedParentSheetIndex / preserve / price.Value. Recreating
        /// by base id alone loses ALL of it (a Smoked Legend comes back as a blank 57g smoked fish),
        /// so capture those fields when present. Plain items have no preserve identity → leave the
        /// fields null so restore doesn't touch them. A tool with slots records each slot in order
        /// (null for an empty one) so rod bait/tackle survive the loop.</summary>
        internal static StashItemRecord ToRecord(Item item) => ToRecord(item, 0);

        private static StashItemRecord ToRecord(Item item, int depth)
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
                        attachments.Add(slot == null ? null : ToRecord(slot, depth + 1));
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
                ? combined.combinedRings.Where(r => r != null).Select(r => ToRecord(r, depth + 1)).ToList()
                : null;
            int? trinketSeed = item is Trinket trinket ? trinket.generationSeed.Value : null;

            // An empty container records null, never an empty list: the legacy rescue reads a
            // non-null list as "has contents".
            List<StashItemRecord> contents = null;
            StashItemRecord heldObject = null;
            IList<Item> held = ContainerItems(item);
            bool hasContents = held != null && held.Any(i => i != null);
            StardewValley.Object heldValue = (item as StardewValley.Object)?.heldObject.Value;
            if (depth >= MaxNestingDepth)
            {
                if (hasContents || heldValue != null)
                    Monitor?.Log($"StashItemCodec: '{item.QualifiedItemId}' is nested more than {MaxNestingDepth} deep; its contents are not recorded.", LogLevel.Warn);
            }
            else
            {
                if (hasContents)
                    contents = held.Where(i => i != null).Select(i => ToRecord(i, depth + 1)).ToList();
                if (heldValue != null)
                    heldObject = ToRecord(heldValue, depth + 1);
            }

            return new StashItemRecord(
                item.QualifiedItemId,
                item.Stack,
                quality,
                hasPreserveIdentity ? obj.preservedParentSheetIndex.Value : null,
                hasPreserveIdentity && obj.preserve.Value.HasValue ? (int)obj.preserve.Value.Value : null,
                hasPreserveIdentity ? obj.Price : null,
                attachments,
                enchantments,
                Contents: contents, HeldObject: heldObject,
                Clothing: clothing, Boots: boots, InnerRings: innerRings, TrinketSeed: trinketSeed);
        }

        // Mod item types that expose their own item list, resolved once per type.
        private static readonly Dictionary<System.Type, System.Reflection.MemberInfo> ModItemLists = new();

        /// <summary>The live item list a container item holds: a dresser's or fish tank's
        /// heldItems, a carried chest's Items, or a mod bag's own list. Null for anything else.</summary>
        internal static IList<Item> ContainerItems(Item item)
        {
            switch (item)
            {
                case StorageFurniture storage: return storage.heldItems;
                case Chest chest: return chest.Items;
            }
            return ModItemList(item);
        }

        // A mod bag: any public instance field or readable property holding an IList<Item>, on a type
        // the game assembly does not define. Game types are covered by the switch above.
        private static IList<Item> ModItemList(Item item)
        {
            System.Type type = item?.GetType();
            if (type == null || type.Assembly == typeof(Item).Assembly)
                return null;
            if (!ModItemLists.TryGetValue(type, out System.Reflection.MemberInfo member))
            {
                const System.Reflection.BindingFlags Public = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
                member = (System.Reflection.MemberInfo)type.GetFields(Public).FirstOrDefault(f => typeof(IList<Item>).IsAssignableFrom(f.FieldType))
                    ?? type.GetProperties(Public).FirstOrDefault(p => p.CanRead && p.GetIndexParameters().Length == 0 && typeof(IList<Item>).IsAssignableFrom(p.PropertyType));
                ModItemLists[type] = member;
            }
            try
            {
                return member switch
                {
                    System.Reflection.FieldInfo f => f.GetValue(item) as IList<Item>,
                    System.Reflection.PropertyInfo p => p.GetValue(item) as IList<Item>,
                    _ => null,
                };
            }
            catch (System.Reflection.TargetInvocationException ex)
            {
                // A mod getter that throws must not take the whole bank down (BankToMeta has
                // already cleared the list). Treat the type as not a container from now on, so
                // this logs once per type.
                ModItemLists[type] = null;
                Monitor?.Log($"StashItemCodec: reading '{type.FullName}.{member.Name}' threw ({ex.InnerException?.GetType().Name}: {ex.InnerException?.Message}); treating it as not a container.", LogLevel.Warn);
                return null;
            }
        }

        /// <summary>Recreate one banked item; nested items with nowhere to go are logged and lost.
        /// Callers that must never lose an item use the overload with an orphan list.</summary>
        internal static Item CreateFromRecord(StashItemRecord record, IMonitor monitor)
        {
            var orphans = new List<Item>();
            Item item = CreateFromRecord(record, monitor, orphans);
            if (orphans.Count > 0)
                monitor?.Log($"StashItemCodec: {orphans.Count} nested item(s) of '{record.ItemId}' had nowhere to go.", LogLevel.Warn);
            return item;
        }

        /// <summary>Recreate one banked item from its record: registry lookup by id/stack/quality,
        /// the flavored-good identity, then a tool's attachment slots and enchantments, then a
        /// container's contents and held object. Null when the id is unknown to this game (mod item
        /// from a removed mod, typo). Nested items the recreated item cannot hold (its type no
        /// longer holds items) go to <paramref name="orphans"/> instead of being lost.</summary>
        internal static Item CreateFromRecord(StashItemRecord record, IMonitor monitor, List<Item> orphans)
        {
            Item item = ItemRegistry.Create(record.ItemId, record.Quantity, record.Quality,
                allowNull: true);
            if (item == null)
            {
                // The container is gone, but what it held is not: hand those items out.
                RescueNestedOfUnknown(record, monitor, orphans);
                return null;
            }

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
                    if (CreateFromRecord(inner, monitor, orphans) is Ring ring)
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
                    if (CreateFromRecord(slotRecord, monitor, orphans) is StardewValley.Object attachment)
                        tool.attachments[i] = attachment;
                    else
                        monitor?.Log(
                            $"StashItemCodec: could not recreate attachment '{slotRecord.ItemId}' on '{record.ItemId}', slot left empty.",
                            LogLevel.Warn);
                }
            }

            if (item is Tool enchanted && record.Enchantments != null)
                RestoreEnchantments(enchanted, record, monitor);

            if (record.Contents != null)
            {
                IList<Item> target = ContainerItems(item);
                foreach (StashItemRecord childRecord in record.Contents)
                {
                    if (childRecord == null) continue;
                    Item child = CreateFromRecord(childRecord, monitor, orphans);
                    if (child == null)
                        monitor?.Log($"StashItemCodec: could not recreate '{childRecord.ItemId}' inside '{record.ItemId}' (unknown id).", LogLevel.Warn);
                    else if (target != null)
                        target.Add(child);
                    else
                        orphans.Add(child);
                }
            }
            if (record.HeldObject != null)
            {
                Item heldItem = CreateFromRecord(record.HeldObject, monitor, orphans);
                if (item is StardewValley.Object holder && heldItem is StardewValley.Object heldObj)
                    holder.heldObject.Value = heldObj;
                else if (heldItem != null)
                    orphans.Add(heldItem);
            }

            return item;
        }

        /// <summary>A container whose own id is unknown still had contents and a held object;
        /// recreate them into <paramref name="orphans"/> so nothing is deleted.</summary>
        private static void RescueNestedOfUnknown(StashItemRecord record, IMonitor monitor, List<Item> orphans)
        {
            var nested = new List<StashItemRecord>();
            if (record.Contents != null)
                nested.AddRange(record.Contents.Where(r => r != null));
            if (record.HeldObject != null)
                nested.Add(record.HeldObject);
            foreach (StashItemRecord childRecord in nested)
            {
                if (CreateFromRecord(childRecord, monitor, orphans) is Item child)
                    orphans.Add(child);
                else
                    monitor?.Log($"StashItemCodec: could not recreate '{childRecord.ItemId}' inside unknown '{record.ItemId}' (unknown id).", LogLevel.Warn);
            }
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
