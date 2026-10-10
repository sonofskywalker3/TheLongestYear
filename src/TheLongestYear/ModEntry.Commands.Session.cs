using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Network;
using StardewValley.Quests;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using TheLongestYear.Donations;
using TheLongestYear.Integration;
using TheLongestYear.Loop;
using TheLongestYear.UI;

namespace TheLongestYear
{
    public sealed partial class ModEntry
    {
        /// <summary>Load a save from the title screen by folder name — the same
        /// <c>SaveGame.Load(slotName)</c> + <c>Game1.exitActiveMenu()</c> pair LoadGameMenu's slot
        /// click makes (LoadGameMenu.cs:85-86). Both calls are required: without the menu exit the
        /// TitleMenu stays active after the loader finishes, keeps drawing the title screen, and the
        /// world never proceeds (no SaveLoaded, frozen log).
        /// Debug/automation tool: lets an unattended session load a save via console injection to
        /// read the SaveLoaded diagnostics (e.g. the remixed-bundle classification lines) without
        /// clicking through the title menu. Refuses while a save is already loaded.</summary>
        private void CmdLoadSave(string command, string[] args)
        {
            if (Context.IsWorldReady)
            {
                this.Monitor.Log("A save is already loaded — return to title first (tly_loadsave is title-screen-only).", LogLevel.Warn);
                return;
            }
            if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
            {
                this.Monitor.Log("Usage: tly_loadsave <saveFolderName>  (e.g. tly_loadsave None_123456789)", LogLevel.Info);
                return;
            }

            // SaveGame.Load on a folder that does not exist fails SILENTLY: the game drops to the
            // title screen and simply never finishes loading, which reads exactly like a hang.
            // That is easy to hit because a TLY reset RENAMES the save folder (it re-seeds
            // uniqueIDForThisGame, and the folder name embeds it), so yesterday's folder name is
            // stale after any loop. Check first and list what is actually there.
            string savesDir = System.IO.Path.Combine(
                StardewModdingAPI.Constants.DataPath ?? "", "Saves");
            string target = System.IO.Path.Combine(savesDir, args[0]);
            if (System.IO.Directory.Exists(savesDir) && !System.IO.Directory.Exists(target))
            {
                string[] available = System.IO.Directory.GetDirectories(savesDir)
                    .Select(System.IO.Path.GetFileName).OrderBy(n => n, System.StringComparer.Ordinal).ToArray();
                this.Monitor.Log(
                    $"tly_loadsave: no save folder named '{args[0]}'. A TLY reset renames the folder " +
                    $"(the name embeds uniqueIDForThisGame), so an older name goes stale. Available: " +
                    $"{(available.Length > 0 ? string.Join(", ", available) : "(none)")}",
                    LogLevel.Warn);
                return;
            }

            this.Monitor.Log($"tly_loadsave: loading '{args[0]}'.", LogLevel.Info);
            StardewValley.SaveGame.Load(args[0]);
            Game1.exitActiveMenu();
        }

        /// <summary>Farm-type tokens for <c>tly_newgame</c>: vanilla ids 0-6, plus 7 with a
        /// <c>ModFarmType</c> for the 1.6 Meadowlands (which the game ships as an "additional farm").</summary>
        private static readonly Dictionary<string, int> NewGameFarmTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["standard"] = 0, ["riverland"] = 1, ["forest"] = 2, ["hilltop"] = 3,
            ["wilderness"] = 4, ["fourcorners"] = 5, ["beach"] = 6, ["meadowlands"] = 7,
        };

        private const string MeadowlandsFarmId = "MeadowlandsFarm";

