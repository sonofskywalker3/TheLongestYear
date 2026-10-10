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
        private void CmdBuildings(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            Farm farm = Game1.getFarm();
            var rows = farm.buildings
                .OrderBy(b => b.tileY.Value).ThenBy(b => b.tileX.Value)
                .Select(b => $"{b.buildingType.Value}@({b.tileX.Value},{b.tileY.Value})");
            this.Monitor.Log(
                $"Buildings farm={Game1.whichFarm}/{Game1.GetFarmTypeID()} n={farm.buildings.Count}: {string.Join(" ", rows)}",
                LogLevel.Info);
        }

        /// <summary>Debug: clean slate for a Keep-Horse carryover test. Demolishes every Stable on
        /// the Farm (removing its horse), clears <see cref="MetaState.HorseState"/> so the snapshot
        /// isn't restored, and drops the <c>early_horse</c> upgrade so the shrine shop re-offers
        /// "Keep Horse". Buy it again + build a stable to test carryover with a real, named horse.</summary>
        private void CmdRemoveHorse(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }

            int removed = 0;
            StardewValley.Farm farm = Game1.getFarm();
            if (farm != null)
            {
                foreach (StardewValley.Buildings.Stable stable in farm.buildings.OfType<StardewValley.Buildings.Stable>().ToList())
                {
                    StardewValley.Characters.Horse horse = stable.getStableHorse();
                    if (horse != null)
                        farm.characters.Remove(horse);
                    farm.buildings.Remove(stable);
                    removed++;
                }
            }

            _meta.State.HorseState = null;
            bool hadUpgrade = _meta.State.OwnedUpgrades.Remove(TheLongestYear.Loop.HorseCarryoverService.UpgradeId);
            this.Monitor.Log(
                $"tly_removehorse: demolished {removed} stable(s), cleared HorseState, " +
                $"Keep Horse upgrade {(hadUpgrade ? "removed (re-buyable)" : "was not owned")}. " +
                "Persists on next save.",
                LogLevel.Info);
        }

        /// <summary>Print the player's current tile coordinate. Used for tuning interactable
        /// tile coords (e.g. finding the fireplace before running tly_setboard).</summary>
        private void CmdHere(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            int x = (int)Game1.player.Tile.X;
            int y = (int)Game1.player.Tile.Y;
            string loc = Game1.currentLocation?.Name ?? "?";
            this.Monitor.Log(
                $"Player at tile ({x}, {y}) in '{loc}' (dialogueUp={Game1.dialogueUp}, " +
                $"menu={Game1.activeClickableMenu?.GetType().Name ?? "none"}).", LogLevel.Info);
        }

        private void CmdSetStash(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (Game1.currentLocation is not Farm)
            {
                this.Monitor.Log("tly_setstash: stand on the Farm first.", LogLevel.Warn);
                return;
            }
            int dx = Game1.player.FacingDirection == 1 ? 1 : Game1.player.FacingDirection == 3 ? -1 : 0;
            int dy = Game1.player.FacingDirection == 2 ? 1 : Game1.player.FacingDirection == 0 ? -1 : 0;
            _config.StashTileX = (int)Game1.player.Tile.X + dx;
            _config.StashTileY = (int)Game1.player.Tile.Y + dy;
            this.Helper.WriteConfig(_config);
            this.Monitor.Log(
                $"Junimo Stash anchored to ({_config.StashTileX}, {_config.StashTileY}). Saved to config.json.",
                LogLevel.Info);
            // Immediately re-place the chest at the new tile.
            _stashService?.PlaceChest();
            _stashService?.PopulateFromMeta();
        }

        /// <summary>Debug: add a pet (or list them). Smoke scaffolding for Keep Pet with several
        /// pets (Nexus bug 1122901): the throwaway save has none, and vanilla adoption needs Marnie.</summary>
        private void CmdAddPet(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (args.Length == 0 || args[0] == "check")
            {
                foreach (StardewValley.Characters.Pet pet in Utility.getAllPets())
                {
                    var bowl = pet.GetPetBowl();
                    this.Monitor.Log(
                        $"tly_addpet: '{pet.Name}' ({pet.petType.Value}) in {pet.currentLocation?.Name ?? "?"} at " +
                        $"({pet.Tile.X},{pet.Tile.Y}), friendship {pet.friendshipTowardFarmer.Value}, " +
                        $"bowl={(bowl == null ? "NONE" : $"({bowl.tileX.Value},{bowl.tileY.Value})")}.", LogLevel.Info);
                }
                this.Monitor.Log($"tly_addpet: {Utility.getAllPets().Count} pet(s), " +
                    $"{Game1.getFarm().buildings.OfType<StardewValley.Buildings.PetBowl>().Count()} bowl(s) on the Farm.", LogLevel.Info);
                return;
            }
            string type = args[0];
            string name = args.Length > 1 ? args[1] : type;
            string breed = args.Length > 2 ? args[2] : "0";
            var farm = Game1.getFarm();
            var added = new StardewValley.Characters.Pet(54, 8, breed, type) { Name = name, displayName = name };
            farm.characters.Add(added);
            this.Monitor.Log($"tly_addpet: added {type} '{name}' (breed {breed}) on the Farm.", LogLevel.Info);
        }

        /// <summary>Debug: repair the beach bridge in place, or report its state. Smoke scaffolding
        /// for Nexus bug 1124076 (the rewind must put the broken bridge back).</summary>
        private void CmdFixBridge(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (Game1.getLocationFromName("Beach") is not StardewValley.Locations.Beach beach)
            {
                this.Monitor.Log("tly_fixbridge: no Beach location loaded.", LogLevel.Warn);
                return;
            }
            if (args.Length == 0)
            {
                beach.bridgeFixed.Value = true;   // fieldChangeEvent runs Beach.fixBridge on the live map
                this.Monitor.Log("tly_fixbridge: bridgeFixed set; vanilla edited the Beach map in place.", LogLevel.Info);
            }
            int tile = beach.getTileIndexAt(58, 13, "Buildings");
            bool hasAction = beach.doesTileHaveProperty(58, 13, "Action", "Buildings") != null;
            this.Monitor.Log(
                $"tly_fixbridge: bridgeFixed={beach.bridgeFixed.Value}, Buildings tile (58,13)={tile} " +
                $"(284 = broken, 301 = repaired), Action property {(hasAction ? "present" : "MISSING")}, " +
                $"walkable={beach.isTilePassable(new Microsoft.Xna.Framework.Vector2(59, 13))}.", LogLevel.Info);
        }

        /// <summary>Debug: put a fully loaded rod in the stash, or list stashed tools' state. Smoke
        /// scaffolding for the 0.16.1/0.16.2 stash fixes.</summary>
        /// <summary>Debug: vanilla one-time gift boxes (Book_Trash in Town, Book_Marlon in the Guild).
        /// 'warp' puts the farmer next to the tile so the location's entry spawns the box; a call with
        /// no mode reports the box, its contents and the mail flag; 'open' opens it like a click.</summary>
        private void CmdGiftBox(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (args.Length < 3 || !int.TryParse(args[1], out int x) || !int.TryParse(args[2], out int y))
            { this.Monitor.Log("Usage: tly_giftbox <Location> <x> <y> [warp|open]", LogLevel.Warn); return; }
            string mode = args.Length > 3 ? args[3] : "report";
            GameLocation loc = Game1.getLocationFromName(args[0]);
            if (loc == null) { this.Monitor.Log($"tly_giftbox: no location '{args[0]}'.", LogLevel.Warn); return; }
            if (mode == "warp") { Game1.warpFarmer(loc.Name, x, y + 1, 0); this.Monitor.Log($"tly_giftbox: warping to {loc.Name} ({x},{y + 1}).", LogLevel.Info); return; }
            string flag = loc.Name + "_giftbox_" + x + "_" + y;
            loc.overlayObjects.TryGetValue(new Microsoft.Xna.Framework.Vector2(x, y), out StardewValley.Object obj);
            var chest = obj as StardewValley.Objects.Chest;
            string items = chest == null ? "-" : string.Join(",", chest.Items.Where(i => i != null).Select(i => i.QualifiedItemId));
            if (mode == "open" && chest != null && chest.giftbox.Value)
            {
                chest.checkForAction(Game1.player);
                this.Monitor.Log($"tly_giftbox: opened the box at {loc.Name} ({x},{y}).", LogLevel.Info);
            }
            bool hasBook = Game1.player.Items.Any(i => i != null && items.Contains(i.QualifiedItemId));
            this.Monitor.Log(
                $"tly_giftbox {loc.Name} ({x},{y}): box={(chest != null && chest.giftbox.Value)} items=[{items}] mailFlag={Game1.player.mailReceived.Contains(flag)} " +
                $"guildMember={Game1.player.mailReceived.Contains("guildMember")} playerLoc={Game1.currentLocation?.Name} inventoryHasIt={hasBook}",
                LogLevel.Info);
        }

        /// <summary>Debug: open the stash and log what the menu carries (0.18.6 verification).</summary>
        private void CmdStashMenu(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            var chest = _stashService?.FindStashChest();
            if (chest == null) { this.Monitor.Log("tly_stashmenu: no stash chest.", LogLevel.Warn); return; }
            if (args.Length > 0 && args[0] == "name")
            {
                chest.modData[JunimoStashService.ChestsAnywhereModDataPrefix + "Name"] = args.Length > 1 ? args[1] : "Junimo";
                this.Monitor.Log("tly_stashmenu: stamped a Chests Anywhere name on the stash.", LogLevel.Info);
                return;
            }
            chest.ShowMenu();
            var menu = Game1.activeClickableMenu as ItemGrabMenu;
            string keys = string.Join(",", chest.modData.Keys.Where(k => k.StartsWith("Pathoschild.", StringComparison.Ordinal)).Select(k => k + "=" + chest.modData[k]));
            this.Monitor.Log(
                $"tly_stashmenu: menu={(Game1.activeClickableMenu?.GetType().Name ?? "none")} context={(menu?.context == null ? "null" : menu.context.GetType().Name)} " +
                $"source={(menu?.sourceItem?.GetType().Name ?? "null")} overhaulLoaded={JunimoStashService.StorageOverhaulLoaded} " +
                $"chestsAnywhere={this.Helper.ModRegistry.IsLoaded("Pathoschild.ChestsAnywhere")} caKeys=[{keys}]",
                LogLevel.Info);
        }

        /// <summary>Debug: would the Community Center note let the player pick up this item (0.18.7)?</summary>
        private void CmdRingTest(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            string id = args.Length > 0 ? args[0] : "(O)529";
            Item item = ItemRegistry.Create(id, 1);
            bool vanilla = Utility.highlightSmallObjects(item);
            var cc = Game1.RequireLocation<StardewValley.Locations.CommunityCenter>("CommunityCenter");
            for (int area = 0; area <= 5; area++)
            {
                var note = new JunimoNoteMenu(area, cc.bundlesDict());
                Bundle asking = note.bundles.FirstOrDefault(b => b.ingredients.Any(ing => !ing.completed && ing.id == id));
                if (asking == null) { note.exitThisMenu(false); continue; }
                Game1.activeClickableMenu = note;
                AccessTools.Method(typeof(JunimoNoteMenu), "setUpBundleSpecificPage").Invoke(note, new object[] { asking });
                bool live = note.inventory.highlightMethod(item);
                this.Monitor.Log(
                    $"tly_ringtest [{id}] type={item.GetType().Name} bundle={asking.name} (area {area}) vanillaHighlight={vanilla} liveHighlight={live} " +
                    $"liveBoardHasNonObjectSlots={TheLongestYear.Patches.BundleDonationPatches.LiveBoardHasNonObjectSlots} enableNonObjectDonations={_config.EnableNonObjectDonations}",
                    live ? LogLevel.Info : LogLevel.Warn);
                Game1.exitActiveMenu();
                return;
            }
            this.Monitor.Log($"tly_ringtest [{id}] type={item.GetType().Name} vanillaHighlight={vanilla}: no bundle on the live board asks for it.", LogLevel.Info);
        }

        private void CmdStashRod(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            var chest = _stashService?.FindStashChest();
            if (chest == null)
            {
                this.Monitor.Log("tly_stashrod: no stash chest found. Own stash_1 and reset/reload first.", LogLevel.Warn);
                return;
            }
            if (args.Length > 0 && args[0] == "check")
            {
                foreach (Item item in chest.Items)
                {
                    if (item is not Tool tool) continue;
                    string slots = string.Join(", ", tool.attachments.Select((a, i) => $"[{i}]={(a == null ? "empty" : $"{a.QualifiedItemId} x{a.Stack}")}"));
                    string ench = string.Join(", ", tool.enchantments.Select(e => $"{e.GetType().Name} L{e.GetLevel()}"));
                    string stats = tool is StardewValley.Tools.MeleeWeapon w
                        ? $"; damage {w.minDamage.Value}-{w.maxDamage.Value}, defense {w.addedDefense.Value}, speed {w.speed.Value}"
                        : "";
                    this.Monitor.Log($"tly_stashrod: {tool.QualifiedItemId} slots {slots}; enchantments [{ench}]{stats}.", LogLevel.Info);
                }
                this.Monitor.Log($"tly_stashrod: {chest.Items.Count(i => i != null)} item(s) in the stash chest.", LogLevel.Info);
                return;
            }
            if (args.Length > 0 && args[0] == "weapon")
            {
                // Nexus bug (Bumblewyn, 2026-08-28): a weapon's innate enchantment + forged gem
                // vanish across the loop. Stash a Galaxy Sword with Attack II (innate, secondary)
                // and a Ruby forge (IsForge) the way vanilla adds them, then compare `check`
                // before and after tly_reset.
                var weapon = ItemRegistry.Create("(W)4") as StardewValley.Tools.MeleeWeapon;
                if (weapon == null) { this.Monitor.Log("tly_stashrod: could not create (W)4.", LogLevel.Warn); return; }
                weapon.AddEnchantment(new StardewValley.Enchantments.AttackEnchantment());
                weapon.AddEnchantment(new StardewValley.Enchantments.AttackEnchantment());
                weapon.AddEnchantment(new StardewValley.Enchantments.RubyEnchantment());
                chest.Items.Add(weapon);
                string wench = string.Join(", ", weapon.enchantments.Select(e => $"{e.GetType().Name} L{e.GetLevel()}"));
                this.Monitor.Log(
                    $"tly_stashrod: stashed a Galaxy Sword with [{wench}]; damage {weapon.minDamage.Value}-{weapon.maxDamage.Value}.",
                    LogLevel.Info);
                return;
            }
            var rod = ItemRegistry.Create("(T)IridiumRod") as Tool;
            if (rod == null) { this.Monitor.Log("tly_stashrod: could not create (T)IridiumRod.", LogLevel.Warn); return; }
            if (rod.attachments.Count > 0) rod.attachments[0] = ItemRegistry.Create<StardewValley.Object>("(O)685", 20);
            if (rod.attachments.Count > 1) rod.attachments[1] = ItemRegistry.Create<StardewValley.Object>("(O)686");
            var hook = new StardewValley.Enchantments.AutoHookEnchantment();
            rod.enchantments.Add(hook);
            hook.ApplyTo(rod);
            chest.Items.Add(rod);
            this.Monitor.Log($"tly_stashrod: stashed an Iridium Rod with {rod.attachments.Count} slot(s) filled and Auto-Hook.", LogLevel.Info);
        }

        private void CmdStashClear(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _meta.State.StashItems.Clear();
            var chest = _stashService?.FindStashChest();
            if (chest != null)
                chest.Items.Clear();
            this.Monitor.Log("Junimo Stash MetaState cleared (in memory — persists on next save).", LogLevel.Warn);
        }

        /// <summary>
        /// Wipe MetaState (JP, owned upgrades, stash items, dismissed indicators, kept tools/skills/
        /// buildings, completed-resets counter) without deleting the save file. Persisted
        /// immediately so a save reload picks up the clean slate. Intended for playtest iteration —
        /// "I want to test a fresh-save run without redoing character creation."
        /// </summary>
        private void CmdWipeMeta(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }

            long oldJp = _meta.State.JunimoPoints;
            int oldUpgrades = _meta.State.OwnedUpgrades.Count;
            int oldStashItems = _meta.State.StashItems.Count;

            _meta.WipeMeta();

            this.Monitor.Log(
                $"tly_wipemeta: MetaState wiped (was JP={oldJp}, upgrades={oldUpgrades}, " +
                $"stash items={oldStashItems}). Persisted to save. " +
                "Reload the save (or run tly_reset) to apply — some services hold the old " +
                "MetaState reference until OnSaveLoaded re-attaches them.",
                LogLevel.Warn);
        }

        private void CmdReplayIntro(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _introInjector?.ClearIntroState();
        }
    }
}
