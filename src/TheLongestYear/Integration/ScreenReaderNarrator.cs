using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using TheLongestYear.Core;

namespace TheLongestYear.Integration
{
    /// <summary>
    /// The slice of Stardew Access's public API the narrator uses (its IStardewAccessApi). Declared
    /// locally so TLY builds and runs without Stardew Access; SMAPI maps it at runtime through
    /// Helper.ModRegistry.GetApi.
    /// </summary>
    public interface IStardewAccessApi
    {
        /// <summary>Speaks the text through the loaded screen reader, if any.</summary>
        bool Say(string text, bool interrupt);

        /// <summary>Tells Stardew Access a menu class narrates itself. Without this it reads the
        /// hovered component's internal name ("row-0") over whatever the menu just said.</summary>
        void RegisterCustomMenuAsAccessible(string fullNameOfClass);
    }

    /// <summary>A menu of ours the screen reader can read. Stardew Access reads the game's own
    /// menus by patching each one; it knows nothing about ours, so they describe themselves.</summary>
    internal interface INarratedMenu
    {
        /// <summary>Spoken once when the menu opens: its title, what it is for and how to use it.</summary>
        string NarrationIntro();

        /// <summary>A cheap value that changes whenever <see cref="NarrateFocus"/> would say
        /// something different (the snapped component, a page, a count). Read every tick.</summary>
        object NarrationFocusKey { get; }

        /// <summary>The focused item, read out in full. Empty when nothing is focused.</summary>
        string NarrateFocus();
    }

    /// <summary>
    /// Speaks TLY's own menus through Stardew Access when it is installed. Stardew Access turns
    /// controller-style menus on, so the movement keys and its left click key already reach our
    /// menus; without this they are silent, and the modal ones (the weekly hub cannot be closed
    /// without a pick) lock a blind player out. Does nothing when Stardew Access is not loaded.
    /// </summary>
    internal sealed class ScreenReaderNarrator
    {
        private const string StardewAccessId = "shoaib.stardewaccess";
        /// <summary>The game's "controller mode: always on" option value.</summary>
        private const string ForceControllerMode = "force_on";

        private readonly IMonitor _monitor;
        private readonly NarrationTracker _tracker = new NarrationTracker();
        private IStardewAccessApi _api;

        public ScreenReaderNarrator(IMonitor monitor)
        {
            _monitor = monitor;
        }

        public void Attach(IModHelper helper)
        {
            helper.Events.GameLoop.GameLaunched += (_, _) =>
            {
                if (!helper.ModRegistry.IsLoaded(StardewAccessId)) return;
                _api = helper.ModRegistry.GetApi<IStardewAccessApi>(StardewAccessId);
                if (_api == null)
                {
                    _monitor.Log("Stardew Access is installed but its API could not be read; The Longest Year's menus will not be spoken.", LogLevel.Warn);
                    return;
                }
                int menus = 0;
                foreach (System.Type type in typeof(INarratedMenu).Assembly.GetTypes())
                {
                    if (type.IsAbstract || !typeof(INarratedMenu).IsAssignableFrom(type)) continue;
                    _api.RegisterCustomMenuAsAccessible(type.FullName);
                    menus++;
                }
                _monitor.Log($"Stardew Access found: The Longest Year's menus will be spoken ({menus} menus registered).", LogLevel.Info);
                helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            };
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            INarratedMenu menu = TopMenu() as INarratedMenu;
            switch (_tracker.Observe(menu, menu?.NarrationFocusKey))
            {
                case NarrationCue.MenuOpened:
                    EnsureControllerStyleMenus((IClickableMenu)menu);
                    Speak(NarrationText.Sentences(menu.NarrationIntro(), menu.NarrateFocus()));
                    break;
                case NarrationCue.FocusChanged:
                    Speak(menu.NarrateFocus());
                    break;
            }
        }

        /// <summary>Stardew Access plays every menu controller-style: the movement keys step the
        /// snap, the cursor follows it, and its left click key clicks where the cursor is. It sets
        /// that mode at launch and at the title screen, and a new farm's fresh options can drop it
        /// (seen on a farm made without the character screen). Put it back with the same two
        /// settings Stardew Access uses, and park the cursor on the menu's focused item.</summary>
        private void EnsureControllerStyleMenus(IClickableMenu menu)
        {
            if (Game1.options.gamepadControls && Game1.options.snappyMenus) return;
            Game1.options.setGamepadMode(ForceControllerMode);
            Game1.options.snappyMenus = true;
            if (menu.currentlySnappedComponent != null) menu.snapCursorToCurrentSnappedComponent();
            else menu.snapToDefaultClickableComponent();
            _monitor.Log($"Screen reader: controller-style menus were off when {menu.GetType().Name} opened; switched back on.", LogLevel.Trace);
        }

        /// <summary>The menu taking input: the last child in the active menu's chain.</summary>
        private static IClickableMenu TopMenu()
        {
            IClickableMenu menu = Game1.activeClickableMenu;
            while (menu?.GetChildMenu() is IClickableMenu child)
                menu = child;
            return menu;
        }

        private void Speak(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            bool spoken = _api.Say(text, true);
            _monitor.Log($"Screen reader{(spoken ? "" : " (not spoken)")}: {text}", LogLevel.Trace);
        }
    }
}
