using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Integration
{
    /// <summary>
    /// v1.1 narrative intro: replaces vanilla's day-1 Robin/Lewis welcome with a TLY-specific
    /// Lewis-on-porch + Junimo-in-CC chain that frames the year-loop stakes (Joja deadline,
    /// historical-landmark protection, Junimo rewind-on-failure mechanic).
    ///
    /// Two events are injected via SMAPI asset edits:
    ///   - <c>tly_intro_porch</c> on Data/Events/Farm — Spring 1, Lewis welcomes the player
    ///     and frames the Joja threat + gives the CC key.
    ///   - <c>tly_intro_cc</c> on Data/Events/CommunityCenter — fires once the player walks
    ///     into the CC, a Junimo appears and explains the bundle system + the rewind loop.
    ///
    /// Both events are PREPENDED to the dict so they win iteration order against any vanilla
    /// day-1 farm event (e.g. event 60367 Robin arrival) that shares the Spring-1 precondition.
    ///
    /// Gating uses per-run mail flags so vanilla precondition syntax works:
    ///   - <c>tly_intro_porch_seen</c> — added at end of porch event, gates the CC event in.
    ///   - <c>tly_intro_cc_seen</c> — added at end of CC event, drives the cross-run flag.
    ///   - <c>tly_intro_done</c> — injected on SaveLoaded when <see cref="MetaState.HasSeenIntro"/>
    ///     is already true, suppressing both events on every subsequent loop.
    ///
    /// HasSeenIntro is the load-bearing cross-run gate. It's set in <see cref="MarkIntroSeenIfApplicable"/>
    /// (called from <c>ModEntry.OnSaving</c>) whenever <c>tly_intro_cc_seen</c> is present on the
    /// player. WorldResetService wipes mailReceived but never touches MetaState, so the flag survives.
    /// </summary>
    internal sealed class IntroEventInjector
    {
        // Event ids — string ids work in 1.6+ and live alongside vanilla numeric ids in eventsSeen.
        private const string PorchEventId = "tly_intro_porch";
        private const string CcEventId    = "tly_intro_cc";

        // Mail flags. Per-run lifecycle: wiped by FarmerReset.loadForNewGame; persists within a
        // single loop via Farmer.mailReceived (which is part of the save).
        private const string PorchSeenMail = "tly_intro_porch_seen";
        private const string CcSeenMail    = "tly_intro_cc_seen";
        private const string IntroDoneMail = "tly_intro_done";

        // Asset names. SMAPI's NameWithoutLocale comparison is case-insensitive on these.
        private const string FarmEventsAsset = "Data/Events/Farm";
        private const string CcEventsAsset   = "Data/Events/CommunityCenter";

        private readonly IMonitor _monitor;
        private readonly MetaStore _meta;

        public IntroEventInjector(IMonitor monitor, MetaStore meta, IModHelper helper)
        {
            _monitor = monitor;
            _meta = meta;
            helper.Events.Content.AssetRequested += this.OnAssetRequested;
        }

        /// <summary>Called from <c>ModEntry.OnSaveLoaded</c> after MetaStore.Load(). When the
        /// player has already seen the intro on a prior loop, plant the <c>tly_intro_done</c>
        /// mail flag so the two event preconditions fail and neither event fires.</summary>
        public void ApplyMailFlagsForRun()
        {
            if (!_meta.State.HasSeenIntro) return;
            if (Game1.player == null) return;
            if (!Game1.player.mailReceived.Contains(IntroDoneMail))
                Game1.player.mailReceived.Add(IntroDoneMail);
        }

        /// <summary>Called from <c>ModEntry.OnSaving</c> just before persisting MetaState.
        /// If the CC event has completed this run (its end-command adds the mail flag),
        /// promote the run flag to the cross-run <see cref="MetaState.HasSeenIntro"/> so
        /// the upcoming reset can't lose it.</summary>
        public void MarkIntroSeenIfApplicable()
        {
            if (_meta.State.HasSeenIntro) return;
            if (Game1.player == null) return;
            if (Game1.player.mailReceived.Contains(CcSeenMail))
            {
                _meta.State.HasSeenIntro = true;
                _monitor.Log(
                    "IntroEventInjector: tly_intro_cc_seen detected — promoting to MetaState.HasSeenIntro.",
                    LogLevel.Info);
            }
        }

        /// <summary>Debug helper: clear MetaState.HasSeenIntro AND wipe the per-run mail flags
        /// so a <c>tly_reset</c> back to Spring 1 re-fires the intro chain. Used by
        /// <c>tly_replayintro</c>.</summary>
        public void ClearIntroState()
        {
            _meta.State.HasSeenIntro = false;
            if (Game1.player != null)
            {
                Game1.player.mailReceived.Remove(PorchSeenMail);
                Game1.player.mailReceived.Remove(CcSeenMail);
                Game1.player.mailReceived.Remove(IntroDoneMail);
                Game1.player.eventsSeen.Remove(PorchEventId);
                Game1.player.eventsSeen.Remove(CcEventId);
            }
            _monitor.Log(
                "IntroEventInjector: cleared HasSeenIntro + porch/cc mail flags + eventsSeen entries. " +
                "Run tly_reset and walk out of the farmhouse to retest the intro.",
                LogLevel.Warn);
        }

        private void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (e.NameWithoutLocale.IsEquivalentTo(FarmEventsAsset))
            {
                e.Edit(asset => PrependEntry(asset, PorchEventKey(), PorchEventScript()),
                    AssetEditPriority.Default);
            }
            else if (e.NameWithoutLocale.IsEquivalentTo(CcEventsAsset))
            {
                e.Edit(asset => PrependEntry(asset, CcEventKey(), CcEventScript()),
                    AssetEditPriority.Default);
            }
        }

        /// <summary>Insert <paramref name="key"/>=<paramref name="value"/> at the FRONT of the
        /// dict so vanilla's first-precondition-match-wins iteration picks our entry before
        /// any vanilla day-1 event on the same location. Existing entries are re-added in
        /// their original order behind ours.</summary>
        private static void PrependEntry(IAssetData asset, string key, string value)
        {
            var data = asset.AsDictionary<string, string>().Data;
            if (data.ContainsKey(key))
                return; // already injected — idempotent across asset reloads
            var existing = new System.Collections.Generic.Dictionary<string, string>(data);
            data.Clear();
            data[key] = value;
            foreach (var kv in existing)
                data[kv.Key] = kv.Value;
        }

        // ---- Event script authoring --------------------------------------------------------

        /// <summary>Porch event key with preconditions. Slash-separated: id then prereqs.
        ///   D 1            — day 1
        ///   s spring       — spring season (TLY resets the calendar so this re-evaluates true on every loop start)
        ///   !m porch_seen  — not already seen this run (porch event end adds this flag, so a save+reload mid-run won't refire)
        ///   !m intro_done  — cross-run gate; set on save load when MetaState.HasSeenIntro is true
        /// </summary>
        private static string PorchEventKey()
            => $"{PorchEventId}/D 1/s spring/!m {PorchSeenMail}/!m {IntroDoneMail}";

        /// <summary>CC event key. Gates on porch event having fired this run (so the chain is
        /// forward-only — player has to go through Lewis first), plus the same cross-run done flag.
        /// </summary>
        private static string CcEventKey()
            => $"{CcEventId}/m {PorchSeenMail}/!m {CcSeenMail}/!m {IntroDoneMail}";

        /// <summary>Porch event. Lewis spawns south of the standard farmhouse, welcomes the
        /// player, frames the Joja deadline + historical-landmark protection, hands over the
        /// key, walks off south. Player remains on the farm to walk to town. Ends with the
        /// porch_seen mail flag so the CC event becomes eligible.</summary>
        private static string PorchEventScript() => string.Join("/", new[]
        {
            "none",                                  // music: keep current (or none)
            "-1500 -2000",                           // viewport: don't move
            "farmer 66 18 1 Lewis 68 18 3",          // actors: player at (66,18) facing east; Lewis at (68,18) facing west
            "skippable",
            "pause 1200",
            "speak Lewis \"Hi there, @. Welcome to the valley.#$b#I'm Lewis — mayor of Pelican Town. I came up to greet you and your new farm.$h\"",
            "pause 200",
            "speak Lewis \"I wanted to be the first to say it... but I'm afraid I have to apologize for the timing.$s\"",
            "pause 200",
            "speak Lewis \"Joja Corporation rolled out a 'revitalization initiative' this spring. They've already filed permits to demolish our old Community Center. They're calling it an 'eyesore.'$a\"",
            "pause 200",
            "speak Lewis \"Deadline is Winter 28. If nothing changes by then, it'll be a Joja warehouse by next year.$s\"",
            "pause 400",
            "speak Lewis \"If only there was a way for us to repair the old Community Center.$s#$b#It's a registered historical landmark, you know. The state will protect it — but only if it's restored to full working condition before that deadline.$h\"",
            "pause 200",
            "speak Lewis \"I figured... if anyone's going to take a swing at it, it ought to be you. New blood. Fresh eyes.$h\"",
            "pause 300",
            "speak Lewis \"Here. Take the key to the front door. Have a look inside.$h\"",
            "playSound coin",
            "pause 800",
            "speak Lewis \"Good luck out there, @. Pelican Town's rooting for you.$h\"",
            "pause 500",
            "move Lewis 0 4 2 true",
            "move Lewis -10 0 3 false",
            $"addMailReceived {PorchSeenMail}",
            "end"
        });

        /// <summary>CC event. Player spawns just inside the south door, walks north into the
        /// hall, looks around (faceDirection cycle stands in for an animation), a Junimo drops
        /// in from the north, gives the bundle pitch + the loop-rewind framing, hops away.
        /// Ends with the cc_seen mail flag — which MarkIntroSeenIfApplicable promotes to
        /// MetaState.HasSeenIntro on the next save.</summary>
        private static string CcEventScript() => string.Join("/", new[]
        {
            "none",
            "-1500 -2000",
            "farmer 32 22 0",                                    // player just inside south door
            "skippable",
            "addTemporaryActor Junimo 16 16 32 11 2 false character Junimo",
            "pause 1000",
            "move farmer 0 -3 0 true",
            "pause 500",
            "faceDirection farmer 1",
            "pause 400",
            "faceDirection farmer 3",
            "pause 400",
            "faceDirection farmer 0",
            "pause 800",
            "playSound junimoMeep1",
            "pause 200",
            "move Junimo 0 4 2 false",
            "pause 300",
            "speak Junimo \"Hi! We are the Junimos.#$b#We've lived here since the founders built the Center, ages ago.$h\"",
            "pause 200",
            "speak Junimo \"We helped them keep the valley alive. They brought us Bundles — gifts of crops, fish, fiber, food — and we channeled the seasons back through the land.$h\"",
            "pause 200",
            "speak Junimo \"Then the people stopped coming. The Center fell silent. We slept.$s\"",
            "pause 200",
            "speak Junimo \"Now Joja wants to tear it all down. But you're here. So we woke up.$h\"",
            "pause 300",
            "speak Junimo \"If you fill the Bundles — all of them, every room — the Center will be whole again. Joja cannot touch a restored landmark.$h\"",
            "pause 400",
            "speak Junimo \"But the year is short, @. We can already feel it. You will not finish in time. Not on this run.$s\"",
            "pause 200",
            "speak Junimo \"So if Winter ends with Bundles unfilled, we will turn time back. The seasons, the crops, the buildings — all of it returns to Spring 1.$s\"",
            "pause 300",
            "speak Junimo \"Your progress will be lost. But the energy you spent — every donation, every season worked, every connection — we keep that. We will use it to make the next loop a little kinder.$h\"",
            "pause 300",
            "speak Junimo \"We will do it as many times as we must. There is no shame in starting again.$h\"",
            "pause 300",
            "speak Junimo \"Will you help us?$h\"",
            "pause 800",
            "speak Junimo \"Good luck, @. Spring 1 is yours.$h\"",
            "pause 600",
            "playSound junimoMeep1",
            "move Junimo 0 -5 0 true",
            "pause 1500",
            $"addMailReceived {CcSeenMail}",
            "end"
        });
    }
}
