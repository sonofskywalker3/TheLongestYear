using System;
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
    /// </summary>
    internal sealed class DifficultyLever
    {
        public const string FieldId = "TheLongestYear.Difficulty.Overall";
        private const string GmcmNamespace = "GenericModConfigMenu";

        private readonly IGenericModConfigMenuApi _gmcm;
        private readonly IManifest _manifest;
        private readonly IMonitor _monitor;
        private bool _reopenNextTick;

        /// <summary>The level picked on the open page but not saved yet. Null when nothing is pending.</summary>
        public DifficultyStep? Pending { get; private set; }

        public DifficultyLever(IModHelper helper, IGenericModConfigMenuApi gmcm, IManifest manifest, IMonitor monitor)
        {
            _gmcm = gmcm;
            _manifest = manifest;
            _monitor = monitor;
            gmcm.OnFieldChanged(manifest, OnFieldChanged);
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
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
                // the menu while its dropdown is still handling the click.
                _reopenNextTick = false;
                try
                {
                    _gmcm.OpenModMenu(_manifest);
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
