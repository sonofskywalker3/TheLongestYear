using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;
using TheLongestYear.Core;
using TheLongestYear.UI;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Enforces the Junimo Stash slot cap on <see cref="Chest.addItem"/>.
    ///
    /// The patch is a postfix: vanilla runs first and handles stacking (stacking onto an
    /// existing stack is always allowed because it doesn't consume a new slot). After vanilla
    /// returns null (item accepted), we check whether the stash's non-null item count now
    /// exceeds the cap. If it does, we remove the just-added item, restore it to the return
    /// value, and show a HUD message.
    ///
    /// Gate: only fires when <c>chest.modData["tly.junimo.stash"] == "1"</c>.
    /// </summary>
    [HarmonyPatch(typeof(Chest), nameof(Chest.addItem))]
    internal static class JunimoStashCapPatch
    {
        private static IMonitor _monitor;
        // ReSharper disable once MemberCanBePrivate.Global — read by JunimoStashShowMenuPatch
        // in the same file for the stash-intro-quest dismissal write.
        internal static MetaState _meta;

        public static void Connect(IMonitor monitor, MetaState meta)
        {
            _monitor = monitor;
            _meta    = meta;
        }

        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static void Postfix(Chest __instance, Item item, ref Item __result)
        {
            // Only act on the stash chest, and only when vanilla accepted the item (result null).
            if (__result != null)
                return;
            if (!__instance.modData.ContainsKey(JunimoStashService.StashModDataKey))
                return;
            if (_meta == null)
                return;

            int cap = _meta.StashSlotCount;
            if (cap == 0)
                return;

            // Count non-null items in the player's inventory for this chest.
            // GetItemsForPlayer() returns the right IInventory (single-player = Items).
            var inv = __instance.GetItemsForPlayer();
            int nonNullCount = 0;
            for (int i = 0; i < inv.Count; i++)
            {
                if (inv[i] != null) nonNullCount++;
            }

            if (nonNullCount <= cap)
                return;  // within cap — accept as-is

            // Slot cap exceeded. Undo vanilla's acceptance: remove the item from the inventory.
            // The item landed in one of: a null slot (set by reference), or was added via .Add().
            // We find it by scanning for the item reference itself.
            for (int i = inv.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(inv[i], item))
                {
                    inv[i] = null;
                    break;
                }
            }

            // Return the item (rejected).
            __result = item;

            // HUD message.
            Game1.showRedMessage(
                $"Junimo Stash is full! ({cap} slot{(cap == 1 ? "" : "s")} maximum)");

            _monitor?.Log(
                $"JunimoStashCapPatch: rejected item '{item.QualifiedItemId}' — stash at cap ({cap}).",
                LogLevel.Trace);
        }
    }

    /// <summary>
    /// Dismisses the "tly.stash" indicator the first time the player opens the stash chest,
    /// and strips the chest-tint color picker from the opened ItemGrabMenu. Vanilla ItemGrabMenu
    /// shows the color picker for any Chest whose <c>SpecialChestType</c> is None or BigChest —
    /// our stash chest is forced to SpecialChestType.None (to keep local per-save inventory
    /// instead of the team-shared Junimo Chest pool), which would normally surface the picker.
    /// The 2026-05-28 playtest explicitly asked for it to be hidden.
    ///
    /// 2026-05-29 round 13: user reported the picker reappearing after adding an item to the
    /// stash. Root cause: <c>Chest.grabItemFromInventory</c> calls <c>ShowMenu()</c> AGAIN
    /// after each item transfer, which constructs a fresh ItemGrabMenu (picker included).
    /// Our ShowMenu postfix DID strip that — but vanilla's
    /// <c>grabItemFromInventory</c> then snaps the cursor to the previously-snapped component
    /// ID, and the stale fillStacks <c>upNeighborID</c> (still pointing at the now-null toggle
    /// at ID 27346) lets controller navigation flash phantom-pointer the picker location.
    /// Belt-and-suspenders fix: (1) reset fillStacks.upNeighborID to -500 after the strip so
    /// no neighbor points at the dead toggle; (2) <see cref="JunimoStashColorPickerScrubPatch"/>
    /// re-runs the strip on every update tick for any open stash menu, catching ANY path
    /// (window resize, setSourceItem, future SMAPI mod interaction) that might recreate it.
    /// </summary>
    [HarmonyPatch(typeof(Chest), nameof(Chest.ShowMenu))]
    internal static class JunimoStashShowMenuPatch
    {
        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static void Postfix(Chest __instance)
        {
            if (!__instance.modData.ContainsKey(JunimoStashService.StashModDataKey))
                return;

            // Mark the stash intro quest as "seen" so it doesn't re-fire on the next reset.
            // Same DismissedIndicators set the cookbook/craftbook menus write to — the
            // visual ? indicator was removed 2026-05-29 per user, but the dismissal-tracking
            // semantic was preserved to keep one-time intro quests one-time.
            JunimoStashCapPatch._meta?.DismissedIndicators.Add("tly.stash");

            if (Game1.activeClickableMenu is ItemGrabMenu igm)
                StripColorPicker(igm);
        }

        /// <summary>Strip the color picker UI from a stash menu — toggle button + swatch row
        /// nulled, right-column buttons shifted down to fill the toggle's old slot, fillStacks'
        /// stale upNeighborID cleared. Idempotent: safe to call repeatedly on the same menu.
        /// </summary>
        internal static void StripColorPicker(ItemGrabMenu igm)
        {
            // Shift the right column down so the empty slot ends up at the top of the column,
            // abutting the inventory grid (reads as intentional spacing rather than a gap).
            // Vanilla column top→bottom: organize, fillStacks, colorPickerToggle.
            if (igm.colorPickerToggleButton != null
                && igm.fillStacksButton != null
                && igm.organizeButton != null)
            {
                int toggleY = igm.colorPickerToggleButton.bounds.Y;
                int fillY   = igm.fillStacksButton.bounds.Y;
                igm.fillStacksButton.bounds.Y = toggleY;
                igm.organizeButton.bounds.Y   = fillY;
            }

            // Kill the picker components.
            igm.chestColorPicker         = null;
            igm.colorPickerToggleButton  = null;
            igm.discreteColorPickerCC    = null;
            Game1.player.showChestColorPicker = false;

            // Clear the stale upNeighborID on fillStacks — vanilla wired it to 27346 (the toggle
            // ID) at construction. Now that the toggle is null, controller-up from fillStacks
            // would target a phantom and could (depending on the controller-nav fallback)
            // re-acquire showChestColorPicker behaviour or visually flash on the dead spot.
            if (igm.fillStacksButton != null)
                igm.fillStacksButton.upNeighborID = -500;
        }
    }

    /// <summary>
    /// Round-13 belt-and-suspenders for the picker-reappears-on-item-add bug. On every
    /// ItemGrabMenu update tick, if the menu's sourceItem is our stash chest AND any color
    /// picker component is still present, strip it again. Cheap (one ref check + three null
    /// comparisons per tick when active), idempotent, and catches any code path that
    /// recreates the picker after our initial strip.
    /// </summary>
    [HarmonyPatch(typeof(ItemGrabMenu), nameof(ItemGrabMenu.update),
        new System.Type[] { typeof(Microsoft.Xna.Framework.GameTime) })]
    internal static class JunimoStashColorPickerScrubPatch
    {
        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static void Postfix(ItemGrabMenu __instance)
        {
            if (__instance == null) return;
            // Fast-path: nothing to strip means nothing to do.
            if (__instance.chestColorPicker == null
                && __instance.colorPickerToggleButton == null
                && __instance.discreteColorPickerCC == null)
                return;
            if (!(__instance.sourceItem is Chest chest)) return;
            if (!chest.modData.ContainsKey(JunimoStashService.StashModDataKey)) return;

            JunimoStashShowMenuPatch.StripColorPicker(__instance);
        }
    }

    /// <summary>
    /// Round-14 fix for the 1-frame picker flash on item add/remove. The update-tick scrub
    /// runs every frame but vanilla's frame order is update → draw, so any recreation that
    /// happens BETWEEN ticks (e.g. inside Chest.grabItemFromInventory's mid-frame ShowMenu
    /// call) gets one draw frame to flash the picker before the next update scrubs it.
    /// Prefixing draw runs synchronously immediately before the menu renders — guaranteed
    /// stripped state at every render, no flash possible. Adds one cast + three null checks
    /// per draw call when active; the redundant work with the update scrub is intentional
    /// (defense in depth).
    /// </summary>
    [HarmonyPatch(typeof(ItemGrabMenu), nameof(ItemGrabMenu.draw),
        new System.Type[] { typeof(Microsoft.Xna.Framework.Graphics.SpriteBatch) })]
    internal static class JunimoStashColorPickerDrawGuardPatch
    {
        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static void Prefix(ItemGrabMenu __instance)
        {
            if (__instance == null) return;
            if (__instance.chestColorPicker == null
                && __instance.colorPickerToggleButton == null
                && __instance.discreteColorPickerCC == null)
                return;
            if (!(__instance.sourceItem is Chest chest)) return;
            if (!chest.modData.ContainsKey(JunimoStashService.StashModDataKey)) return;

            JunimoStashShowMenuPatch.StripColorPicker(__instance);
        }
    }

    /// <summary>
    /// Caps the stash chest's <see cref="Chest.GetActualCapacity"/> at the current
    /// <see cref="MetaState.StashSlotCount"/> (4 base + 4 per stash_1/2/3 upgrade owned). This is
    /// what controls how many slots ItemGrabMenu draws on open — a non-upgraded stash shows 4
    /// slots, fully upgraded shows 16. Without this patch the chest opens with the full vanilla
    /// 36-slot grid (the JunimoStashCapPatch still REJECTS over-cap deposits, but the UI lies
    /// about the available space).
    /// </summary>
    [HarmonyPatch(typeof(Chest), nameof(Chest.GetActualCapacity))]
    internal static class JunimoStashCapacityPatch
    {
        private static MetaState _meta;

        public static void Connect(MetaState meta) => _meta = meta;

        // Priority.Last: among postfixes, lower priority runs LATER, so ours writes __result
        // last and wins. Without this, another mod's capacity postfix can override the stash
        // cap depending on mod load order — VeggieGirl43 (Reddit, 2026-06-06) saw the stash
        // open as a 70-slot grid with Better Chests installed while only 4 slots actually
        // accepted items (the addItem cap patch still enforced the real limit). Fix-our-mod
        // rule: we pin our answer for OUR chest only; every other chest is untouched.
        [HarmonyPriority(Priority.Last)]
        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static void Postfix(Chest __instance, ref int __result)
        {
            // 2026-05-29 round 10 diagnostic: trace WHY the cap might not be applying. User
            // reported "stash shows 36 slots" after the round-9 sweep-order fix — narrowing
            // the cause requires seeing whether the gate is failing because modData is missing,
            // _meta is null, or cap = 0.
            bool hasTag = __instance?.modData?.ContainsKey(JunimoStashService.StashModDataKey) ?? false;
            int probedCap = _meta?.StashSlotCount ?? -1;
            PatchLog.Trace(
                $"GetActualCapacity postfix: tile=({__instance?.TileLocation.X}, {__instance?.TileLocation.Y}), " +
                $"qid='{__instance?.QualifiedItemId}', hasStashTag={hasTag}, metaWired={_meta != null}, " +
                $"probedCap={probedCap}, vanillaResult={__result}.");

            if (_meta == null) return;
            if (!__instance.modData.ContainsKey(JunimoStashService.StashModDataKey)) return;

            int cap = _meta.StashSlotCount;
            if (cap > 0)
                __result = cap;
        }
    }

    /// <summary>
    /// Opens the stash's ItemGrabMenu with a NULL <c>context</c> so storage-overhaul mods leave
    /// its layout alone. Better Chests and Unlimited Storage both TRANSPILE the ItemGrabMenu
    /// constructor: after vanilla computes the capacity, their inserted helper post-processes it
    /// using the ctor's <c>context</c> argument (`context as Chest` → their per-chest option →
    /// 36/70-slot grid), so no Harmony postfix priority can win. Vanilla itself keys the layout
    /// on <c>sourceItem</c>, NOT context (ItemGrabMenu.cs:316-318), and Chest.ShowMenu passes
    /// the chest as BOTH — so nulling context for OUR tagged chest keeps the vanilla small-chest
    /// geometry while both helpers fall through their Chest checks. Vanilla uses context beyond
    /// layout only for cases that never apply to a plain chest (JunimoHut button, JunimoNoteMenu
    /// reward grab, CC/museum drag checks). A prefix runs before the (transpiled) ctor body.
    /// Verified live 2026-07-10 vs Unlimited Storage 1.2.0 with BigChestMenu=true.
    /// </summary>
    [HarmonyPatch]
    internal static class JunimoStashMenuContextPatch
    {
        // Match every ItemGrabMenu ctor overload that has both a sourceItem and an object
        // context parameter (PC and Android overloads differ in arity — match by name, not
        // by exact signature).
        private static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            foreach (var ctor in typeof(ItemGrabMenu).GetConstructors())
            {
                var ps = ctor.GetParameters();
                bool hasContext = false, hasSource = false;
                foreach (var p in ps)
                {
                    if (p.Name == "context" && p.ParameterType == typeof(object)) hasContext = true;
                    if (p.Name == "sourceItem") hasSource = true;
                }
                if (hasContext && hasSource)
                    yield return ctor;
            }
        }

        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static void Prefix(Item sourceItem, ref object context)
        {
            if (sourceItem is Chest chest
                && chest.modData?.ContainsKey(JunimoStashService.StashModDataKey) == true)
            {
                context = null;
            }
        }
    }

    /// <summary>
    /// Pins the stash chest's <see cref="Chest.SpecialChestType"/> to <c>None</c> so the
    /// ItemGrabMenu lays it out as a plain small chest. Storage mods force the big-chest MENU
    /// through this getter rather than through capacity — Unlimited Storage 1.2.0 (LeFauxMatt,
    /// verified live 2026-07-10 with BigChestMenu=true) postfixes it to BigChest for every
    /// enabled item ID, and its enable list is per item ID ("130" = ALL regular chests), so
    /// there is no per-instance opt-out on its side (unlike Better Chests' modData options).
    /// Same shape as <see cref="JunimoStashCapacityPatch"/>: Priority.Last so our answer wins,
    /// gated on OUR modData tag so every other chest is left to the storage mod.
    /// </summary>
    [HarmonyPatch(typeof(Chest), nameof(Chest.SpecialChestType), MethodType.Getter)]
    internal static class JunimoStashSpecialChestTypePatch
    {
        [HarmonyPriority(Priority.Last)]
        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static void Postfix(Chest __instance, ref Chest.SpecialChestTypes __result)
        {
            if (__instance?.modData?.ContainsKey(JunimoStashService.StashModDataKey) != true) return;
            __result = Chest.SpecialChestTypes.None;
        }
    }

    /// <summary>
    /// Prevents the player from picking up the stash chest with a pickaxe or axe. The stash is
    /// part of the run loop (placed on every save load, populated from MetaState) — letting the
    /// player pick it up would put a regular-chest copy in their inventory that wouldn't sync
    /// with the meta layer + would leak the modData tag onto a portable chest. Returning false
    /// from performToolAction matches the vanilla semantics for "the tool didn't break this."
    /// </summary>
    [HarmonyPatch(typeof(Chest), nameof(Chest.performToolAction))]
    internal static class JunimoStashImmovablePatch
    {
        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static bool Prefix(Chest __instance, ref bool __result)
        {
            if (!__instance.modData.ContainsKey(JunimoStashService.StashModDataKey))
                return true; // run original

            __result = false;
            return false; // skip original
        }
    }
}
