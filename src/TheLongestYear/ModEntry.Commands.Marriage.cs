using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace TheLongestYear
{
    public partial class ModEntry
    {
        /// <summary>Debug: get engaged the way a proposal leaves the save (vanilla's
        /// <c>debug engaged</c>). With <c>orphan</c>, then drop the friendship entry and keep the
        /// spouse name, the state the rewind left behind before 0.19.117.</summary>
        private void CmdEngage(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            NPC npc = args.Length > 0 ? Utility.fuzzyCharacterSearch(args[0]) : null;
            if (npc == null) { this.Monitor.Log("Usage: tly_engage <npc> [orphan]", LogLevel.Info); return; }

            Farmer p = Game1.player;
            if (!p.friendshipData.TryGetValue(npc.Name, out Friendship friendship))
                friendship = p.friendshipData[npc.Name] = new Friendship();
            p.changeFriendship(2500, npc);
            p.spouse = npc.Name;
            friendship.Status = FriendshipStatus.Engaged;
            WorldDate wedding = WorldDate.Now();
            wedding.TotalDays += 3;
            friendship.WeddingDate = wedding;

            bool orphan = args.Length > 1 && args[1].ToLowerInvariant() == "orphan";
            if (orphan) p.friendshipData.Remove(npc.Name);
            this.Monitor.Log($"tly_engage: engaged to {npc.Name}{(orphan ? ", then dropped the friendship entry (spouse name left)" : "")}.", LogLevel.Info);
        }

        private void CmdMarriage(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            Farmer p = Game1.player;
            string spouse = p.spouse;
            string status = "no entry";
            if (!string.IsNullOrEmpty(spouse) && p.friendshipData.TryGetValue(spouse, out Friendship f))
                status = $"{f.Status}, {f.Points} points";
            NPC npc = string.IsNullOrEmpty(spouse) ? null : Game1.getCharacterFromName(spouse);
            var partners = p.friendshipData.Pairs
                .Where(kv => kv.Value.IsMarried() || kv.Value.IsEngaged() || kv.Value.RoommateMarriage)
                .Select(kv => kv.Key).ToList();
            this.Monitor.Log(
                $"tly_marriage: spouse={(string.IsNullOrEmpty(spouse) ? "none" : spouse)}, friendship=[{status}], " +
                $"engaged={p.isEngaged()}, married={p.isMarriedOrRoommates()}, partner entries=[{string.Join(", ", partners)}], " +
                $"friendships={p.friendshipData.Count()}" +
                (npc == null ? "" : $", {npc.Name} at {npc.currentLocation?.Name} {npc.Tile} (home {npc.DefaultMap})"),
                LogLevel.Info);
        }

        /// <summary>Debug: what the load screen would list, through the game's own reader, so a
        /// save it cannot read shows up as missing.</summary>
        private void CmdListSaves(string command, string[] args)
        {
            if (Context.IsWorldReady) { this.Monitor.Log("tly_listsaves is title-screen-only.", LogLevel.Warn); return; }
            var find = AccessTools.Method(typeof(LoadGameMenu), "FindSaveGames");
            var farmers = (List<Farmer>)find.Invoke(null, new object[] { null });
            this.Monitor.Log($"tly_listsaves: {farmers.Count} readable: " +
                string.Join(", ", farmers.Select(f => $"{f.slotName} (spouse {(string.IsNullOrEmpty(f.spouse) ? "none" : f.spouse)})")),
                LogLevel.Info);
        }
    }
}