        /// <summary>Title-screen only. Mirrors what the character screen + TitleMenu.createdNewCharacter
        /// do for a "Skip intro" new game, so an unattended run can start a farm of any type. Goes
        /// through the same SaveCreating/SaveLoaded path as a real new game, so TLY activates and
        /// the Advanced Options bundle choice defaults to TLY Custom.</summary>
        private void CmdNewGame(string command, string[] args)
        {
            if (Context.IsWorldReady)
            {
                this.Monitor.Log("A save is already loaded — return to title first (tly_newgame is title-screen-only).", LogLevel.Warn);
                return;
            }
            if (args.Length < 1 || !NewGameFarmTypes.TryGetValue(args[0], out int farmType))
            {
                this.Monitor.Log("Usage: tly_newgame <standard|riverland|forest|hilltop|wilderness|fourcorners|beach|meadowlands> [skipintro] [custom|standard|remixed] [name]", LogLevel.Info);
                return;
            }
            bool skipIntro = args.Skip(1).Any(a => a.Equals("skipintro", StringComparison.OrdinalIgnoreCase));
            // Optional Advanced Options bundle choice (custom / standard / remixed); the default
            // stays TLY Custom. Standard keeps another bundle mod's board, as a player would pick.
            string bundleToken = args.Skip(1).FirstOrDefault(a => BundleOptionPatch.TryParseChoice(a, out _));
            if (bundleToken != null && BundleOptionPatch.TryParseChoice(bundleToken, out var bundleChoice))
                BundleOptionPatch.SetChoice(bundleChoice);
            string name = args.Skip(1).FirstOrDefault(a => !a.Equals("skipintro", StringComparison.OrdinalIgnoreCase)
                && !BundleOptionPatch.TryParseChoice(a, out _)) ?? "Rodger";

            Game1.resetPlayer();
            Game1.player.Name = name;
            Game1.player.displayName = name;
            Game1.player.farmName.Value = args[0];
            Game1.player.favoriteThing.Value = "loops";
            Game1.player.isCustomized.Value = true;

            Game1.whichFarm = farmType;
            Game1.whichModFarm = null;
            Game1.spawnMonstersAtNight = farmType == 4;
            if (farmType == 7)
            {
                var mod = DataLoader.AdditionalFarms(Game1.content).FirstOrDefault(f => f.Id == MeadowlandsFarmId);
                if (mod == null)
                {
                    this.Monitor.Log($"tly_newgame: '{MeadowlandsFarmId}' not found in Data/AdditionalFarms.", LogLevel.Error);
                    return;
                }
                Game1.whichModFarm = mod;
                Game1.spawnMonstersAtNight = mod.SpawnMonstersByDefault;
            }

            // What the character screen's OK does with Skip intro on (TitleMenu.createdNewCharacter),
            // routed through our own prefix so the checkbox choice is recorded the same way.
            Loop.SkipIntroChoicePatch.Choice.Record(skipIntro);
            this.Monitor.Log($"tly_newgame: creating '{name}' on farm type {farmType} ({args[0]}), skipIntro={skipIntro}.", LogLevel.Info);
            if (Game1.activeClickableMenu is TitleMenu)
                TitleMenu.subMenu = null;
            Game1.game1.loadForNewGame();
            Game1.saveOnNewDay = true;
            Game1.player.eventsSeen.Add("60367");
            Game1.player.currentLocation = Utility.getHomeOfFarmer(Game1.player);
            Game1.player.Position = new Microsoft.Xna.Framework.Vector2(9f, 9f) * 64f;
            Game1.player.isInBed.Value = true;
            Game1.NewDay(0f);
            Game1.exitActiveMenu();
            Game1.setGameMode(3);
        }

        private void CmdToTitle(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Already at the title screen.", LogLevel.Info); return; }
            this.Monitor.Log("tly_totitle: exiting to title (no save).", LogLevel.Info);
            Game1.ExitToTitle(() => (Game1.activeClickableMenu as TitleMenu)?.skipToTitleButtons());
        }

        /// <summary>Debug: jump to a given day of the current season (console alias for the file-bridge
        /// <c>tly_setday</c>). Sleep afterward to trigger that day's gate. Usage: tly_setday &lt;day&gt;
        /// (defaults to 28).</summary>
        private void CmdSetDay(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            int day = args.Length > 0 && int.TryParse(args[0], out int d) ? d : 28;
            _runController?.DebugSetDay(day);
        }

