using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Menus;
using TheLongestYear.Core;

namespace TheLongestYear.Patches
{
    /// <summary>
    /// Weapon/hat ((W)/(H)) Community Center donation cluster. Vanilla's CC donation UI is
    /// hard-wired for <c>(O)</c> Objects in two independent places:
    ///
    ///   - <c>JunimoNoteMenu</c>'s inventory is built with <c>Utility.highlightSmallObjects</c>
    ///     as its <c>highlightMethod</c> (JunimoNoteMenu.cs ~232/410); that method returns false
    ///     for anything that isn't an <c>Object</c> (Utility.cs:1365-1372), and
    ///     <c>InventoryMenu</c> refuses to let the player pick up / drag any item that fails
    ///     <c>highlightMethod</c> (InventoryMenu.cs:905) — so a weapon or hat can never even be
    ///     lifted out of the player's inventory while the note menu is open.
    ///   - <c>JunimoNoteMenu.setUpBundleSpecificPage(Bundle b)</c> only creates an ingredient
    ///     icon (the little picture above the donation slot) for ingredients whose
    ///     representative item id resolves to <c>HasTypeObject() == true</c>
    ///     (JunimoNoteMenu.cs ~1491) — a non-Object ingredient (e.g. Gil's Trophies' Rusty
    ///     Sword/Trilby Hat slots) is silently skipped, leaving no icon at all.
    ///
    /// Neither gate reflects an actual matching/deposit limitation: <c>Bundle.
    /// IsValidItemForThisIngredientDescription</c>'s id-branch is <c>ItemRegistry.HasItemId</c>
    /// (type-agnostic, Bundle.cs:245) and <c>tryToDepositThisItem</c> never casts to Object. Once
    /// <see cref="TheLongestYear.Core.GameplayConfig.EnableNonObjectDonations"/> lets the bundle
    /// generator put (W)/(H) items into a bundle (e.g. Gil's Trophies), these two patches are what
    /// let the player actually SEE and DONATE them:
    ///
    ///   1. <see cref="HighlightWrapperPatch"/> — wraps <c>inventory.highlightMethod</c> so a
    ///      (W)/(H) item that matches an incomplete ingredient on the open bundle also highlights
    ///      (and therefore becomes pickable).
    ///   2. <see cref="IconGatePatch"/> — postfixes <c>setUpBundleSpecificPage</c> to add the
    ///      missing ingredient icon for any non-Object ingredient vanilla skipped.
    ///
    /// Both patches are inert unless <see cref="RunActivation.IsActive"/> AND
    /// <see cref="GameplayConfig.EnableNonObjectDonations"/> are both true (<see cref="Enabled"/>)
    /// — on a non-TLY save, or with the kill-switch off, vanilla behavior is untouched.
    /// </summary>
    internal static class BundleDonationPatches
    {
        private const string ObjectIdPrefix = "(O)";

        private static IMonitor _monitor;
        private static GameplayConfig _config;

        /// <summary>Wire the static monitor/config this patch cluster reads. Called once from
        /// <c>ModEntry.Entry</c> — <see cref="GameplayConfig"/> is a single stable instance for
        /// the whole session (unlike per-save state), so there's no need to re-wire per save
        /// load; the <see cref="RunActivation"/> gate already keeps the patches dormant on
        /// non-TLY saves regardless of when Connect ran.</summary>
        public static void Connect(IMonitor monitor, GameplayConfig config)
        {
            _monitor = monitor;
            _config = config;
        }

        /// <summary>Kill-switch + per-save gate. Both patches no-op entirely when this is false.</summary>
        private static bool Enabled => RunActivation.IsActive && _config != null && _config.EnableNonObjectDonations;

        private static void Warn(string message) => _monitor?.Log($"BundleDonationPatches: {message}", LogLevel.Warn);

        // Bypasses whatever the field's real accessibility is (the brief's decompile ground
        // truth says public; the Android decompile checked during implementation shows it
        // private — platforms are known to differ here, see workspace CLAUDE.md). FieldRefAccess
        // works either way, so this sidesteps the discrepancy instead of gambling on it.
        private static readonly AccessTools.FieldRef<JunimoNoteMenu, Bundle> _currentPageBundleRef =
            AccessTools.FieldRefAccess<JunimoNoteMenu, Bundle>("currentPageBundle");

