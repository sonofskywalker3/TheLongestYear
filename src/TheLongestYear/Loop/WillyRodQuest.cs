using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using TheLongestYear.Core;
using TheLongestYear.Integration;

namespace TheLongestYear.Loop
{
    /// <summary>Willy's beach scene (739330) is what closes "To The Beach", the quest his letter
    /// gives: its script opens with <c>removeQuest 13/addMailReceived NOQUEST_13</c>
    /// (Data/Events/Beach). A rod kept at the rewind or bought as a focus card marks that scene seen
    /// so it cannot hand over a second pole, and then nothing ever closed the quest (tanky24u,
    /// 1.1.4). Walking onto the beach in the scene's hours does what its script would have.</summary>
    internal static class WillyRodQuest
    {
        private const string BeachName = "Beach";
        private const string ToTheBeachQuestId = "13";
        private const string NoQuestMail = "NOQUEST_13";
        private const int SceneEarliestTime = 600;   // the scene's "t 600 1710" precondition
        private const int SceneLatestTime = 1710;

        private static IMonitor _monitor;

        public static void Init(IMonitor monitor) => _monitor = monitor;

        public static void OnWarped(object sender, WarpedEventArgs e)
        {
            if (!RunActivation.IsActive || !e.IsLocalPlayer || e.NewLocation?.Name != BeachName) return;
            if (Game1.timeOfDay < SceneEarliestTime || Game1.timeOfDay > SceneLatestTime) return;
            Farmer player = e.Player;
            if (!player.eventsSeen.Contains(ToolTierItems.WillyRodEventId) || !player.hasQuest(ToTheBeachQuestId)) return;
            player.removeQuest(ToTheBeachQuestId);
            player.mailReceived.Add(NoQuestMail);
            _monitor?.Log("Willy's rod scene was skipped (a rod was already given); closed his To The Beach quest the way the scene does.", LogLevel.Info);
        }
    }
}
