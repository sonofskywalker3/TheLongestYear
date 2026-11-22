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
                _monitor.Log("Stardew Access found: The Longest Year's menus will be spoken.", LogLevel.Info);
                helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            };
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            INarratedMenu menu = TopMenu() as INarratedMenu;
            switch (_tracker.Observe(menu, menu?.NarrationFocusKey))
            {
                case NarrationCue.MenuOpened:
                    Speak(NarrationText.Sentences(menu.NarrationIntro(), menu.NarrateFocus()));
                    break;
                case NarrationCue.FocusChanged:
                    Speak(menu.NarrateFocus());
                    break;
            }
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