        /// <summary>True if <paramref name="item"/> matches any incomplete non-Object ingredient
        /// on the menu's currently open bundle page, or — if no page is open (board overview) —
        /// any incomplete non-Object ingredient on any bundle in this menu's <c>bundles</c> list.
        /// </summary>
        private static bool ItemMatchesAnyNonObjectIngredient(JunimoNoteMenu menu, Item item)
        {
            if (menu == null || item == null)
                return false;

            Bundle current = _currentPageBundleRef(menu);
            if (current?.ingredients != null)
                return AnyIngredientMatches(current.ingredients, item);

            if (menu.bundles != null)
            {
                foreach (Bundle bundle in menu.bundles)
                {
                    if (bundle?.ingredients != null && AnyIngredientMatches(bundle.ingredients, item))
                        return true;
                }
            }
            return false;
        }

        private static bool AnyIngredientMatches(List<BundleIngredientDescription> ingredients, Item item)
        {
            foreach (BundleIngredientDescription ing in ingredients)
            {
                if (ing.completed)
                    continue;
                if (string.IsNullOrEmpty(ing.id))
                    continue; // category-based ingredient (id is null) — out of scope, vanilla objects only
                if (ing.id.StartsWith(ObjectIdPrefix, StringComparison.Ordinal))
                    continue; // vanilla's own highlightSmallObjects already covers Objects
                if (ItemRegistry.HasItemId(item, ing.id))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Postfixes every public <see cref="JunimoNoteMenu"/> constructor (the "fromGameMenu"/
        /// "whichArea" board ctors and the single-bundle ctor all independently build an
        /// <c>inventory</c> with <c>highlightMethod = Utility.highlightSmallObjects</c> —
        /// JunimoNoteMenu.cs ~232/410; patching all of them is cheap and keeps this correct
        /// regardless of which path a given CC context uses) and replaces
        /// <c>inventory.highlightMethod</c> with a wrapper that additionally accepts (W)/(H)
        /// items matching an open bundle's ingredients. The wrapper defers to the ORIGINAL
        /// delegate first (<c>original(item) || …</c>), so every vanilla highlight case
        /// (Objects) is unchanged. The wrapper re-checks <see cref="Enabled"/> on each invocation,
        /// closing the window where a mid-menu config toggle could leave stale highlighting active.
        /// </summary>
        [HarmonyPatch(typeof(JunimoNoteMenu))]
        internal static class HighlightWrapperPatch
        {
            // Marks InventoryMenu instances whose highlightMethod we've already wrapped, so a
            // second postfix firing for the same instance (defensive — no known path re-runs a
            // ctor on an existing instance) can't double-wrap it. Weak table: never keeps a menu
            // alive past its own lifetime.
            private static readonly ConditionalWeakTable<InventoryMenu, object> _wrapped = new();

            private static IEnumerable<MethodBase> TargetMethods()
            {
                foreach (ConstructorInfo ctor in typeof(JunimoNoteMenu).GetConstructors())
                    yield return ctor;
            }

            // ReSharper disable once InconsistentNaming — Harmony convention.
            // ReSharper disable once UnusedMember.Local — discovered by the manual PatchClassProcessor scan.
            private static void Postfix(JunimoNoteMenu __instance)
            {
                if (!Enabled)
                    return;

                try
                {
                    InventoryMenu inv = __instance?.inventory;
                    if (inv == null)
                        return;
                    if (_wrapped.TryGetValue(inv, out _))
                        return; // already our wrapper — never mutate twice

                    InventoryMenu.highlightThisItem original = inv.highlightMethod;
                    if (original == null)
                        return;

                    InventoryMenu.highlightThisItem wrapper = candidate =>
                        original(candidate) || (Enabled && ItemMatchesAnyNonObjectIngredient(__instance, candidate));
                    inv.highlightMethod = wrapper;
                    _wrapped.Add(inv, null);
                }
                catch (Exception ex)
                {
                    Warn($"highlight wrapper postfix failed: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Postfixes <c>JunimoNoteMenu.setUpBundleSpecificPage(Bundle b)</c>: for each ingredient
        /// whose representative id (<c>GetRepresentativeItemId</c>) is non-Object and has no
        /// corresponding entry in <c>ingredientList</c> after the original ran (vanilla's
        /// <c>HasTypeObject()</c> gate, JunimoNoteMenu.cs ~1491, skipped it), builds the same
        /// <c>ClickableTextureComponent</c> vanilla builds for an Object ingredient and inserts
        /// it at the ingredient's ordinal position (by <c>myID</c>, matching vanilla's
        /// <c>i + 1000</c> convention).
        /// </summary>
        [HarmonyPatch(typeof(JunimoNoteMenu), "setUpBundleSpecificPage")]
        internal static class IconGatePatch
        {
            // Vanilla's createRowOfBoxesCenteredAt uses boxWidth=boxHeight=72, horizontalGap=12
            // (JunimoNoteMenu.cs ~1556-1605) — used here only as a LAST-RESORT default when no
            // neighboring icon exists to copy real bounds from.
            private const int DefaultIconSize = 72;
            private const int DefaultGap = 12;
            private const int IngredientListFirstMyId = 1000;

            // ReSharper disable once InconsistentNaming — Harmony convention.
            // ReSharper disable once UnusedMember.Local — discovered by the manual PatchClassProcessor scan.
            private static void Postfix(JunimoNoteMenu __instance, Bundle b)
            {
                if (!Enabled)
                    return;
                if (__instance?.ingredientList == null || b?.ingredients == null)
                    return;

                try
                {
                    List<ClickableTextureComponent> list = __instance.ingredientList;

                    var presentOrdinals = new HashSet<int>();
                    foreach (ClickableTextureComponent c in list)
                        if (c != null && c.myID >= IngredientListFirstMyId)
                            presentOrdinals.Add(c.myID - IngredientListFirstMyId);

                    for (int i = 0; i < b.ingredients.Count; i++)
                    {
                        if (presentOrdinals.Contains(i))
                            continue;

                        BundleIngredientDescription ing = b.ingredients[i];
                        string repId = JunimoNoteMenu.GetRepresentativeItemId(ing);
                        if (string.IsNullOrEmpty(repId) || repId.StartsWith(ObjectIdPrefix, StringComparison.Ordinal))
                            continue; // Object (or unresolved category) — vanilla already added it, or it's out of scope

                        ParsedItemData data = ItemRegistry.GetDataOrErrorItem(repId);
                        if (data == null)
                            continue;
                        // Real-assembly equivalent of the decompile's ParsedItemData.HasTypeObject()
                        // (that method doesn't exist on the actual game build compiled against —
                        // verified via reflection against the installed Stardew Valley.dll). A
                        // representative id from GetRepresentativeItemId is always either an
                        // already-qualified id (category branch resolves to a QualifiedItemId,
                        // id branch returns the ingredient's own id, which this codebase's bundle
                        // data always authors qualified) or the type prefix on it directly reflects
                        // GetItemTypeId(); presentOrdinals above already caught every id vanilla
                        // resolved as an Object regardless of format, so this is a defensive
                        // backstop, not the primary correctness gate.
                        if (data.GetItemTypeId() == ItemRegistry.type_object)
                            continue; // vanilla's own gate would have included this — nothing missing

                        Item item = ItemRegistry.Create(repId, ing.stack, ing.quality);
                        Rectangle bounds = ComputeBounds(__instance, list, i);
                        var component = new ClickableTextureComponent(
                            "ingredient_list_slot", bounds, "", item.DisplayName, data.GetTexture(), data.GetSourceRect(), 4f)
                        {
                            myID = i + IngredientListFirstMyId,
                            item = item,
                            upNeighborID = -99998,
                            rightNeighborID = -99998,
                            leftNeighborID = -99998,
                            downNeighborID = -99998,
                        };

                        int insertAt = 0;
                        while (insertAt < list.Count && list[insertAt].myID < component.myID)
                            insertAt++;
                        list.Insert(insertAt, component);
                    }
                }
                catch (Exception ex)
                {
                    Warn($"icon-gate postfix failed for bundle '{b?.name}': {ex.GetType().Name}: {ex.Message}. Leaving vanilla ingredientList as-is.");
                }
            }

            /// <summary>
            /// Vanilla computes ingredient-icon rectangles from a LOCAL list built by
            /// <c>addRectangleRowsToList</c>/<c>createRowOfBoxesCenteredAt</c>
            /// (JunimoNoteMenu.cs ~1481-1605), which isn't reachable from a postfix (it depends
            /// on several private fields). Instead of reimplementing that layout, this
            /// approximates by copying bounds from whichever REAL (vanilla-built) icon(s) sit
            /// nearest this ordinal position:
            ///   - both neighbors present → linearly interpolate X/Y between them (exact within a
            ///     row; an approximation across a row wrap).
            ///   - one neighbor present → step away from it by its own width + the vanilla gap
            ///     constant (assumes same row — a bundle whose missing icon falls at a row
            ///     boundary will look slightly off).
            ///   - no icons at all (an ALL-non-Object bundle, e.g. a rings-only trophy case
            ///     wouldn't hit this, but a hypothetical all-weapon bundle would) → anchor off
            ///     <c>ingredientSlots[0]</c> (always populated — built from
            ///     <c>numberOfIngredientSlots</c>, independent of ingredient type) one row above
            ///     it, laid out left-to-right; if even that's empty, anchor off the menu's own
            ///     <c>xPositionOnScreen</c>/<c>yPositionOnScreen</c>. LIMITATION: this fallback
            ///     is a single row only and won't reproduce vanilla's multi-row wrap for bundles
            ///     with more than ~4-6 all-non-Object ingredients.
            /// </summary>
            private static Rectangle ComputeBounds(JunimoNoteMenu menu, List<ClickableTextureComponent> list, int ordinalIndex)
            {
                ClickableTextureComponent prev = null, next = null;
                int prevIdx = -1, nextIdx = int.MaxValue;
                foreach (ClickableTextureComponent c in list)
                {
                    if (c == null)
                        continue;
                    int idx = c.myID - IngredientListFirstMyId;
                    if (idx < ordinalIndex && idx > prevIdx) { prev = c; prevIdx = idx; }
                    if (idx > ordinalIndex && idx < nextIdx) { next = c; nextIdx = idx; }
                }

                int w = prev?.bounds.Width ?? next?.bounds.Width ?? DefaultIconSize;
                int h = prev?.bounds.Height ?? next?.bounds.Height ?? DefaultIconSize;

                if (prev != null && next != null)
                {
                    double t = (double)(ordinalIndex - prevIdx) / (nextIdx - prevIdx);
                    int x = prev.bounds.X + (int)Math.Round((next.bounds.X - prev.bounds.X) * t);
                    int y = prev.bounds.Y + (int)Math.Round((next.bounds.Y - prev.bounds.Y) * t);
                    return new Rectangle(x, y, w, h);
                }
                if (prev != null)
                {
                    int step = prev.bounds.Width + DefaultGap;
                    return new Rectangle(prev.bounds.X + step * (ordinalIndex - prevIdx), prev.bounds.Y, w, h);
                }
                if (next != null)
                {
                    int step = next.bounds.Width + DefaultGap;
                    return new Rectangle(next.bounds.X - step * (nextIdx - ordinalIndex), next.bounds.Y, w, h);
                }

                if (menu.ingredientSlots != null && menu.ingredientSlots.Count > 0)
                {
                    Rectangle anchor = menu.ingredientSlots[0].bounds;
                    int step = w + DefaultGap;
                    return new Rectangle(anchor.X + step * ordinalIndex, anchor.Y - h - 96, w, h);
                }

                int baseX = menu.xPositionOnScreen + 200;
                int baseY = menu.yPositionOnScreen + 200;
                return new Rectangle(baseX + (w + DefaultGap) * ordinalIndex, baseY, w, h);
            }
        }
    }
}
