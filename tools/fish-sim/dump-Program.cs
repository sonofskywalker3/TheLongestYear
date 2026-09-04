using System; using System.Collections.Generic; using System.IO; using System.Text.Json;
using Microsoft.Xna.Framework; using Microsoft.Xna.Framework.Content;
using StardewValley.GameData.Locations; using StardewValley.GameData.Objects;
var game = @"C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley";
var cm = new ContentManager(new GameServiceContainer(), Path.Combine(game, "Content"));
var locs = cm.Load<Dictionary<string, LocationData>>("Data/Locations");
var fish = cm.Load<Dictionary<string, string>>("Data/Fish");
var objs = cm.Load<Dictionary<string, ObjectData>>("Data/Objects");
var opts = new JsonSerializerOptions { WriteIndented = false, IncludeFields = true };
var rows = new Dictionary<string, List<object>>();
foreach (var (name, ld) in locs) {
  if (ld.Fish == null) continue;
  var l = new List<object>();
  foreach (var s in ld.Fish) l.Add(new { s.Id, s.ItemId, s.Chance, Season = s.Season?.ToString(), s.Condition, s.Precedence, s.MinFishingLevel, s.MinDistanceFromShore, s.MaxDistanceFromShore, s.FishAreaId, BobberPosition = s.BobberPosition?.ToString(), PlayerPosition = s.PlayerPosition?.ToString(), s.RequireMagicBait, s.IsBossFish, s.CatchLimit, s.CanBeInherited, s.UseFishCaughtSeededRandom, s.IgnoreFishDataRequirements, s.ApplyDailyLuck, s.CuriosityLureBuff, s.SpecificBaitBuff, s.SpecificBaitMultiplier, ChanceModifiers = s.ChanceModifiers?.Count ?? 0, ChanceBoostPerLuckLevel = s.ChanceBoostPerLuckLevel, s.RandomItemId });
  rows[name] = l;
}
var names = new Dictionary<string,string>(); foreach (var (id,o) in objs) names[id] = o.Name;
File.WriteAllText("locations_fish.json", JsonSerializer.Serialize(rows, opts));
File.WriteAllText("fish.json", JsonSerializer.Serialize(fish, opts));
File.WriteAllText("objects.json", JsonSerializer.Serialize(names, opts));
Console.WriteLine($"locs {rows.Count} fish {fish.Count} objs {names.Count}");
