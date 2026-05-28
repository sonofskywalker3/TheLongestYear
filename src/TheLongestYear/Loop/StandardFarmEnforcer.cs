using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Forces every new TLY game onto the Standard farm (Game1.whichFarm == 0). TLY's tile
    /// defaults, kept-building placement coords, and stash auto-pick all assume Standard farm
    /// geometry — running on Riverland / Forest / Hilltop / etc. lands buildings in water or
    /// leaves the stash chest invisible. Rather than bailing at save-load (which leaves the
    /// player with a half-broken save and no idea why), we override the farm choice during
    /// character creation so a Standard farm is the only outcome.
    ///
    /// Implementation:
    ///   - Every UpdateTicked while <see cref="CharacterCustomization"/> is the active menu,
    ///     reset Game1.whichFarm to 0. The CC preview snaps back to Standard on any click,
    ///     making it obvious that other farm types are disallowed (a more discoverable signal
    ///     than a silent override on save creation alone).
    ///   - On <c>GameLoop.SaveCreating</c> (just before the save folder is written), force
    ///     whichFarm = 0 as a belt-and-braces guard — covers any edge case where the per-tick
    ///     reset missed a frame and the player committed a non-Standard choice.
    ///
    /// Disabled when <see cref="GameplayConfig.Enabled"/> is false so the player can run the
    /// game with TLY off and pick any farm type they want.
    /// </summary>
    internal sealed class StandardFarmEnforcer
    {
        private readonly IMonitor _monitor;
        private readonly TheLongestYear.Core.GameplayConfig _config;

        /// <summary>True once we have logged the "forcing Standard farm" notice for the current
        /// CharacterCustomization session. Resets when the menu closes so a reopen relogs.</summary>
        private bool _loggedThisSession;

        public StandardFarmEnforcer(IMonitor monitor, TheLongestYear.Core.GameplayConfig config)
        {
            _monitor = monitor;
            _config = config;
        }

        public void Attach(IModHelper helper)
        {
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.GameLoop.SaveCreating += OnSaveCreating;
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!_config.Enabled)
                return;

            bool inCharCustom = Game1.activeClickableMenu is CharacterCustomization;
            if (!inCharCustom)
            {
                _loggedThisSession = false;
                return;
            }

            // Only enforce during the title-screen new-game flow. CharacterCustomization is
            // ALSO opened mid-game by the Dresser (clothing dye) and the Wizard (sex/appearance
            // change) — both of those run with a save loaded, and resetting whichFarm mid-session
            // would corrupt the loaded farm's spawn logic + tile properties. Context.IsWorldReady
            // is the cleanest "are we in a loaded save?" signal SMAPI exposes.
            if (Context.IsWorldReady)
                return;

            if (Game1.whichFarm != 0)
            {
                Game1.whichFarm = 0;
                if (!_loggedThisSession)
                {
                    _monitor.Log(
                        "StandardFarmEnforcer: snapped farm selection back to Standard. " +
                        "TLY only supports the Standard farm; disable TLY in GMCM (or config.json) " +
                        "if you want to pick a different farm type.",
                        LogLevel.Info);
                    _loggedThisSession = true;
                }
            }
        }

        private void OnSaveCreating(object sender, SaveCreatingEventArgs e)
        {
            if (!_config.Enabled)
                return;

            if (Game1.whichFarm != 0)
            {
                _monitor.Log(
                    $"StandardFarmEnforcer: overriding farm type {Game1.whichFarm} -> 0 (Standard) " +
                    "before save creation.",
                    LogLevel.Warn);
                Game1.whichFarm = 0;
            }
        }
    }
}
