using System.Reflection;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Watches the new-game <see cref="CharacterCustomization"/> menu for the "Skip intro"
    /// checkbox: ticking it pops a one-button notice recommending first-timers watch the opening.
    /// The checkbox now skips TLY's own Lewis->Junimo cutscene (see
    /// <see cref="SkipIntroChoicePatch"/>, which reads it when the character is committed); the
    /// vanilla bus ride is always skipped.
    ///
    /// History: until 0.17.3 this class (as StandardFarmEnforcer) also stripped every farm type
    /// but Standard out of the menu. That guard dated from when kept buildings and the stash
    /// chest used fixed Standard-farm tiles; both now place relative to the player's own spots
    /// and the farmhouse door, so every farm type is allowed.
    ///
    /// Reflection-driven because the PC CharacterCustomization fields are private and differ
    /// from the Android decompile; a missing field is logged once and the notice is skipped.
    /// </summary>
    internal sealed class CharacterCreationWatcher
    {
        private const BindingFlags FieldFlags
            = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private readonly IMonitor _monitor;
        private readonly TheLongestYear.Core.GameplayConfig _config;

        /// <summary>The CharacterCustomization instance being watched (or null), so a reopened
        /// menu re-seeds the checkbox edge detector.</summary>
        private IClickableMenu _watchedInstance;

        /// <summary>Last observed "Skip intro" checkbox value, so the notice fires on the off->on edge.</summary>
        private bool? _lastSkipIntro;
        private bool _warnedNoSkipField;

        public CharacterCreationWatcher(IMonitor monitor, TheLongestYear.Core.GameplayConfig config)
        {
            _monitor = monitor;
            _config = config;
        }

        public void Attach(IModHelper helper)
        {
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
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
                _watchedInstance = null;
                _lastSkipIntro = null;
                return;
            }

            if (!ReferenceEquals(_watchedInstance, cc))
            {
                _watchedInstance = cc;
                _lastSkipIntro = ReadSkipIntro(cc.GetType(), cc);
                return;
            }

            WatchSkipIntroToggle(cc);
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
                _monitor.Log("CharacterCreationWatcher: Skip intro ticked; showing the watch-it-first notice.", LogLevel.Info);
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
                        "CharacterCreationWatcher: skipIntro not found on CharacterCustomization — field name may " +
                        "have changed; the skip-intro notice is unavailable.",
                        LogLevel.Warn);
                }
                return null;
            }
            return (bool)skipFlag.GetValue(cc);
        }
    }
}