        /// <summary>Debug: close whatever menu is up without the mouse. A LevelUpMenu needs its OK
        /// button (exitThisMenu would skip the profession pick and leave vanilla's end-of-night
        /// chain hanging); everything else takes exitThisMenu.</summary>
        private void CmdDismiss(string command, string[] args)
        {
            var menu = Game1.activeClickableMenu;
            if (menu == null) { this.Monitor.Log("tly_dismiss: no menu open.", LogLevel.Info); return; }
            string name = menu.GetType().Name;
            if (menu is StardewValley.Menus.LevelUpMenu levelUp)
            {
                var ok = typeof(StardewValley.Menus.LevelUpMenu).GetMethod("okButtonClicked",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (ok != null) { ok.Invoke(levelUp, null); this.Monitor.Log($"tly_dismiss: {name} OK clicked.", LogLevel.Info); return; }
            }
            // A DialogueBox must close through its own closeDialogue (the click path): exitThisMenu
            // leaves Game1.dialogueUp set, which freezes every fade-to-clear (ScreenFade.UpdateFadeAlpha)
            // and hung a crop fairy night forever (smoke 2026-10-06: the fairy waits on !fadeToBlack).
            if (menu is StardewValley.Menus.DialogueBox box)
            {
                box.closeDialogue();
                this.Monitor.Log($"tly_dismiss: {name} closed (dialogueUp={Game1.dialogueUp}).", LogLevel.Info);
                return;
            }
            // A NamingMenu (incubator hatch, barn birth, a new horse) names through its own Enter path, so the
            // callback (AnimalHouse.addNewHatchedAnimal for an animal) runs exactly as a typed name would.
            if (menu is StardewValley.Menus.NamingMenu naming)
            {
                if (string.IsNullOrWhiteSpace(naming.textBox.Text)) naming.textBox.Text = "Tester";
                string chosen = naming.textBox.Text;
                naming.textBoxEnter(naming.textBox);
                this.Monitor.Log($"tly_dismiss: {name} named '{chosen}'.", LogLevel.Info);
                return;
            }
            menu.exitThisMenu(playSound: false);
            this.Monitor.Log($"tly_dismiss: {name} closed.", LogLevel.Info);
        }

        /// <summary>Debug: open the basic win screen → JP shrine → keep-playing choice, the real
        /// win-path flow. See <see cref="RunController.DebugForceWin"/>.</summary>

        /// <summary>Debug: pick a response on the open question dialogue without the mouse, for the
        /// headless runbook (e.g. the loop-again/keep-playing choice after the Year One Ending, or
        /// the Year 2 wall). Forces the dialogue's text fully shown and its safety timer clear, then
        /// drives the same <see cref="StardewValley.Menus.DialogueBox.receiveLeftClick"/> path a
        /// click on that response takes.</summary>
        private void CmdAnswer(string command, string[] args)
        {
            if (!(Game1.activeClickableMenu is StardewValley.Menus.DialogueBox box) ||
                !box.isQuestion || box.responses == null || box.responses.Length == 0)
            {
                this.Monitor.Log("tly_answer: no question dialogue is open.", LogLevel.Warn);
                return;
            }
            if (args.Length >= 1 && args[0].Equals("key", System.StringComparison.OrdinalIgnoreCase))
            {
                // Debug: the keyboard path (Escape, or N with "tly_answer key n"), which vanilla routes
                // through receiveKeyPress instead of the click path above.
                var key = args.Length >= 2 && args[1].Equals("n", System.StringComparison.OrdinalIgnoreCase)
                    ? Microsoft.Xna.Framework.Input.Keys.N
                    : Microsoft.Xna.Framework.Input.Keys.Escape;
                box.transitioning = false;
                box.safetyTimer = 0;
                box.receiveKeyPress(key);
                this.Monitor.Log($"tly_answer: sent key {key} (dialogueUp={Game1.dialogueUp}).", LogLevel.Info);
                return;
            }
            if (args.Length < 1 || !int.TryParse(args[0], out int n))
            {
                this.Monitor.Log("Usage: tly_answer <n> (0-based response index) | tly_answer key [n]", LogLevel.Warn);
                return;
            }
            if (n < 0 || n >= box.responses.Length)
            {
                this.Monitor.Log($"tly_answer: {n} is out of range (0..{box.responses.Length - 1}); clamping.", LogLevel.Warn);
                n = System.Math.Clamp(n, 0, box.responses.Length - 1);
            }
            try
            {
                string text = box.responses[n].responseText;
                string current = box.getCurrentString();
                if (current != null) box.characterIndexInDialogue = current.Length;
                box.safetyTimer = 0;
                // A freshly opened question box sets transitioning=true until its open animation
                // ends, and receiveLeftClick returns immediately while it is true, so the click
                // would silently no-op. Clear it before clicking.
                box.transitioning = false;
                box.selectedResponse = n;
                box.receiveLeftClick(0, 0, false);
                // A taken answer starts the box's outro (transitioning) and it closes itself a few
                // frames later; callbacks that open a menu afterwards (the voluntary restart's No)
                // wait for that close. Only a box still up and not closing ignored the click.
                if (object.ReferenceEquals(Game1.activeClickableMenu, box) && !box.transitioning)
                    this.Monitor.Log("tly_answer: the box did not close; send it again", LogLevel.Warn);
                else
                    this.Monitor.Log($"tly_answer: chose response {n} (\"{text}\").", LogLevel.Info);
            }
            catch (System.Exception ex)
            {
                this.Monitor.Log($"tly_answer failed: {ex.Message}", LogLevel.Error);
            }
        }

        private void CmdSelect(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (args.Length < 1) { this.Monitor.Log("Usage: tly_select <theme> [left|right]", LogLevel.Warn); return; }
            // With the planning hub open this is the same as clicking the card, so an unattended
            // run never needs the mouse: the hub commits the pick (current week or the day-28
            // next-month pre-pick) and closes itself.
            if (Game1.activeClickableMenu is TheLongestYear.UI.WeeklyHubMenu hub)
            {
                // Optional side: the real card click (multiplier and mystery included).
                string side = args.Length > 1 ? args[1] : null;
                if (side != null && !hub.TryPickSide(args[0], side, out string sideError))
                {
                    this.Monitor.Log($"tly_select: {sideError}", LogLevel.Warn);
                    return;
                }
                if (side != null)
                    return; // TryPickSide already logged the real outcome.
                if (!hub.ConfirmByName(args[0]))
                    this.Monitor.Log($"tly_select: unknown theme '{args[0]}'. Options: {string.Join(", ", Enum.GetNames(typeof(TheLongestYear.Core.Theme)))}.", LogLevel.Warn);
                else if (hub.LastPickTook)
                    this.Monitor.Log($"tly_select: picked {args[0]} on the open planning hub.", LogLevel.Info);
                else
                    this.Monitor.Log($"tly_select: {args[0]} was rejected (already picked this month).", LogLevel.Warn);
                return;
            }
            // skipOfferCheck: this is a debug/playtest command; let it force any theme, not just
            // the seeded pair. The SelectedThemesThisMonth dedupe inside Select still applies.
            _runController.SelectByName(args[0], skipOfferCheck: true);
        }

        private void CmdSkipScene(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (Game1.activeClickableMenu is TheLongestYear.UI.Day28CutsceneMenu scene)
            {
                scene.SkipToEnd();
                this.Monitor.Log("tly_skipscene: finished the day-28 scene.", LogLevel.Info);
                return;
            }
            if (Game1.activeClickableMenu is TheLongestYear.UI.VictoryMenu victory)
            {
                // The win screen finishes on any click; closing it any other way makes the
                // day-28 driver re-arm it (headless keep-playing runbook, Nexus bug 1130863).
                victory.receiveLeftClick(0, 0, playSound: false);
                this.Monitor.Log("tly_skipscene: finished the win screen.", LogLevel.Info);
                return;
            }
            string blocking = Game1.activeClickableMenu is StardewValley.Menus.DialogueBox box
                ? $" text=\"{box.getCurrentString()}\""
                : "";
            this.Monitor.Log($"tly_skipscene: no day-28 scene is open (activeClickableMenu={Game1.activeClickableMenu?.GetType().Name ?? "none"}{blocking}).", LogLevel.Info);
        }

        private void CmdOffer(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _runController.PresentOffer();
        }

        private void CmdDonate(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (args.Length < 1) { this.Monitor.Log("Usage: tly_donate <itemId>", LogLevel.Warn); return; }
            _runController.Donate(args[0]);
        }
    }
}
