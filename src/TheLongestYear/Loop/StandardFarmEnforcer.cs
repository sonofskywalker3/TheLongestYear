using System.Collections;
using System.Reflection;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Forces every new TLY game onto the Standard farm by scrubbing the relevant options out of
    /// CharacterCustomization, and watches its "Skip intro" checkbox: ticking it pops a one-button
    /// notice recommending first-timers watch the opening. The checkbox now skips TLY's own
    /// Lewis->Junimo cutscene (see <see cref="SkipIntroChoicePatch"/>); the vanilla bus ride is
    /// always skipped. TLY's tile defaults, kept-building
    /// placement coords, and stash auto-pick all assume Standard farm geometry — running on
    /// Riverland / Forest / Hilltop / etc. lands buildings in water or leaves the stash chest
    /// invisible. Rather than bailing at save-load (the c4af752 approach, which left the player
    /// hunting for a non-existent chest with no in-game explanation), we strip every non-Standard
    /// option out of the farm-type tab of <see cref="CharacterCustomization"/> so the only
    /// outcome is a Standard farm.
    ///
    /// Implementation (reflection-driven because PC CharacterCustomization fields differ
    /// from the Android decompile, and Harmony patches against private fields are fragile
    /// across game updates):
    ///   1. On every UpdateTicked while no save is loaded (Context.IsWorldReady = false),
    ///      walk the menu chain (activeClickableMenu, plus TitleMenu.subMenu when the active
    ///      menu is TitleMenu) looking for the PC <c>CharacterCustomization</c> instance.
    ///   2. Once found (and once per CharacterCustomization session), trim the parallel
    ///      farm-type lists down to just the first entry (Standard) and reset pagination:
    ///        - farmTypeButtons      (List&lt;ClickableTextureComponent&gt;)
    ///        - farmTypeButtonNames  (List&lt;string&gt;)
    ///        - farmTypeIcons        (List&lt;Texture2D&gt;)
    ///        - farmTypeHoverText    (List&lt;string&gt;)
    ///        - _farmPages = 1, _currentFarmPage = 0
    ///   3. Snap <c>Game1.whichFarm</c> back to 0 every frame as a belt-and-braces guard
    ///      against any code path that could have committed a different value before step 2
    ///      ran (e.g. last-pressed state lingering from a prior CC session).
    ///   4. <c>GameLoop.SaveCreating</c> handler: final whichFarm = 0 force just before the
    ///      save folder is written.
    ///
    /// Disabled when <see cref="GameplayConfig.Enabled"/> is false so the player can run the
    /// game with TLY off and pick any farm type they want.
    /// </summary>
    internal sealed class StandardFarmEnforcer
    {
        private const BindingFlags FieldFlags
            = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private readonly IMonitor _monitor;
        private readonly TheLongestYear.Core.GameplayConfig _config;

        /// <summary>The CharacterCustomization instance currently scrubbed (or null). Tracked
        /// per-instance so a reopened menu (with a fresh button list) gets re-scrubbed.</summary>
        private IClickableMenu _scrubbedInstance;

        /// <summary>Last observed "Skip intro" checkbox value, so the notice fires on the off->on edge.</summary>
        private bool? _lastSkipIntro;
        private bool _warnedNoSkipField;

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

            // CharacterCustomization is ALSO opened mid-game by the Dresser and the Wizard,
            // both of which run with a save loaded. Skipping when a save is loaded keeps us out
            // of those flows.
            if (Context.IsWorldReady)
                return;

            IClickableMenu cc = FindCharacterCustomization();
            if (cc == null)
            {
                _scrubbedInstance = null;
                return;
            }

            if (!ReferenceEquals(_scrubbedInstance, cc))
            {
                ScrubFarmTypeOptions(cc);
                _scrubbedInstance = cc;
            }

            if (Game1.whichFarm != 0)
                Game1.whichFarm = 0;

            WatchSkipIntroToggle(cc);
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

        /// <summary>
        /// Locate the CharacterCustomization menu in the current menu chain. PC's new-game flow
        /// nests it inside <see cref="TitleMenu"/>.<c>subMenu</c>; the Android port also exposes
        /// it as <c>Game1.activeClickableMenu</c> directly.
        /// </summary>
        private static IClickableMenu FindCharacterCustomization()
        {
            IClickableMenu active = Game1.activeClickableMenu;
            if (active is CharacterCustomization)
                return active;

            if (active is TitleMenu)
            {
                IClickableMenu sub = TitleMenu.subMenu;
                if (sub is CharacterCustomization)
                    return sub;
            }

            return null;
        }

        /// <summary>
        /// Trim the farm-type parallel lists down to just the first entry (Standard) and reset
        /// pagination so the next/prev page buttons can't surface the removed entries.
        /// </summary>
        private void ScrubFarmTypeOptions(IClickableMenu cc)
        {
            System.Type type = cc.GetType();

            int beforeCount = TrimList(type, cc, "farmTypeButtons");
            TrimList(type, cc, "farmTypeButtonNames");
            TrimList(type, cc, "farmTypeIcons");
            TrimList(type, cc, "farmTypeHoverText");

            // Reset pagination so any UI that reads page-count to draw prev/next buttons
            // doesn't render a teaser for the removed options.
            FieldInfo pages = type.GetField("_farmPages", FieldFlags);
            FieldInfo current = type.GetField("_currentFarmPage", FieldFlags);
            pages?.SetValue(cc, 1);
            current?.SetValue(cc, 0);

            // Sanity check: if the trim didn't happen (field names changed across game versions),
            // log loudly so the next playtest's log diagnoses the failure mode.
            int afterCount = ReadListCount(type, cc, "farmTypeButtons");
            if (afterCount > 1)
            {
                _monitor.Log(
                    $"StandardFarmEnforcer: tried to trim farmTypeButtons but {afterCount} entries " +
                    "remain — field name may have changed. Per-tick whichFarm=0 reset is still active.",
                    LogLevel.Warn);
            }
            else
            {
                _monitor.Log(
                    $"StandardFarmEnforcer: scrubbed CharacterCustomization farm-type list " +
                    $"(was {beforeCount} options, now {afterCount}). Standard farm forced.",
                    LogLevel.Info);
            }

            // Ensure whichFarm is 0 immediately — a prior CC session may have left it non-zero.
            Game1.whichFarm = 0;

            _lastSkipIntro = ReadSkipIntro(type, cc);
        }

        /// <summary>Show the notice each time the checkbox goes off -> on in this menu session.
        /// The checkbox itself is untouched: vanilla toggles the field, the patch reads it on OK.</summary>
        private void WatchSkipIntroToggle(IClickableMenu cc)
        {
            bool? now = ReadSkipIntro(cc.GetType(), cc);
            if (now == null)
                return;

            if (now == true && _lastSkipIntro == false && Game1.activeClickableMenu != null
                && Game1.activeClickableMenu.GetChildMenu() == null)
            {
                _monitor.Log("StandardFarmEnforcer: Skip intro ticked; showing the watch-it-first notice.", LogLevel.Info);
                Game1.activeClickableMenu.SetChildMenu(new TheLongestYear.UI.IntroSkipNoticeMenu());
            }
            _lastSkipIntro = now;
        }

        /// <summary>The CharacterCustomization <c>skipIntro</c> field, or null when the game has
        /// renamed it (warned once; the notice is then unavailable but nothing else breaks).</summary>
        private bool? ReadSkipIntro(System.Type type, IClickableMenu cc)
        {
            FieldInfo skipFlag = type.GetField("skipIntro", FieldFlags);
            if (skipFlag == null || skipFlag.FieldType != typeof(bool))
            {
                if (!_warnedNoSkipField)
                {
                    _warnedNoSkipField = true;
                    _monitor.Log(
                        "StandardFarmEnforcer: skipIntro not found on CharacterCustomization — field name may " +
                        "have changed; the skip-intro notice is unavailable. Farm-type scrub is unaffected.",
                        LogLevel.Warn);
                }
                return null;
            }
            return (bool)skipFlag.GetValue(cc);
        }

        /// <summary>Trim a parallel List&lt;T&gt; field down to at most one entry. Returns the
        /// count BEFORE trimming (or -1 if the field doesn't exist).</summary>
        private static int TrimList(System.Type type, object instance, string fieldName)
        {
            FieldInfo field = type.GetField(fieldName, FieldFlags);
            if (field == null) return -1;

            if (field.GetValue(instance) is IList list)
            {
                int before = list.Count;
                while (list.Count > 1)
                    list.RemoveAt(list.Count - 1);
                return before;
            }
            return -1;
        }

        private static int ReadListCount(System.Type type, object instance, string fieldName)
        {
            FieldInfo field = type.GetField(fieldName, FieldFlags);
            if (field == null) return -1;
            return field.GetValue(instance) is IList list ? list.Count : -1;
        }
    }
}
