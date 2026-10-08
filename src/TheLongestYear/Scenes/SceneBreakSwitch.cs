using System.Collections.Generic;
using TheLongestYear.Core.Sabotage;

namespace TheLongestYear.Scenes
{
    /// <summary>Debug only (<c>tly_sabotage breakscene</c>): break a strike scene on purpose, so the
    /// "broken scene lands bare" rule (Jeff, 2026-10-08) can be watched headless. At the pick it makes
    /// the kind's staging check say no; at setUp it makes setUp throw before staging. In memory only,
    /// cleared by <c>tly_sabotage breakscene off</c> or a restart of the game.</summary>
    internal static class SceneBreakSwitch
    {
        private static readonly HashSet<DarknessEvent> AtPickSet = new();
        private static readonly HashSet<DarknessEvent> AtSetUpSet = new();

        public static bool BreaksAtPick(DarknessEvent e) => AtPickSet.Contains(e);

        public static bool BreaksAtSetUp(DarknessEvent e) => AtSetUpSet.Contains(e);

        public static void Break(DarknessEvent e, bool atSetUp)
        {
            if (atSetUp) AtSetUpSet.Add(e);
            else AtPickSet.Add(e);
        }

        public static void Clear()
        {
            AtPickSet.Clear();
            AtSetUpSet.Clear();
        }

        public static string Describe()
            => $"broken at the pick: {(AtPickSet.Count == 0 ? "none" : string.Join(", ", AtPickSet))}; broken in setUp: {(AtSetUpSet.Count == 0 ? "none" : string.Join(", ", AtSetUpSet))}";
    }
}
