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
        /// <summary>Debug: say where a running event is stuck and step a speak line on. Headless runs
        /// have no mouse, so an event's `speak` box never advances on its own; this clicks it. Also
        /// prints the current command and every actor's tile so a blocked `move` is visible.</summary>
        private void CmdWitness(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            string verb = args.Length > 0 ? args[0].ToLowerInvariant() : "list";
            if (verb == "list")
            {
                var records = _meta.Run.WitnessLines ?? new();
                this.Monitor.Log($"tly_witness: {records.Count} record(s): " + string.Join("; ",
                    records.ConvertAll(r => $"{r.Npc} scene day {r.SceneDayOfYear} said={r.Said} held={r.HeldForIntroduction}")), LogLevel.Info);
                return;
            }
            if (verb == "click")
            {
                if (Game1.activeClickableMenu is StardewValley.Menus.DialogueBox open)
                {
                    open.safetyTimer = 0;
                    open.finishTyping();
                    open.receiveLeftClick(0, 0, false);
                    this.Monitor.Log($"tly_witness: clicked the dialogue box on; {DescribeOpenDialogue()}.", LogLevel.Info);
                }
                else this.Monitor.Log($"tly_witness: no dialogue box open ({DescribeOpenDialogue()}).", LogLevel.Info);
                return;
            }
            NPC npc = args.Length > 1 ? Game1.getCharacterFromName(args[1]) : null;
            if (npc == null) { this.Monitor.Log("Usage: tly_witness list | peek <npc> | show <npc> | talk <npc> | click | arm <Linus|Shane> | fresh <npc>", LogLevel.Warn); return; }
            this.Monitor.Log($"tly_witness: {npc.Name} stack={npc.CurrentDialogue.Count}: " + string.Join(" | ",
                System.Linq.Enumerable.Select(npc.CurrentDialogue, d => $"[{d?.TranslationKey}] '{d?.getCurrentDialogue()}'")), LogLevel.Info);
            switch (verb)
            {
                case "show":
                    Game1.drawDialogue(npc);
                    this.Monitor.Log($"tly_witness: {DescribeOpenDialogue()}.", LogLevel.Info);
                    break;
                case "talk":
                    bool sameLocation = Game1.player.currentLocation == npc.currentLocation;
                    bool acted = npc.checkAction(Game1.player, npc.currentLocation);
                    this.Monitor.Log($"tly_witness: talked to {npc.Name} (checkAction={acted}, same location={sameLocation}, canMove={Game1.player.CanMove}); {DescribeOpenDialogue()}.", LogLevel.Info);
                    break;
                case "arm":
                    string call = _witness?.ArmForTest(npc.Name);
                    this.Monitor.Log(call != null
                        ? $"tly_witness: {npc.Name}'s line is recorded as if he saw last night's scene; this morning's call: {call}."
                        : $"tly_witness: {npc.Name} has no witness line.", LogLevel.Info);
                    break;
                case "fresh":
                    Game1.player.mailReceived.Remove(npc.Name + "_Introduction");
                    if (!Game1.player.activeDialogueEvents.ContainsKey("Introduction")) Game1.player.activeDialogueEvents.Add("Introduction", 6);
                    this.Monitor.Log($"tly_witness: {npc.Name}'s Introduction is armed again for the next talk.", LogLevel.Info);
                    break;
            }
        }

        /// <summary>What dialogue box is up, for the tly_witness log.</summary>
        private static string DescribeOpenDialogue()
        {
            if (Game1.activeClickableMenu is not StardewValley.Menus.DialogueBox box)
                return $"menu now {Game1.activeClickableMenu?.GetType().Name ?? "none"}";
            Dialogue d = box.characterDialogue;
            return d == null
                ? $"an object dialogue box is open: '{box.getCurrentString()}'"
                : $"{d.speaker?.Name ?? "?"}'s [{d.TranslationKey}] is open: '{box.getCurrentString()}'";
        }

        private void CmdEventStep(string command, string[] args)
        {
            // Context.IsWorldReady stays false for the whole pre-Day-1 window (SMAPI holds it off
            // while Game1.dayOfMonth == 0, precisely so mods don't act on a "new-game intro not
            // finished yet" world). That is exactly the window the arrival event (60367/u 0)
            // runs in. Gating this on IsWorldReady made tly_newgame <type> arrival + tly_eventstep
            // deadlock: the event can't be stepped until the world is ready, and the world isn't
            // ready until the event's "end beginGame" runs. Allow stepping whenever an event is
            // actually running, even pre-Day-1.
            if (!Context.IsWorldReady && Game1.currentLocation?.currentEvent == null)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }
            // The rewind cutscene's farmhouse beats are drawn menus, not vanilla Events, and their
            // speech box is a plain object the scene forwards input to rather than the active menu.
            // So neither the "no event" bail below nor the EndingSpeechBox branch further down can
            // reach it, and headlessly the scene sat on its first line forever: the only way through
            // was tly_skipscene, which jumps the whole beat and is why the middle of the scene had
            // never been looked at. Step it here, before the event check.
            if (Game1.activeClickableMenu is UI.RewindJunimoScene rewind)
            {
                rewind.receiveLeftClick(0, 0, false);
                this.Monitor.Log(
                    $"tly_eventstep: clicked the rewind scene's speech box on ({rewind.GetType().Name}).",
                    LogLevel.Info);
                return;
            }

            StardewValley.Event evt = Game1.currentLocation?.currentEvent;
            if (evt == null)
            {
                this.Monitor.Log("tly_eventstep: no event.", LogLevel.Info);
                return;
            }
            string cmd;
            try { cmd = evt.GetCurrentCommand(); }
            catch (System.Exception ex)
            {
                this.Monitor.Log($"tly_eventstep: could not read the current command ({ex.GetType().Name}: {ex.Message}).", LogLevel.Warn);
                cmd = "<" + ex.GetType().Name + ">";
            }
            this.Monitor.Log(
                $"tly_eventstep: cmd[{evt.CurrentCommand}]='{cmd}', menu={Game1.activeClickableMenu?.GetType().Name ?? "none"}, dialogueUp={Game1.dialogueUp}, loc={Game1.currentLocation?.Name}.",
                LogLevel.Info);
            foreach (NPC a in evt.actors)
                this.Monitor.Log($"tly_eventstep:   actor {a.Name} at ({(int)a.Tile.X}, {(int)a.Tile.Y}) moving={a.isMoving()}.", LogLevel.Info);
            {
                Farmer f = Game1.player;
                var bb = f.GetBoundingBox();
                string targets = "";
                try
                {
                    var fld = typeof(StardewValley.Event).GetField("actorPositionsAfterMove", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (fld?.GetValue(evt) is System.Collections.Generic.Dictionary<string, Microsoft.Xna.Framework.Vector3> d)
                        targets = string.Join(",", d.Select(kv => $"{kv.Key}->({kv.Value.X},{kv.Value.Y})"));
                }
                catch (System.Exception ex) { targets = "<" + ex.GetType().Name + ">"; }
                this.Monitor.Log($"tly_eventstep:   farmer tile ({(int)f.Tile.X}, {(int)f.Tile.Y}) pos ({(int)f.Position.X}, {(int)f.Position.Y}) box ({bb.X},{bb.Y},{bb.Width},{bb.Height}) moving={f.isMoving()} speed={f.getMovementSpeed():0.0} facing={f.FacingDirection} pending=[{targets}] viewport=({Game1.viewport.X},{Game1.viewport.Y}).", LogLevel.Info);
            }

            if (Game1.activeClickableMenu is Integration.EndingSpeechBox speech)
            {
                speech.receiveLeftClick(0, 0, false);   // finish typing, next page, or close
                speech.receiveLeftClick(0, 0, false);
                this.Monitor.Log("tly_eventstep: clicked the ending speech box on.", LogLevel.Info);
            }
            else if (Game1.activeClickableMenu is StardewValley.Menus.DialogueBox box)
            {
                string current = box.getCurrentString();
                if (current != null) box.characterIndexInDialogue = current.Length;
                box.safetyTimer = 0;
                box.transitioning = false;
                box.receiveLeftClick(0, 0, false);
                this.Monitor.Log("tly_eventstep: clicked the dialogue box on.", LogLevel.Info);
            }
        }

        /// <summary>Debug: what art is actually on the map, per layer, for a rectangle of tiles.
        /// Written to settle a question a screenshot alone could not (2026-09-11): the farmhouse
        /// window is white in the morning and near-black at midnight, but the farmhouse's night
        /// ambient is only 180/180/0, which cannot take a white pane to black, and the map reports no
        /// NightTiles and no DayTiles property at all. Running this at both times says whether the
        /// art changes or the light does.</summary>
        private void CmdTiles(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            GameLocation loc = Game1.currentLocation;
            if (loc?.map == null) { this.Monitor.Log("tly_tiles: no map.", LogLevel.Warn); return; }
            if (args.Length < 2
                || !int.TryParse(args[0], out int x0) || !int.TryParse(args[1], out int y0))
            {
                this.Monitor.Log("tly_tiles <x> <y> [width] [height]", LogLevel.Warn);
                return;
            }
            int w = args.Length > 2 && int.TryParse(args[2], out int pw) ? pw : 3;
            int h = args.Length > 3 && int.TryParse(args[3], out int ph) ? ph : 3;

            this.Monitor.Log(
                $"tly_tiles: '{loc.Name}' at ({x0},{y0}) {w}x{h}, timeOfDay={Game1.timeOfDay}, " +
                $"ambient={Game1.ambientLight}, lightGlows={loc.lightGlows.Count}.",
                LogLevel.Info);
            foreach (xTile.Layers.Layer layer in loc.map.Layers)
            {
                var sb = new System.Text.StringBuilder($"tly_tiles:   {layer.Id}:");
                for (int y = y0; y < y0 + h; y++)
                {
                    for (int x = x0; x < x0 + w; x++)
                    {
                        string cell;
                        try
                        {
                            xTile.Tiles.Tile tile = x >= 0 && y >= 0 && x < layer.LayerWidth && y < layer.LayerHeight
                                ? layer.Tiles[x, y]
                                : null;
                            cell = tile == null ? "-" : tile.TileIndex.ToString();
                        }
                        catch (System.Exception) { cell = "?"; }
                        sb.Append(' ').Append($"({x},{y})={cell}");
                    }
                }
                this.Monitor.Log(sb.ToString(), LogLevel.Info);
            }
        }

        /// <summary>Debug: the whole lighting picture for one frame, in one place.
        ///
        /// Written for the farmhouse window (2026-09-11). The window stays lit for about a second
        /// after the room goes dark and then fades to black, which is the signature of a
        /// <c>LightSource</c> with <c>fadeOut</c> set, not of tile art: vanilla sets
        /// <c>fadeOut = 4</c> on any <c>LightContext.WindowLight</c> source once
        /// <c>Game1.isTimeToTurnOffLighting</c> is true, and <c>LightSource.Draw</c> then takes 4 off
        /// its alpha every frame until it stops drawing. This prints the alpha so that fade can be
        /// watched happening rather than inferred.</summary>
        private void CmdLights(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            GameLocation loc = Game1.currentLocation;
            this.Monitor.Log(
                $"tly_lights: '{loc?.Name}' timeOfDay={Game1.timeOfDay}, ambient={Game1.ambientLight}, " +
                $"drawLighting={Game1.drawLighting}, startDark={Game1.getStartingToGetDarkTime(loc)}, " +
                $"trulyDark={Game1.getTrulyDarkTime(loc)}, turnOffLighting={Game1.isTimeToTurnOffLighting(loc)}, " +
                $"lightGlows={loc?.lightGlows.Count}, sharedLights={loc?.sharedLights.Length}, " +
                $"sources={Game1.currentLightSources.Count}.",
                LogLevel.Info);
            foreach (var pair in Game1.currentLightSources)
            {
                StardewValley.LightSource light = pair.Value;
                this.Monitor.Log(
                    $"tly_lights:   '{pair.Key}' context={light.lightContext.Value} colour={light.color.Value} " +
                    $"alpha={light.color.Value.A} radius={light.radius.Value:0.00} " +
                    $"pos=({light.position.Value.X:0},{light.position.Value.Y:0}) fadeOut={light.fadeOut.Value} " +
                    $"texture={light.textureIndex.Value}.",
                    LogLevel.Info);
            }
        }

        private void CmdOpenCookbook(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _launcher?.OpenCookbook();
        }

        private void CmdOpenCraftbook(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _launcher?.OpenCraftbook();
        }

        private void CmdOpenHerdBook(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _launcher?.OpenHerdBook();
        }

        private void CmdOpenStash(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            var chest = _stashService?.FindStashChest();
            if (chest == null)
            {
                this.Monitor.Log("No stash chest found. Own stash_1 and run tly_setstash first.", LogLevel.Warn);
                return;
            }
            chest.ShowMenu();
        }

        /// <summary>Debug: open the planning shrine on a tab, the same construction the statue's
        /// checkForAction patch does, so the bridge can exercise every tab's row builder.</summary>
        private void CmdOpenShrine(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            var tab = TheLongestYear.UI.ShrinePreviewMenu.ShrineTab.Active;
            if (args.Length > 0 && !System.Enum.TryParse(args[0], ignoreCase: true, out tab))
            {
                this.Monitor.Log("Usage: tly_openshrine [active|boosts|focus|plan|donate]", LogLevel.Warn);
                return;
            }
            var menu = new TheLongestYear.UI.ShrinePreviewMenu(
                _meta.State, _meta.State.EffectiveDifficulty(_config).ShrinePriceFactor, _meta.Run,
                (id, skill) => _boostPurchases.TryBuy(id, skill),
                () => _runController?.IsVoluntaryRestartOffered() == true,
                () => _runController?.AskVoluntaryRestart(),
                ShrineDonationService.Active, _focus,
                () => _runController?.IsRewindChainRunning == true);
            if (!menu.ShowTab(tab))
            {
                this.Monitor.Log($"tly_openshrine: no {tab} tab here (Donate needs shrine goals this week); opened on Active.", LogLevel.Warn);
                tab = TheLongestYear.UI.ShrinePreviewMenu.ShrineTab.Active;
            }
            Game1.activeClickableMenu = menu;
            this.Monitor.Log($"tly_openshrine: shrine opened on the {tab} tab.", LogLevel.Info);
            var block = _runController?.VoluntaryRestartBlock() ?? TheLongestYear.Core.Day28.RestartBlock.ResetRunning;
            this.Monitor.Log(
                block == TheLongestYear.Core.Day28.RestartBlock.None
                    ? "tly_openshrine: restart button shown."
                    : $"tly_openshrine: restart button hidden ({block}).",
                LogLevel.Info);
            // Debug: the restart button's bounds against the tabs at this window size.
            var restart = this.Helper.Reflection.GetField<ClickableComponent>(menu, "_restartButton").GetValue();
            if (restart != null)
            {
                var tabs = this.Helper.Reflection.GetField<List<ClickableTextureComponent>>(menu, "_tabs").GetValue();
                var tabRects = tabs.Select(t => t.bounds).ToList();
                if (menu.upperRightCloseButton != null) tabRects.Add(menu.upperRightCloseButton.bounds);
                bool overlap = tabRects.Any(r => r.Intersects(restart.bounds));
                this.Monitor.Log(
                    $"tly_openshrine: viewport {Game1.uiViewport.Width}x{Game1.uiViewport.Height}, restart {restart.bounds}, " +
                    $"tabs+close {string.Join(" ", tabRects)}, overlap={overlap}.", LogLevel.Info);
            }
        }

        private void CmdOpenHub(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _launcher?.OpenWeeklyHub();
        }

        private void CmdSeasonGoals(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _launcher?.OpenSeasonGoals();
        }

        private void CmdBundleTab(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            var words = new System.Collections.Generic.List<string>(args);
            int shotAt = words.FindIndex(w => w.Equals("shot", System.StringComparison.OrdinalIgnoreCase));
            string[] shotPath = shotAt >= 0 ? words.Skip(shotAt + 1).ToArray() : System.Array.Empty<string>();
            if (shotAt >= 0) words.RemoveRange(shotAt, words.Count - shotAt);
            bool stayOnInventory = words.Any(w => w.Equals("inventory", System.StringComparison.OrdinalIgnoreCase));
            bool hover = words.Any(w => w.Equals("hover", System.StringComparison.OrdinalIgnoreCase));

            Game1.activeClickableMenu = null;
            StardewValley.Menus.GameMenu menu = TheLongestYear.Integration.BundleLogTab.OpenOnTab(!stayOnInventory);
            if (menu == null) { this.Monitor.Log("tly_bundletab: no Bundle Log tab (is a run active?).", LogLevel.Warn); return; }
            int index = TheLongestYear.Integration.BundleLogTab.IndexIn(menu);
            // The label as if the cursor were on the tab; the next hover update clears it.
            if (hover) menu.hoverText = menu.tabs[index].label;
            this.Monitor.Log($"tly_bundletab: tab {index} of {menu.tabs.Count} at {menu.tabs[index].bounds}, open tab {menu.currentTab}.", LogLevel.Info);
            if (words.Any(w => w.Equals("snapcheck", System.StringComparison.OrdinalIgnoreCase)))
                LogBundleTabSnaps(menu, index);
            if (shotAt >= 0) TheLongestYear.DebugCommands.MenuShotCommand.Run(this.Monitor, shotPath);
        }

        /// <summary>Follows the controller neighbours by id without moving the cursor (snapping moves the
        /// real mouse): the log page's top row up, the tab left and right, and the triggers.</summary>
        private void LogBundleTabSnaps(StardewValley.Menus.GameMenu menu, int index)
        {
            string Name(StardewValley.Menus.IClickableMenu page, int id)
                => page?.getComponentWithID(id) is { } c ? $"{c.name}({c.myID})" : $"none({id})";

            menu.changeTab(index, playSound: false);
            StardewValley.Menus.IClickableMenu page = menu.GetCurrentPage();
            StardewValley.Menus.ClickableComponent tab = menu.tabs[index];
            StardewValley.Menus.ClickableComponent row0 = page.allClickableComponents.FirstOrDefault(c => c.name == "row-0");
            StardewValley.Menus.ClickableComponent before = menu.tabs[index - 1];
            this.Monitor.Log($"tly_bundletab snap: row-0 up -> {Name(page, row0?.upNeighborID ?? -1)}; tab left -> {Name(page, tab.leftNeighborID)}; " +
                $"tab down -> {tab.downNeighborID} (-99999 = the page's default, {page.GetType().Name}); {before.name} right -> {Name(page, before.rightNeighborID)}.", LogLevel.Info);

            menu.changeTab(index - 1, playSound: false);
            menu.receiveGamePadButton(Microsoft.Xna.Framework.Input.Buttons.RightTrigger);
            int afterRight = menu.currentTab;
            menu.receiveGamePadButton(Microsoft.Xna.Framework.Input.Buttons.RightTrigger);
            int afterSecondRight = menu.currentTab;
            menu.receiveGamePadButton(Microsoft.Xna.Framework.Input.Buttons.LeftTrigger);
            this.Monitor.Log($"tly_bundletab snap: right trigger from tab {index - 1} -> {afterRight}, again -> {afterSecondRight}, left trigger -> {menu.currentTab}.", LogLevel.Info);
            menu.changeTab(index, playSound: false);
        }

        private void CmdOpenShop(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            // Debug: focus rows shown so they can be checked; a buy here is keep only, as at a rewind.
            _launcher?.OpenShrineShop(focusRows: true);
            // Debug: tly_openshop <tab> [hoverRow] [scroll] shows a tab (for headless screenshots).
            if (args.Length >= 1 && Game1.activeClickableMenu is TheLongestYear.UI.JunimoShrineMenu shop
                && System.Enum.TryParse(args[0], ignoreCase: true, out TheLongestYear.Core.UpgradeCategory tab))
                shop.DebugShow(tab,
                    args.Length >= 2 && int.TryParse(args[1], out int hover) ? hover : -1,
                    args.Length >= 3 && int.TryParse(args[2], out int scroll) ? scroll : 0);
        }
    }
}
