using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Objects;
using TheLongestYear.Core.Sabotage;

namespace TheLongestYear.Loop
{
    /// <summary>Tonight's strike, picked at day end and not yet applied (spec 2026-09-21). The
    /// overnight scene applies it at its beat; with no scene it is applied at once. Apply runs
    /// its effect once however often it is called. In memory only: a strike is always applied
    /// before the night's save.</summary>
    internal sealed class PendingStrike
    {
        public DarknessEvent Event { get; }

        /// <summary>Crows: the crop tiles on the Farm that die.</summary>
        public IReadOnlyList<Vector2> CropTiles { get; init; } = Array.Empty<Vector2>();

        /// <summary>Thief: every unit taken tonight. At most one chest is among them (Jeff,
        /// 2026-09-21), plus any number of placed machines.</summary>
        public IReadOnlyList<SpoilagePass.Hit> Hits { get; init; } = Array.Empty<SpoilagePass.Hit>();

        /// <summary>The one chest tonight's thief empties, or null when only machines went.</summary>
        public Chest TargetChest => FirstChestHit()?.Chest;

        /// <summary>Where the thief scene is staged: the chest it raids if there is one, else the
        /// first machine it takes, else null.</summary>
        public SpoilagePass.Hit SceneTarget => FirstChestHit() ?? (Hits.Count > 0 ? Hits[0] : null);

        /// <summary>The map the thief works on, or null when nothing was taken.</summary>
        public GameLocation TargetLocation => SceneTarget?.Location;

        private SpoilagePass.Hit FirstChestHit()
        {
            foreach (SpoilagePass.Hit h in Hits)
                if (!h.Machine) return h;
            return null;
        }

        public bool Applied { get; private set; }

        private readonly Action _effect;

        public PendingStrike(DarknessEvent e, Action effect)
        {
            Event = e;
            _effect = effect ?? throw new ArgumentNullException(nameof(effect));
        }

        public void Apply()
        {
            if (Applied) return;
            Applied = true;
            _effect();
        }
    }
}
