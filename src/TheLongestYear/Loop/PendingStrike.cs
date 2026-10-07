using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Objects;
using TheLongestYear.Core.Sabotage;

namespace TheLongestYear.Loop
{
    /// <summary>Tonight's strike, picked at day end and not yet applied (spec 2026-09-21). The
    /// overnight scene applies it at its beat. With no scene by design it is applied at once. When
    /// its scene cannot have the overnight slot it is postponed: dropped unapplied and uncommitted,
    /// so nothing of it lands (Jeff, 2026-10-07). Apply runs its effect once however often it is
    /// called, and tells its owner whether the effect landed. In memory only: a strike is always
    /// applied or postponed before the night's save.</summary>
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

        /// <summary>Is tonight spent on this strike? Set when its scene takes the overnight slot, or
        /// when it is applied (which commits first). Until then the run has recorded nothing, so a
        /// strike dropped uncommitted is a night with no strike at all.</summary>
        public bool Committed { get; private set; }

        /// <summary>Did the effect actually land? False until <see cref="Apply"/> has run, and false
        /// afterwards when it found nothing to do (a slot the board would not open, a tamper the
        /// world state refused, a blight that took nothing).</summary>
        public bool Landed { get; private set; }

        private readonly Func<bool> _effect;
        private readonly Action<PendingStrike> _onApplied;
        private readonly Action<PendingStrike> _onCommitted;

        /// <param name="effect">What the strike does. True when it landed.</param>
        /// <param name="onApplied">Told once, inside <see cref="Apply"/>, whatever the outcome. This
        /// is where the run's bookkeeping is corrected, so it runs on every apply path: the
        /// immediate one, the nets, and a scene calling <see cref="Apply"/> itself.</param>
        /// <param name="onCommitted">Told once, inside <see cref="Commit"/>: where the run records the
        /// strike (the week's chance, the cap, the guarantee).</param>
        public PendingStrike(DarknessEvent e, Func<bool> effect, Action<PendingStrike> onApplied = null, Action<PendingStrike> onCommitted = null)
        {
            Event = e;
            _effect = effect ?? throw new ArgumentNullException(nameof(effect));
            _onApplied = onApplied;
            _onCommitted = onCommitted;
        }

        /// <summary>Spend tonight on this strike, at most once: its scene has the slot, or it is
        /// landing now.</summary>
        public void Commit()
        {
            if (Committed) return;
            Committed = true;
            _onCommitted?.Invoke(this);
        }

        /// <summary>Do it, at most once. Returns whether the effect landed.</summary>
        public bool Apply()
        {
            if (Applied) return Landed;
            Commit();
            Applied = true;
            Landed = _effect();
            _onApplied?.Invoke(this);
            return Landed;
        }
    }
}
