using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData;
using StardewValley.GameData.Locations;
using TheLongestYear.Core;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// The Spring/Summer/Fall Returns boosts (Jeff, 2026-09-24): while one runs, Data/Locations gains
    /// a copy of every fish and forage row that belongs only to that past season, with the season
    /// restriction taken off and every other rule kept (spot, time, weather, catch limit, the
    /// legendaries' special-order guard). Today's own rows are untouched, so the pool is added to,
    /// never replaced. Which rows are copied, and how, is <see cref="PastSeasonSpawn.For"/>.
    /// <para>
    /// The copies exist only while a boost runs, so the edit is keyed on (today's season, boosted
    /// seasons) and the asset is invalidated only when that key changes: on a purchase, at day start
    /// (a boost expired or the season turned) and on save load. Copied rows carry
    /// <see cref="PastSeasonSpawn.IdPrefix"/> so the mod's own Data/Locations readers skip them.
    /// </para>
    /// </summary>
    internal sealed class PastSeasonSpawnsService
    {
        private const string LocationsAsset = "Data/Locations";

        private static readonly MethodInfo ShallowClone =
            typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly IMonitor _monitor;
        private readonly IModHelper _helper;
        private string _appliedKey = "";
        private IReadOnlySet<CoreSeason> _boosted = new HashSet<CoreSeason>();
        private CoreSeason _season;

        public PastSeasonSpawnsService(IMonitor monitor, IModHelper helper)
        {
            _monitor = monitor;
            _helper = helper;
        }

        /// <summary>Seasons brought back on a day of the year; wired at save load, cleared at title.</summary>
        public static System.Func<int, IReadOnlySet<CoreSeason>> BoostedOn;

        /// <summary>Prepare Data/Locations for <paramref name="dayOfYear"/>: today on a purchase, day
        /// start and save load, TOMORROW at day end, because vanilla spawns the next day's forage
        /// overnight, before DayStarted, and an expired boost must not leak a morning of copies.
        /// Invalidates only when (season, boosted seasons) changed.</summary>
        public void Refresh(int dayOfYear)
        {
            bool inYear = dayOfYear >= 1 && dayOfYear <= Calendar.DaysPerYear;
            IReadOnlySet<CoreSeason> boosted = inYear ? BoostedOn?.Invoke(dayOfYear) ?? new HashSet<CoreSeason>() : new HashSet<CoreSeason>();
            CoreSeason season = inYear ? Calendar.SeasonOfDay(dayOfYear) : CoreSeason.Spring;
            string key = boosted.Count == 0 ? "" : $"{season}:{string.Join(",", boosted.OrderBy(s => s))}";
            if (key == _appliedKey) return;
            _appliedKey = key;
            _boosted = boosted;
            _season = season;
            _helper.GameContent.InvalidateCache(LocationsAsset);
            _monitor.Log($"Past-season spawns: {(key.Length == 0 ? "off" : "on for " + key)} (day {dayOfYear}); Data/Locations reloaded.", LogLevel.Info);
        }

        public void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (!e.NameWithoutLocale.IsEquivalentTo(LocationsAsset)) return;
            IReadOnlySet<CoreSeason> boosted = _boosted;
            if (boosted.Count == 0) return;
            CoreSeason today = _season;

            e.Edit(asset =>
            {
                var locations = asset.AsDictionary<string, LocationData>().Data;
                int fish = 0, forage = 0;
                foreach (LocationData data in locations.Values)
                {
                    if (data == null) continue;
                    if (data.Fish != null) fish += AddCopies(data.Fish, today, boosted);
                    if (data.Forage != null) forage += AddCopies(data.Forage, today, boosted);
                }
                _monitor.Log($"Past-season spawns: added {fish} fish and {forage} forage rows for {string.Join(", ", boosted)}.", LogLevel.Trace);
            }, AssetEditPriority.Late);
        }

        private static int AddCopies<T>(List<T> rows, CoreSeason today, IReadOnlySet<CoreSeason> boosted)
            where T : GenericSpawnItemDataWithCondition
        {
            var copies = new List<T>();
            foreach (T row in rows)
            {
                if (row == null || PastSeasonSpawn.IsCopy(row.Id)) continue;
                StardewValley.Season? rowSeason = SeasonOf(row);
                CoreSeason? season = rowSeason.HasValue ? SeasonExtensions.FromMonthIndex((int)rowSeason.Value) : null;
                (bool copy, string condition) = PastSeasonSpawn.For(season, row.Condition, today, boosted);
                if (!copy) continue;

                var clone = (T)ShallowClone.Invoke(row, null);
                clone.Id = $"{PastSeasonSpawn.IdPrefix}{row.Id}";
                clone.Condition = condition;
                ClearSeason(clone);
                copies.Add(clone);
            }
            rows.AddRange(copies);
            return copies.Count;
        }

        private static StardewValley.Season? SeasonOf(GenericSpawnItemDataWithCondition row) => row switch
        {
            SpawnFishData f => f.Season,
            SpawnForageData g => g.Season,
            _ => null,
        };

        private static void ClearSeason(GenericSpawnItemDataWithCondition row)
        {
            if (row is SpawnFishData f) f.Season = null;
            else if (row is SpawnForageData g) g.Season = null;
        }
    }
}
