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
        /// <summary>Reset only if the loaded save's farmer name matches the argument. Used by the
        /// debug-command-file bridge to queue a "reset on next load of save X" without affecting
        /// other saves (e.g. write 'tly_resetif puffpuff' before exit, then loading puffpuff
        /// resets it but loading any other save is a no-op).</summary>
        private void ResetIfNameMatches(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            if (args.Length < 1)
            {
                this.Monitor.Log("Usage: tly_resetif <farmerName>", LogLevel.Warn);
                return;
            }

            string target = args[0];
            string current = Game1.player?.Name ?? "";
            if (!string.Equals(current, target, StringComparison.OrdinalIgnoreCase))
            {
                this.Monitor.Log(
                    $"tly_resetif: current save is '{current}', not '{target}'. Skipping reset.",
                    LogLevel.Info);
                return;
            }

            this.Monitor.Log($"tly_resetif: name matches '{target}', resetting.", LogLevel.Info);
            FullResetAndPresentOffer();
        }

        private void ForceReset(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            int? pin = null;
            if (args.Length > 0)
            {
                if (!int.TryParse(args[0], out int seedLoop) || seedLoop < 0)
                {
                    this.Monitor.Log($"tly_reset: '{args[0]}' is not a seed loop. Usage: tly_reset [seedLoop]", LogLevel.Warn);
                    return;
                }
                pin = seedLoop;
            }

            // The pin mutates MetaState, so it must not run when the reset itself cannot: a missing
            // run controller used to leave the save carrying a pinned seed loop for a reset that
            // never happened.
            if (_runController == null)
            {
                this.Monitor.Log("Reset unavailable: no run controller (load a save first).", LogLevel.Warn);
                return;
            }

            if (pin.HasValue) PinSeedLoopForNextReset(pin.Value);

            FullResetAndPresentOffer();
        }

        /// <summary>Debug: force the NEXT reset onto a chosen bundle seed loop, so two runs can
        /// be played on the same board (same number <c>tly_genbundles</c> takes).
        ///
        /// The pin has to survive <see cref="BundleHold.ConsumeChoiceAtReset"/>, which otherwise
        /// snaps BundleSeedLoop back to the post-bump CompletedResets for any reset that skipped
        /// the Fail-night hold question, which is exactly the console path.  Stamping
        /// HoldChoiceMadeForReset makes that call a no-op, so the pin stands.
        /// ConsecutiveHolds is zeroed too: this is a debug pin, not a paid hold.</summary>
        private void PinSeedLoopForNextReset(int seedLoop)
        {
            TheLongestYear.Core.MetaState state = _meta.State;
            state.BundleSeedLoop = seedLoop;
            state.ConsecutiveHolds = 0;
            state.HoldChoiceMadeForReset = true;
            this.Monitor.Log(
                $"tly_reset: pinned bundle seed loop {seedLoop} for this reset (consecutive holds zeroed).",
                LogLevel.Info);
            if (TheLongestYear.Core.BundleSourceNames.IsVanilla(state.BundleSource))
                this.Monitor.Log(
                    "tly_reset: this save runs a vanilla board, which regenerates through loadForNewGame and never reads the seed loop, so the pin will not change the board.",
                    LogLevel.Warn);
        }

        /// <summary>Debug: simulate a day-28 gate-miss reset (shrine-spend → reset → persist),
        /// the natural loop-boundary path. See <see cref="RunController.DebugForceFailReset"/>.</summary>
        private void CmdFailReset(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            _runController?.DebugForceFailReset();
        }

        /// <summary>Debug: arm the Year One Ending for tomorrow morning, the real win-night flow.
        /// See <see cref="RunController.DebugForceWin"/>.</summary>
        private void CmdForceWin(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            _runController?.DebugForceWin();
        }

        /// <summary>Debug: give a villager enough familiarity and memory that EndingSpeaker picks them
        /// and EndingLine lands on the asked tier (1 birthday gifts twice, 2 a heart scene twice,
        /// 3 gifts, 4 talks). A tly_newgame farm has no history at all, so without this the crack
        /// scene never plays in a test (Jeff, 2026-09-07).</summary>
        private void CmdRemember(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (args.Length < 1) { this.Monitor.Log("Usage: tly_remember <Name> [tier 1-4]", LogLevel.Warn); return; }
            string name = args[0];
            int tier = args.Length > 1 && int.TryParse(args[1], out int t) ? Math.Clamp(t, 1, 4) : 1;
            var mem = new TheLongestYear.Core.VillagerMemory { Talks = 20, Loops = new List<int> { 1, 2 } };
            switch (tier)
            {
                case 1: mem.BirthdayGifts = 2; mem.BirthdayGiftLoops = new List<int> { 1, 2 }; mem.Gifts = 4; mem.GiftLoops = new List<int> { 1, 2 }; break;
                case 2:
                    // Any scene in the table whose key names this villager (e.g. "abigail-2").
                    string scene = TheLongestYear.Core.Ending.EndingLine.SceneTable
                        .Where(kv => kv.Value.Contains("." + name.ToLowerInvariant() + "-"))
                        .Select(kv => kv.Key).FirstOrDefault();
                    if (scene != null) mem.HeartEventLoops[scene] = new List<int> { 1, 2 };
                    else this.Monitor.Log($"tly_remember: {name} has no scene phrase; falling through to the gifts tier.", LogLevel.Warn);
                    mem.HeartEvents = 2;
                    break;
                case 3: mem.Gifts = 6; mem.GiftLoops = new List<int> { 1, 2 }; break;
                default: break;
            }
            _meta.State.VillagerMemory[name] = mem;
            _meta.State.VillagerFamiliarity[name] = Math.Max(_config.DejaVuThreshold, 1) + 5;
            this.Monitor.Log($"tly_remember: {name} now qualifies as the ending speaker (tier {tier}); persists on the next save.", LogLevel.Info);
        }

        /// <summary>Debug (tly_sabotage aurachest): another flavour of the same good to set beside a
        /// flavoured taint, so the chest shows one marked and one not: a fixed fruit or fish, the
        /// second one when the first is the tainted flavour itself.</summary>
        private static string OtherFlavorFor(string itemId, string flavor)
        {
            string bare = TheLongestYear.Core.BundleParsing.StripQualifier(flavor);
            bool fish = TheLongestYear.Core.BundleParsing.NormalizeItemId(itemId) == TheLongestYear.Core.FlavoredSlotRules.SmokedFish;
            string first = fish ? AuraContrastFish : AuraContrastFruit;
            string second = fish ? AuraContrastFishAlt : AuraContrastFruitAlt;
            return first == bare ? second : first;
        }

        private const string AuraContrastFruit = "613";     // Apple
        private const string AuraContrastFruitAlt = "634";  // Apricot
        private const string AuraContrastFish = "145";      // Sunfish
        private const string AuraContrastFishAlt = "142";   // Carp

        /// <summary>Debug: replay a season-turn scene now (spec 2026-09-07), no continuation.</summary>
        /// <summary>Darkness pushback debug: force a front now, or print the state.</summary>
        private void CmdSabotage(string command, string[] args)
        {
            if (!Context.IsWorldReady || _sabotage == null) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            string sub = args.Length > 0 ? args[0].ToLowerInvariant() : "status";
            var rng = new System.Random();
            int dayOfYear = TheLongestYear.Core.Calendar.DayOfYear((int)_meta.Run.Season, _meta.Run.DayOfMonth);
            switch (sub)
            {
                case "status":
                    this.Monitor.Log(_sabotage.Status(), LogLevel.Info);
                    break;
                case "blight":
                {
                    TheLongestYear.Core.DifficultyStep level = _meta.State.EffectiveDifficulty(_config).Darkness;
                    int crops = args.Length > 1 && int.TryParse(args[1], out int c) ? c
                        : TheLongestYear.Loop.BlightPass.CountFor(_meta.Run.Season, level);
                    int spoil = args.Length > 2 && int.TryParse(args[2], out int sp) ? sp
                        : TheLongestYear.Core.Sabotage.BlightRule.SpoilCount(TheLongestYear.Loop.SpoilagePass.StoredUnits(TheLongestYear.Core.Sabotage.DarknessLevels.StorageReachesEverything(level)), _meta.Run.Season, level);
                    int taken = _sabotage.Blight(crops, spoil, rng);
                    this.Monitor.Log($"Blight: {taken} thing(s) taken (asked {crops} crops, {spoil} stored). Sleep to see the report.", LogLevel.Info);
                    break;
                }
                case "revert":
                    this.Monitor.Log(_sabotage.Revert(rng) ? "Reversion: a slot came undone. Sleep to see the report." : "Reversion: no candidate slot.", LogLevel.Info);
                    break;
                case "tamper":
                    this.Monitor.Log(_sabotage.Tamper(rng, dayOfYear) ? "Tampering: the board changed. Sleep to see the report." : "Tampering: no open slot with a Winter fit.", LogLevel.Info);
                    break;
                case "fair":
                {
                    if (args.Length < 2) { this.Monitor.Log("Usage: tly_sabotage fair <itemId> [easy|normal|hard|extreme]", LogLevel.Warn); break; }
                    TheLongestYear.Core.DifficultyStep? at = null;
                    if (args.Length > 2)
                    {
                        if (!System.Enum.TryParse(args[2], true, out TheLongestYear.Core.DifficultyStep parsed))
                        {
                            this.Monitor.Log($"Unknown level '{args[2]}'; use easy, normal, hard or extreme.", LogLevel.Warn);
                            break;
                        }
                        at = parsed;
                    }
                    this.Monitor.Log(_sabotage.Explain(args[1], at), LogLevel.Info);
                    break;
                }
                case "travelcheck":
                    TheLongestYear.Loop.MineTravelCheckCommand.Run(this.Monitor, args);
                    break;
                case "report":
                    _sabotage.ShowMorning();
                    break;
                case "arm":
                {
                    string which = args.Length > 1 ? args[1].ToLowerInvariant() : "";
                    TheLongestYear.Core.Sabotage.SabotageKind? kind = which switch
                    {
                        "blight" => TheLongestYear.Core.Sabotage.SabotageKind.Blight,
                        "revert" or "reversion" => TheLongestYear.Core.Sabotage.SabotageKind.Reversion,
                        "tamper" or "tampering" => TheLongestYear.Core.Sabotage.SabotageKind.Tampering,
                        _ => null,
                    };
                    if (kind == null) { this.Monitor.Log("Usage: tly_sabotage arm <blight [crops|chest]|revert|tamper>", LogLevel.Warn); break; }
                    string targetArg = args.Length > 2 ? args[2].ToLowerInvariant() : "";
                    TheLongestYear.Core.Sabotage.BlightTarget? blightTarget = targetArg switch
                    {
                        "crops" or "crop" => TheLongestYear.Core.Sabotage.BlightTarget.Crops,
                        "chest" or "chests" => TheLongestYear.Core.Sabotage.BlightTarget.Chests,
                        _ => null,
                    };
                    this.Monitor.Log(_sabotage.Arm(kind.Value, blightTarget), LogLevel.Info);
                    break;
                }
                case "circle":
                {
                    // Test scaffolding: place one carried Circle of Warding at (door.X-7, door.Y+6),
                    // covering the fixture's first two parsnips and its chest.
                    Farm farm = Game1.getFarm();
                    Microsoft.Xna.Framework.Vector2 door = Utility.PointToVector2(farm.GetMainFarmHouseEntry());
                    var at = new Microsoft.Xna.Framework.Vector2(door.X - 7, door.Y + 6);
                    int slot = -1;
                    for (int i = 0; i < Game1.player.Items.Count; i++)
                        if (Game1.player.Items[i]?.ItemId == TheLongestYear.Loop.CircleOfWardingService.CircleId) { slot = i; break; }
                    if (slot < 0) { this.Monitor.Log("No Circle of Warding in the inventory.", LogLevel.Warn); break; }
                    var circle = (StardewValley.Objects.Furniture)Game1.player.Items[slot];
                    Game1.player.Items[slot] = null;
                    circle.TileLocation = at;
                    farm.furniture.Add(circle);
                    this.Monitor.Log($"Circle of Warding placed at ({at.X},{at.Y}); covers x {at.X}..{at.X + 2}, y {at.Y}..{at.Y + 2}.", LogLevel.Info);
                    break;
                }
                case "aurachest":
                {
                    // Debug: open a loose chest holding every tainted item plus a Parsnip, so the
                    // dark aura can be looked at in a chest menu (no chest has to be placed). A
                    // flavoured taint also gets another flavour of the same good beside it, which
                    // must stay unmarked.
                    var loose = new StardewValley.Objects.Chest(true);
                    foreach (TheLongestYear.Core.Sabotage.TamperRecord t in _meta.Run.Tampers)
                    {
                        Item exact = TheLongestYear.Loop.SabotageService.CreateExact(t.OldItemId, t.OldFlavor);
                        if (exact != null) loose.Items.Add(exact);
                        if (string.IsNullOrEmpty(t.OldFlavor)) continue;
                        string other = OtherFlavorFor(t.OldItemId, t.OldFlavor);
                        Item contrast = other == null ? null : TheLongestYear.Loop.SabotageService.CreateExact(t.OldItemId, other);
                        if (contrast != null) loose.Items.Add(contrast);
                    }
                    loose.Items.Add(ItemRegistry.Create("(O)24", 1));
                    loose.ShowMenu();
                    break;
                }
                case "fixture":
                {
                    // Test scaffolding, and it REWRITES the ground in front of the farmhouse: it
                    // hoes over whatever is there, plants seeds and drops a chest. That is fine on a
                    // throwaway farm and ruinous on a real one, so it refuses to run unless this
                    // session made the save itself. A TLY save cannot be told apart by name:
                    // tly_newgame calls the farm after its farm TYPE ("standard", "riverland" and so
                    // on), which is exactly what a real game would be called too. So the second way
                    // in is the explicit word, and the message says so.
                    bool confirmed = args.Any(a => a.Equals("confirm", StringComparison.OrdinalIgnoreCase));
                    if (!_saveMadeThisSession && !confirmed)
                    {
                        this.Monitor.Log("tly_sabotage fixture digs up the ground in front of the farmhouse and drops a chest on it. This save was not created by tly_newgame in this session, and a throwaway farm cannot be told apart from a real one by name. Add the word 'confirm' if you really mean this save.", LogLevel.Warn);
                        break;
                    }
                    // "fixture here" is the chest on its own, beside the farmer, wherever he is
                    // standing. The thief scene plays in a shed, a cellar and the farmhouse as well
                    // as on the farm, and there is no vanilla debug command that puts a chest down.
                    if (args.Any(a => a.Equals("here", StringComparison.OrdinalIgnoreCase)))
                    {
                        GameLocation here = Game1.currentLocation;
                        Microsoft.Xna.Framework.Vector2 spot = ClearTileNear(here, Game1.player.Tile);
                        if (spot.X < 0) { this.Monitor.Log("tly_sabotage fixture here: no clear tile beside the farmer to stand a chest on.", LogLevel.Warn); break; }
                        here.objects.Remove(spot);
                        // "fixture here junimo" stands a Junimo Chest instead. Every Junimo Chest
                        // shows one shared stock; it is stocked only while empty, so a second one
                        // placed elsewhere shows the same 20 Parsnip + 10 Copper Ore.
                        bool junimo = args.Any(a => a.Equals("junimo", StringComparison.OrdinalIgnoreCase));
                        var stash = junimo ? new StardewValley.Objects.Chest(true, spot, "256") : new StardewValley.Objects.Chest(true, spot);
                        var stock = stash.GetItemsForPlayer();
                        if (!junimo || !stock.HasAny())
                        {
                            stock.Add(ItemRegistry.Create("(O)24", 20));
                            stock.Add(ItemRegistry.Create("(O)378", 10));
                        }
                        here.objects.Add(spot, stash);
                        this.Monitor.Log($"Sabotage fixture: {(junimo ? "Junimo Chest" : "chest")} at ({spot.X},{spot.Y}) on {here.NameOrUniqueName}; its stock: {string.Join(", ", stock.Where(i => i != null).Select(i => $"{i.Stack} {i.DisplayName}"))}.", LogLevel.Info);
                        break;
                    }
                    // Test scaffolding: ten crops in the ground and a chest with food and ore,
                    // on the farm just below the stash, so blight has something to take.
                    // IN-SEASON seeds: Crop.newDay kills any out-of-season outdoor crop overnight
                    // (Crop.cs:794), which would take a Summer test's parsnips before the blight
                    // report could be told apart from it. Winter has no outdoor crop; blight then
                    // only has the chest.
                    Farm farm = Game1.getFarm();
                    Microsoft.Xna.Framework.Vector2 door = Utility.PointToVector2(farm.GetMainFarmHouseEntry());
                    string seed = Game1.season switch
                    {
                        StardewValley.Season.Spring => "472",   // Parsnip Seeds
                        StardewValley.Season.Summer => "487",   // Corn Seeds (Summer and Fall)
                        StardewValley.Season.Fall => "487",
                        _ => null,
                    };
                    // "rows=<n>" makes the patch a block instead of a line. The night's blight count
                    // is a SHARE of the farm's live crops, so ten crops only ever loses one and the
                    // overnight scene then has a single crow in it. Five rows is fifty crops, which
                    // loses a handful and gives the crows scene a flock to stage. It is named rather
                    // than "any number among the arguments", which silently swallowed a typo.
                    const int MaxFixtureRows = 5;
                    int rows = 1;
                    bool badRows = false;
                    foreach (string a in args)
                    {
                        if (!a.StartsWith("rows=", StringComparison.OrdinalIgnoreCase)) continue;
                        if (!int.TryParse(a.Substring("rows=".Length), out int parsedRows) || parsedRows < 1)
                        {
                            this.Monitor.Log($"tly_sabotage fixture: '{a}' is not a row count. Use rows=<1..{MaxFixtureRows}>. Nothing was planted.", LogLevel.Warn);
                            badRows = true;
                            break;
                        }
                        rows = System.Math.Min(parsedRows, MaxFixtureRows);
                    }
                    if (badRows) break;
                    int planted = 0;
                    for (int row = 0; row < rows && seed != null; row++)
                    for (int i = 0; i < 10; i++)
                    {
                        var tile = new Microsoft.Xna.Framework.Vector2(door.X - 6 + i, door.Y + 6 + row);
                        farm.terrainFeatures.Remove(tile);
                        farm.objects.Remove(tile);
                        var dirt = new StardewValley.TerrainFeatures.HoeDirt(1, farm);
                        farm.terrainFeatures.Add(tile, dirt);
                        dirt.crop = new Crop(seed, (int)tile.X, (int)tile.Y, farm);
                        planted++;
                    }
                    // Beside the block, never inside it: a chest standing on a planted tile would be
                    // dug up by the next fixture and confuses a crop count with a chest count.
                    var chestTile = new Microsoft.Xna.Framework.Vector2(door.X + 5, door.Y + 6);
                    farm.objects.Remove(chestTile);
                    farm.terrainFeatures.Remove(chestTile);
                    var chest = new StardewValley.Objects.Chest(true, chestTile);
                    chest.Items.Add(ItemRegistry.Create("(O)24", 20));
                    chest.Items.Add(ItemRegistry.Create("(O)378", 10));
                    farm.objects.Add(chestTile, chest);
                    // Nothing tall in front of it, so the thief scene can show it (designer,
                    // 2026-10-08: the fixture chest was behind a tree).
                    int clearedInFront = TheLongestYear.Scenes.ScenePeekThrough.ClearInFrontOf(farm, ((int)chestTile.X, (int)chestTile.Y));
                    // "fixture scarecrow" also stands one at the head of the row, for the crows
                    // scene: it needs something within twelve tiles of the crops that scares birds.
                    string scarecrowAt = "none";
                    if (args.Any(a => a.Equals("scarecrow", StringComparison.OrdinalIgnoreCase)))
                    {
                        var scarecrowTile = new Microsoft.Xna.Framework.Vector2(door.X - 8, door.Y + 6);
                        farm.objects.Remove(scarecrowTile);
                        farm.terrainFeatures.Remove(scarecrowTile);
                        var scarecrow = (StardewValley.Object)ItemRegistry.Create("(BC)8");
                        scarecrow.TileLocation = scarecrowTile;
                        farm.objects.Add(scarecrowTile, scarecrow);
                        scarecrowAt = $"({scarecrowTile.X},{scarecrowTile.Y})";
                        // "scarecrows=2" stands a second one on the far side of the patch, below the
                        // chest, so the scene's one-perch pick can be watched with two in frame.
                        if (args.Any(a => a.Equals("scarecrows=2", StringComparison.OrdinalIgnoreCase)))
                        {
                            var secondTile = new Microsoft.Xna.Framework.Vector2(door.X + 5, door.Y + 6 + rows + 1);
                            farm.objects.Remove(secondTile);
                            farm.terrainFeatures.Remove(secondTile);
                            var second = (StardewValley.Object)ItemRegistry.Create("(BC)8");
                            second.TileLocation = secondTile;
                            farm.objects.Add(secondTile, second);
                            scarecrowAt += $" and ({secondTile.X},{secondTile.Y})";
                        }
                    }
                    this.Monitor.Log($"Sabotage fixture: {planted} crop(s) of seed {seed ?? "none (Winter)"} at row {door.Y + 6}, chest at ({chestTile.X},{chestTile.Y}) with 20 Parsnip + 10 Copper Ore ({clearedInFront} tree(s) or bush(es) in front of it cleared), scarecrow {scarecrowAt}.", LogLevel.Info);
                    break;
                }
                case "giftbox":
                {
                    // Debug: open the farmhouse's starter gift box as the player would (its items go
                    // to the inventory and the box goes), so a handoff farm's chest count is only what
                    // the fixture put down: the thief's draw counts a gift box in the farmhouse.
                    var house = Utility.getHomeOfFarmer(Game1.player);
                    int opened = 0, items = 0;
                    foreach (Microsoft.Xna.Framework.Vector2 tile in house.objects.Keys.ToList())
                    {
                        if (house.objects[tile] is not StardewValley.Objects.Chest box || !box.giftbox.Value) continue;
                        foreach (Item item in box.Items.Where(i => i != null).ToList())
                        {
                            items += item.Stack;
                            Game1.player.addItemToInventory(item);
                        }
                        house.objects.Remove(tile);
                        opened++;
                    }
                    this.Monitor.Log($"tly_sabotage giftbox: opened {opened} gift box(es) in the farmhouse, {items} item(s) to the inventory.", LogLevel.Info);
                    break;
                }
                case "edge":
                {
                    // Debug: take the current map's own warp onto the Farm, the same call vanilla
                    // makes when the farmer walks onto the edge tile (GameLocation.checkForWarp ->
                    // Farmer.warpFarmer), so the Bus Stop, Forest or Backwoods edge can be crossed
                    // headless (Jeff, 2026-10-07: the tamper scene plays on any arrival).
                    GameLocation from = Game1.currentLocation;
                    Warp edge = from?.warps?.FirstOrDefault(w => w != null && w.TargetName == TheLongestYear.Core.Sabotage.TamperPorchRule.FarmLocationName);
                    if (edge == null) { this.Monitor.Log($"tly_sabotage edge: {from?.NameOrUniqueName ?? "nowhere"} has no warp onto the Farm.", LogLevel.Warn); break; }
                    this.Monitor.Log($"tly_sabotage edge: {from.NameOrUniqueName} ({edge.X},{edge.Y}) -> Farm ({edge.TargetX},{edge.TargetY}).", LogLevel.Info);
                    Game1.player.warpFarmer(edge, Game1.player.FacingDirection);
                    break;
                }
                case "breakscene":
                {
                    // Debug: break a strike scene on purpose so the "broken scene lands bare" rule
                    // (Jeff, 2026-10-08) can be watched. 'pick' (default) fails the pick-time
                    // staging check; 'setup' makes setUp throw before staging. In memory only.
                    string what = args.Length > 1 ? args[1].ToLowerInvariant() : "";
                    if (what == "off") { TheLongestYear.Scenes.SceneBreakSwitch.Clear(); this.Monitor.Log($"breakscene: {TheLongestYear.Scenes.SceneBreakSwitch.Describe()}", LogLevel.Info); break; }
                    TheLongestYear.Core.Sabotage.DarknessEvent? broken = what switch
                    {
                        "crows" => TheLongestYear.Core.Sabotage.DarknessEvent.CropBlight,
                        "thief" => TheLongestYear.Core.Sabotage.DarknessEvent.ChestBlight,
                        "hall" => TheLongestYear.Core.Sabotage.DarknessEvent.Reversion,
                        "cloud" => TheLongestYear.Core.Sabotage.DarknessEvent.Tampering,
                        _ => null,
                    };
                    if (broken == null) { this.Monitor.Log($"Usage: tly_sabotage breakscene <crows|thief|hall|cloud> [pick|setup] | breakscene off. Now {TheLongestYear.Scenes.SceneBreakSwitch.Describe()}", LogLevel.Warn); break; }
                    TheLongestYear.Scenes.SceneBreakSwitch.Break(broken.Value, atSetUp: args.Length > 2 && args[2].ToLowerInvariant() == "setup");
                    this.Monitor.Log($"breakscene: {TheLongestYear.Scenes.SceneBreakSwitch.Describe()}", LogLevel.Info);
                    break;
                }
                case "scene" when args.Length > 1 && args[1].ToLowerInvariant() == "crows":
                    this.PlayCrowsScenePreview(rng);
                    break;
                case "scene" when args.Length > 1 && args[1].ToLowerInvariant() == "thief":
                    this.PlayThiefScenePreview(rng);
                    break;
                case "scene" when args.Length > 1 && args[1].ToLowerInvariant() == "hall":
                    this.PlayHallScenePreview(rng);
                    break;
                case "scene" when args.Length > 1 && args[1].ToLowerInvariant() == "cloud":
                    this.PlayCloudScenePreview(rng, dayOfYear);
                    break;
                case "scene":
                    // The Junimos' "tainted" scene, staged at the porch; the farmer goes back to his
                    // tile after, as on a real arrival.
                    if (!_seasonTurnDriver.StartTamperAtPorch(
                        args.Length > 1 ? args[1] : "Parsnips",
                        args.Length > 3 && args[3].ToLowerInvariant() == "mass",
                        args.Length > 2 ? args[2] : "Crystal Fruit", false,
                        () => this.Monitor.Log("Darkness: scene replay finished.", LogLevel.Info)))
                        this.Monitor.Log("tly_sabotage scene: step out onto the Farm first, with no event up.", LogLevel.Warn);
                    break;
                default:
                    this.Monitor.Log("Usage: tly_sabotage status | arm <blight|revert|tamper> | blight [crops] [spoil] | revert | tamper | fair <itemId> [level] | travelcheck [save] | report | scene crows | scene thief | scene hall | scene cloud | scene [old] [new] [mass] | fixture [scarecrow [scarecrows=2]] [rows=<n>] [here [junimo]] [confirm] | edge | giftbox | circle | breakscene <crows|thief|hall|cloud|off> [pick|setup]", LogLevel.Info);
                    break;
            }
        }

        /// <summary>tly_sabotage scene crows: watch the crop blight scene now, without sleeping.
        ///
        /// It plays against a REAL fresh pick when the farm has crops, and those crops really die at
        /// the scene's own beat, the same as they would overnight. With no crops to take it plays
        /// against a harmless stand-in (the tiles around the farmer, with an effect that does
        /// nothing) so the staging, the camera and Linus can still be watched.</summary>
        private void PlayCrowsScenePreview(System.Random rng)
        {
            _scenePreview ??= new TheLongestYear.Scenes.ScenePreview(this.Helper, this.Monitor);
            if (_scenePreview.Playing) { this.Monitor.Log("A strike scene is already playing.", LogLevel.Warn); return; }

            TheLongestYear.Core.DifficultyStep level = _meta.State.EffectiveDifficulty(_config).Darkness;
            int want = Math.Max(TheLongestYear.Scenes.CrowsScene.PreviewCrops, TheLongestYear.Loop.BlightPass.CountFor(_meta.Run.Season, level));
            List<Microsoft.Xna.Framework.Vector2> tiles = TheLongestYear.Loop.BlightPass.Pick(want, rng);
            bool real = tiles.Count > 0;
            if (!real) tiles = StandInCropTiles();
            if (tiles.Count == 0) { this.Monitor.Log("Nowhere on the farm to stage the crows.", LogLevel.Warn); return; }

            var strike = new TheLongestYear.Loop.PendingStrike(
                TheLongestYear.Core.Sabotage.DarknessEvent.CropBlight,
                () => real && TheLongestYear.Loop.BlightPass.Kill(tiles) > 0)
            { CropTiles = tiles };
            this.Monitor.Log(
                real
                    ? $"tly_sabotage scene crows: {tiles.Count} live crop(s) picked, and they really die at the peck."
                    : $"tly_sabotage scene crows: no crops on the farm, so the scene plays against {tiles.Count} stand-in tile(s) and nothing dies.",
                LogLevel.Info);
            var scene = new TheLongestYear.Scenes.CrowsScene(strike, skippable: true, this.Monitor, _ => { });
            _scenePreview.Play(scene);
        }

        /// <summary>The nearest tile to <paramref name="from"/> that a chest can be stood on, for
        /// <c>tly_sabotage fixture here</c>. Searches outward in rings and gives up at four tiles,
        /// returning (-1,-1).</summary>
        private static Microsoft.Xna.Framework.Vector2 ClearTileNear(GameLocation where, Microsoft.Xna.Framework.Vector2 from)
        {
            const int Reach = 4;
            for (int ring = 0; ring <= Reach; ring++)
                for (int dx = -ring; dx <= ring; dx++)
                    for (int dy = -ring; dy <= ring; dy++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != ring) continue;
                        int x = (int)from.X + dx, y = (int)from.Y + dy;
                        if (TheLongestYear.Scenes.SceneGround.CanStandOn(where, x, y))
                            return new Microsoft.Xna.Framework.Vector2(x, y);
                    }
            return new Microsoft.Xna.Framework.Vector2(-1, -1);
        }

        /// <summary>tly_sabotage scene thief: watch the chest blight scene now, without sleeping.
        ///
        /// There is no stand-in here, unlike the crows. A thief with nothing to steal is not a
        /// scene, so with nothing stored it says so and does nothing. With something stored it makes
        /// a REAL fresh pick and those units really go at the scene's own beat, exactly as they
        /// would overnight, warded chests excluded by the pick itself.</summary>
        private void PlayThiefScenePreview(System.Random rng)
        {
            _scenePreview ??= new TheLongestYear.Scenes.ScenePreview(this.Helper, this.Monitor);
            if (_scenePreview.Playing) { this.Monitor.Log("A strike scene is already playing.", LogLevel.Warn); return; }

            TheLongestYear.Core.DifficultyStep level = _meta.State.EffectiveDifficulty(_config).Darkness;
            bool everything = TheLongestYear.Core.Sabotage.DarknessLevels.StorageReachesEverything(level);
            int units = TheLongestYear.Loop.SpoilagePass.StoredUnits(everything, thiefSceneDue: true);
            int want = Math.Max(1, TheLongestYear.Core.Sabotage.BlightRule.SpoilCount(units, _meta.Run.Season, level));
            List<TheLongestYear.Loop.SpoilagePass.Hit> hits = units > 0
                ? TheLongestYear.Loop.SpoilagePass.Plan(want, rng, everything, thiefSceneDue: true)
                : new List<TheLongestYear.Loop.SpoilagePass.Hit>();
            if (hits.Count == 0)
            {
                this.Monitor.Log("tly_sabotage scene thief: nothing stored where the scene can show it (the farm, a shed, the cellar or the farmhouse), no thief.", LogLevel.Info);
                return;
            }

            var strike = new TheLongestYear.Loop.PendingStrike(
                TheLongestYear.Core.Sabotage.DarknessEvent.ChestBlight,
                () => TheLongestYear.Loop.SpoilagePass.Apply(hits).Total > 0)
            { Hits = hits };
            if (TheLongestYear.Scenes.ThiefScene.SceneTargetOnFarm(strike) == null)
            {
                this.Monitor.Log("tly_sabotage scene thief: tonight's pick is off the farm's own maps, so there is nothing to film. Put something in a chest on the farm, in a shed, in the cellar or in the farmhouse.", LogLevel.Warn);
                return;
            }
            this.Monitor.Log($"tly_sabotage scene thief: {hits.Count} unit(s) picked from {units} stored, and they really go when the lid opens.", LogLevel.Info);
            var scene = new TheLongestYear.Scenes.ThiefScene(strike, skippable: true, this.Monitor, _ => { });
            _scenePreview.Play(scene);
        }

        /// <summary>tly_sabotage scene hall: watch the reversion scene now, without sleeping.
        ///
        /// With a slot the fairness rule will let the darkness take, the pick is REAL and that slot
        /// really comes undone at the jump, exactly as it would overnight. With no candidate the
        /// scene still plays, against an effect that does nothing, because the Community Center
        /// front, the firelight and Shane are worth watching whether or not there is anything on the
        /// board to lose. The log says which of the two it was.</summary>
        private void PlayHallScenePreview(System.Random rng)
        {
            _scenePreview ??= new TheLongestYear.Scenes.ScenePreview(this.Helper, this.Monitor);
            if (_scenePreview.Playing) { this.Monitor.Log("A strike scene is already playing.", LogLevel.Warn); return; }

            TheLongestYear.Loop.PendingStrike strike = _sabotage.PrepareRevert(rng);
            if (strike == null)
            {
                this.Monitor.Log("tly_sabotage scene hall: no slot may fairly come undone tonight, so the scene plays and nothing is taken.", LogLevel.Info);
                strike = new TheLongestYear.Loop.PendingStrike(TheLongestYear.Core.Sabotage.DarknessEvent.Reversion, () => false);
            }
            else
            {
                this.Monitor.Log("tly_sabotage scene hall: a real slot is picked, and it really comes undone at the jump.", LogLevel.Info);
            }
            var scene = new TheLongestYear.Scenes.HallScene(strike, skippable: true, this.Monitor, _ => { });
            _scenePreview.Play(scene);
        }

        /// <summary>tly_sabotage scene cloud: watch the tampering scene now, without sleeping.
        ///
        /// With an unfilled slot that has a fair replacement, the plan is REAL and the board really
        /// is rewritten on the hold, exactly as it would be overnight, so the next morning plays the
        /// Junimos' porch scene and the popup. With no plan the scene still plays, against an effect
        /// that does nothing, because the map and the cloud are worth watching either way. The log
        /// says which of the two it was.</summary>
        private void PlayCloudScenePreview(System.Random rng, int dayOfYear)
        {
            _scenePreview ??= new TheLongestYear.Scenes.ScenePreview(this.Helper, this.Monitor);
            if (_scenePreview.Playing) { this.Monitor.Log("A strike scene is already playing.", LogLevel.Warn); return; }

            TheLongestYear.Loop.PendingStrike strike = _sabotage.PrepareTamper(rng, dayOfYear);
            if (strike == null)
            {
                this.Monitor.Log("tly_sabotage scene cloud: no slot may fairly be rewritten tonight, so the scene plays and the board is left alone.", LogLevel.Info);
                strike = new TheLongestYear.Loop.PendingStrike(TheLongestYear.Core.Sabotage.DarknessEvent.Tampering, () => false);
            }
            else
            {
                this.Monitor.Log("tly_sabotage scene cloud: a real tamper is planned, and the board really changes on the hold. Sleep to see the Junimos.", LogLevel.Info);
            }
            var scene = new TheLongestYear.Scenes.CloudScene(strike, skippable: true, this.Monitor, _ => { });
            _scenePreview.Play(scene);
        }

        /// <summary>Tiles to stage the crows on when the farm has no crops at all: the ring around
        /// the farmer if he is standing on the farm, else the ring below the farmhouse door.</summary>
        private static List<Microsoft.Xna.Framework.Vector2> StandInCropTiles()
        {
            Farm farm = Game1.getFarm();
            var tiles = new List<Microsoft.Xna.Framework.Vector2>();
            if (farm == null) return tiles;
            Microsoft.Xna.Framework.Vector2 centre = Game1.currentLocation is Farm
                ? Game1.player.Tile
                : Utility.PointToVector2(farm.GetMainFarmHouseEntry()) + new Microsoft.Xna.Framework.Vector2(0, 3);
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    var tile = new Microsoft.Xna.Framework.Vector2(centre.X + dx, centre.Y + dy);
                    if (farm.isTileOnMap(tile)) tiles.Add(tile);
                }
            return tiles;
        }

        /// <summary>After the darkness rewrites a bundle on the live board (and in the stored
        /// copy), re-derive everything that was built from the board: the CcItem catalog, the
        /// requirements (through the same resolve path save load uses, so the manifest check runs
        /// against the updated stored board), the fingerprint the Vanilla-mode watcher compares,
        /// and the non-object slot flag.</summary>
        private void RebuildBoardDerivedState(string reason)
        {
            if (_boardBuilder == null || _runController == null) return;
            _catalog = _boardBuilder.Build();
            _requirements = ResolveRequirements(_boardBuilder, ParseItemSeasonPins(), ParseBundleQuotas());
            _boardFingerprint = BoardInspection.Fingerprint(Game1.netWorldState.Value.BundleData);
            _runController.ReplaceCatalog(_catalog);
            _runController.ReplaceRequirements(_requirements);
            TheLongestYear.Patches.BundleDonationPatches.LiveBoardHasNonObjectSlots =
                BoardInspection.HasNonObjectIngredients(Game1.netWorldState.Value.BundleData);
            this.Monitor.Log($"Board re-derived after {reason}: {_requirements.Count} bundles, {_catalog.Count} catalog items.", LogLevel.Info);
        }

        private void CmdSeasonTurn(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (args.Length < 1 || !TheLongestYear.Core.SeasonTurn.TryParse(args[0], out var kind))
            {
                this.Monitor.Log("Usage: tly_seasonturn <summer|fall|winter>", LogLevel.Warn);
                return;
            }
            _seasonTurnDriver?.StartNow(kind);
        }

        /// <summary>Debug: replay the Year One Ending event right now with no continuation, optionally
        /// forcing which villager cracks. See <see cref="Integration.EndingEventDriver.StartNow"/>.</summary>
        private void CmdEnding(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            string speaker = args.Length >= 2 && args[0] == "speaker" ? args[1] : null;
            _endingDriver?.StartNow(speaker);
        }
        /// <summary>Debug: the shrine's Restart the year button without the mouse.</summary>
        private void CmdRestart(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _runController?.AskVoluntaryRestart();
        }

        /// <summary>Full reset: rebuild the world (PerformReset), wipe RunState (BeginNewRun),
        /// and fire the Spring 1 hub. Used by both <see cref="ForceReset"/> and
        /// <see cref="ResetIfNameMatches"/>.
        ///
        /// 2026-05-26 round-2 bug: log showed a deferred SaveLoaded event firing AFTER this
        /// method returned, which called <c>_meta.Load()</c> and overwrote our in-memory
        /// BeginNewRun with the stale on-disk state ("the reset didn't remove the foraging
        /// items I had donated"). Fix: commit the cleared state to disk immediately after
        /// BeginNewRun so the subsequent SaveLoaded's Load reads the post-reset state.</summary>
        private void FullResetAndPresentOffer()
        {
            // Tech-debt consolidation (2026-06-10): the debug reset is now a thin alias for THE
            // shared finalizer (RunController.FinalizeReset) instead of a hand-copied subset. That
            // makes tly_reset a faithful stand-in for the real fail-day-28 reset (it previously
            // skipped ActiveEffectsProvider.Clear — leaking the old theme's effects — and
            // ForceFullSave, and presented the offer via PresentOffer(1) instead of the real
            // day-start flow). Cross-cutting reset fixes now land once, in FinalizeReset.
            if (_runController == null)
            {
                this.Monitor.Log("Reset unavailable: no run controller (load a save first).", LogLevel.Warn);
                return;
            }
            // A planning hub still up when tly_reset fires survives the in-place reset and then
            // blocks the new run's week-1 offer for good: the hub only opens over a clear
            // activeClickableMenu, and a stale hub never closes itself (readyToClose waits for a
            // pick). Drop it here; the reset re-presents week 1 from the real day-start flow.
            if (Game1.activeClickableMenu is TheLongestYear.UI.WeeklyHubMenu)
            {
                this.Monitor.Log("tly_reset: closing the open planning hub so the new run's week-1 offer can open.", LogLevel.Info);
                Game1.exitActiveMenu();
            }
            _runController.FinalizeReset("debug tly_reset");
        }

        private void LeakTest(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            _reset.PerformReset();
            _reset.ProfessionPicker.DrainOnDayStart();
            var first = WorldStateProbe.Capture();

            _reset.PerformReset();
            _reset.ProfessionPicker.DrainOnDayStart();
            var second = WorldStateProbe.Capture();

            this.Monitor.Log(
                $"Leak test object counts (informational, non-deterministic world-gen): {first.PlacedObjectCount} vs {second.PlacedObjectCount}.",
                LogLevel.Info);

            var diff = first.Diff(second);
            if (diff.Count == 0)
            {
                this.Monitor.Log("Leak test PASSED: two consecutive resets produced an identical baseline.", LogLevel.Info);
            }
            else
            {
                this.Monitor.Log($"Leak test FAILED: {diff.Count} field(s) leaked between runs:", LogLevel.Error);
                foreach (string d in diff)
                    this.Monitor.Log($"  - {d}", LogLevel.Error);
            }
        }
    }
}
