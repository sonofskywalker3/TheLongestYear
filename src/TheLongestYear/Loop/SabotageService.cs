using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;
using TheLongestYear.Core.Sabotage;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear.Loop
{
    /// <summary>The darkness (spec 2026-09-09 pushback; rework 2026-09-14; Part B wiring
    /// 2026-09-15): ONE roll a night with a decaying weekly chance, one event per strike split
    /// evenly among what the season offers, and reversion and tampering that consult the
    /// obtainability model against the real save by Darkness level. Since 2026-09-21 the night pass
    /// PICKS at day end and the damage lands exactly once afterwards: in the overnight scene, or at
    /// once when no scene will play (by design, or because the scene is broken: Jeff, 2026-10-08;
    /// <see cref="Pending"/>). A strike whose scene cannot have the overnight slot is postponed,
    /// effect and scene both, and queued (Jeff, 2026-10-07). Blight kills crops (BlightPass)
    /// or empties one chest (SpoilagePass); reversion opens a filled slot; tampering rewrites an
    /// unfilled slot's item and asks ModEntry to rebuild the catalog and requirements. Host only,
    /// single player or master, never on a day 28 or the win night (RunController decides that).</summary>
    internal sealed partial class SabotageService
    {
        private readonly IMonitor _monitor;
        private readonly MetaStore _store;
        private readonly GameplayConfig _config;
        private readonly Func<IReadOnlyList<BundleRequirement>> _requirements;
        private readonly Func<ItemAvailabilityModel> _availability;
        private readonly Func<ItemPools> _pools;
        private readonly Func<ObtainabilityModel> _obtainability;
        private readonly Action<string> _rebuildBoard;
        /// <summary>Starts the board-changed scene where the farmer stands, with the old item's
        /// plural name, the new ask, whether that ask is plural, and what to do once it ends; false
        /// when it cannot start here. Set by ModEntry (the season-turn driver).</summary>
        public Func<string, string, bool, Action, bool> StartTamperScene { get; set; }

        private RunState Run => _store.Run;
        private MetaState Meta => _store.State;
        private DifficultyStep Level => Meta.EffectiveDifficulty(_config).Darkness;

        public SabotageService(
            IMonitor monitor, MetaStore store, GameplayConfig config,
            Func<IReadOnlyList<BundleRequirement>> requirements,
            Func<ItemAvailabilityModel> availability,
            Func<ItemPools> pools,
            Func<ObtainabilityModel> obtainability,
            Action<string> rebuildBoard)
        {
            _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _requirements = requirements ?? throw new ArgumentNullException(nameof(requirements));
            _availability = availability ?? throw new ArgumentNullException(nameof(availability));
            _pools = pools ?? throw new ArgumentNullException(nameof(pools));
            _obtainability = obtainability ?? throw new ArgumentNullException(nameof(obtainability));
            _rebuildBoard = rebuildBoard ?? throw new ArgumentNullException(nameof(rebuildBoard));
        }

        private static bool HostCanAct() => Game1.IsMasterGame && !Game1.IsMultiplayer;

        public bool Enabled(SabotageKind kind) => kind switch
        {
            SabotageKind.Blight => _config.EnableBlight,
            SabotageKind.Reversion => _config.EnableBundleReversion,
            SabotageKind.Tampering => _config.EnableRequirementTampering,
            _ => false,
        };

    }
}
