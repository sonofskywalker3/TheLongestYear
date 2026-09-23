using System;
using System.Linq;
using System.Reflection;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using TheLongestYear.Core;

namespace TheLongestYear.Integration
{
    /// <summary>
    /// GMCM glue for the overall Difficulty lever (Jeff, 2026-09-14). GMCM keeps each field's shown
    /// value until Save and then writes every field back in order, so a lever that only wrote the
    /// config would be overwritten by the ten dials' stale values a moment later. Instead a lever
    /// change is held as <see cref="Pending"/>, the page is re-opened on the next tick (every
    /// getValue is re-read, so the ten dials visibly jump to the level), and Save then writes what
    /// the player sees. Pending is dropped on Save, on Reset, and when the menu closes, so Cancel
    /// still discards it.
    ///
    /// The re-open waits for the dropdown to close. GMCM's dropdown commits whichever row the
    /// mouse is OVER while its list is open, firing the change on hover, not on click. Re-opening on
    /// that first change rebuilt the page mid-hover: the list opens with Easy under the cursor, so
    /// the lever could only ever step between Easy and Normal and never reach Hard or Extreme
    /// (goblinslayer66666, Nexus bug 1138128, 0.18.38). The refresh also keeps the page's scroll
    /// position; a fresh page starts at the top, a long way above the Difficulty section.
    /// </summary>
    internal sealed class DifficultyLever
    {
        public const string FieldId = "TheLongestYear.Difficulty.Overall";
        private const string GmcmNamespace = "GenericModConfigMenu";
        private const string GmcmAssemblyName = "GenericModConfigMenu";
        private const string GmcmDropdownTypeName = "SpaceShared.UI.Dropdown";
        private const string ActiveDropdownFieldName = "ActiveDropdown";
        private const string SpecificModConfigMenuTypeName = "SpecificModConfigMenu";
        private const string TableFieldName = "Table";
        private const string ScrollbarPropertyName = "Scrollbar";
        private const string TopRowPropertyName = "TopRow";
        private const string MaxTopRowPropertyName = "MaxTopRow";
        private const BindingFlags InstanceMembers = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        private readonly IGenericModConfigMenuApi _gmcm;
        private readonly IManifest _manifest;
        private readonly IMonitor _monitor;
        private bool _reopenNextTick;
        private FieldInfo _activeDropdownField;
        private bool _activeDropdownLookedUp;

        /// <summary>The level picked on the open page but not saved yet. Null when nothing is pending.</summary>
        public DifficultyStep? Pending { get; private set; }

        public DifficultyLever(IModHelper helper, IGenericModConfigMenuApi gmcm, IManifest manifest, IMonitor monitor)
        {
            _gmcm = gmcm;
            _manifest = manifest;
            _monitor = monitor;
            gmcm.OnFieldChanged(manifest, OnFieldChanged);
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            IsGmcmDropdownOpen(); // resolve (and log) GMCM's dropdown state once at launch
        }

        /// <summary>Drop any unsaved lever pick (Save and Reset call this).</summary>
        public void Clear()
        {
            Pending = null;
            _reopenNextTick = false;
        }

        private void OnFieldChanged(string fieldId, object value)
        {
            if (fieldId != FieldId)
                return;
            Pending = DifficultySteps.Parse(value as string);
            _reopenNextTick = true;
            _monitor.Log($"Difficulty lever: {Pending} picked; refreshing the page so every option below shows it.", LogLevel.Trace);
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (_reopenNextTick)
            {
                // Deferred a tick: re-opening from inside GMCM's own change callback would replace
                // the menu while its dropdown is still handling the click. Held further while the
                // list is still open, because hovering a row already counts as a change.
                if (IsGmcmDropdownOpen())
                    return;
                _reopenNextTick = false;
                try
                {
                    int? topRow = GetPageTopRow();
                    _gmcm.OpenModMenu(_manifest);
                    if (topRow.HasValue)
                        SetPageTopRow(topRow.Value);
                }
                catch (Exception ex) when (ex is InvalidOperationException or NullReferenceException)
                {
                    _monitor.Log($"Difficulty lever: could not refresh the config page ({ex.Message}). Close and reopen it to see the new levels.", LogLevel.Warn);
                }
                return;
            }

            if (Pending.HasValue && !IsConfigMenuOpen())
                Pending = null;
        }

        /// <summary>True while any GMCM dropdown has its list open (GMCM's static
        /// <c>Dropdown.ActiveDropdown</c>, cleared on the same update the list closes). If GMCM's
        /// internals ever move, this reports closed and the lever re-opens at once, as it did
        /// before this check existed.</summary>
        private bool IsGmcmDropdownOpen()
        {
            if (!_activeDropdownLookedUp)
            {
                _activeDropdownLookedUp = true;
                _activeDropdownField = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == GmcmAssemblyName)
                    ?.GetType(GmcmDropdownTypeName)
                    ?.GetField(ActiveDropdownFieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (_activeDropdownField == null)
                    _monitor.Log("Difficulty lever: GMCM's dropdown state was not found; the page will refresh on the first hovered level.", LogLevel.Warn);
                else
                    _monitor.Log("Difficulty lever: watching GMCM's dropdown state; the page refreshes once the list closes.", LogLevel.Trace);
            }
            return _activeDropdownField?.GetValue(null) != null;
        }

        /// <summary>The open config page's scroll position (GMCM's private <c>Table</c> field, then
        /// <c>Table.Scrollbar.TopRow</c>), so the refresh can land where the player was instead of
        /// at the top of the page. Null when the page or GMCM's internals are not found.</summary>
        private static int? GetPageTopRow()
        {
            object scrollbar = GetPageScrollbar();
            return scrollbar?.GetType().GetProperty(TopRowPropertyName, InstanceMembers)?.GetValue(scrollbar) as int?;
        }

        /// <summary>Scrolls the freshly opened page to <paramref name="row"/>, clamped to its last
        /// row. Sets <c>TopRow</c> directly: GMCM's own <c>ScrollTo</c> plays a sound.</summary>
        private void SetPageTopRow(int row)
        {
            object scrollbar = GetPageScrollbar();
            Type type = scrollbar?.GetType();
            PropertyInfo topRow = type?.GetProperty(TopRowPropertyName, InstanceMembers);
            if (topRow?.GetValue(scrollbar) is not int || type.GetProperty(MaxTopRowPropertyName, InstanceMembers)?.GetValue(scrollbar) is not int maxTopRow)
            {
                _monitor.Log("Difficulty lever: could not keep the page's scroll position; GMCM's page layout was not found.", LogLevel.Trace);
                return;
            }
            topRow.SetValue(scrollbar, Math.Clamp(row, 0, maxTopRow));
        }

        private static object GetPageScrollbar()
        {
            IClickableMenu page = Game1.activeClickableMenu is TitleMenu ? TitleMenu.subMenu : Game1.activeClickableMenu;
            if (page?.GetType().Name != SpecificModConfigMenuTypeName)
                return null;
            object table = page.GetType().GetField(TableFieldName, InstanceMembers)?.GetValue(page);
            return table?.GetType().GetProperty(ScrollbarPropertyName, InstanceMembers)?.GetValue(table);
        }

        private static bool IsConfigMenuOpen()
            => IsGmcm(Game1.activeClickableMenu) || IsGmcm(TitleMenu.subMenu);

        private static bool IsGmcm(IClickableMenu menu)
        {
            for (IClickableMenu m = menu; m != null; m = m.GetChildMenu())
                if (m.GetType().Namespace?.StartsWith(GmcmNamespace, StringComparison.Ordinal) == true)
                    return true;
            return false;
        }
    }
}
