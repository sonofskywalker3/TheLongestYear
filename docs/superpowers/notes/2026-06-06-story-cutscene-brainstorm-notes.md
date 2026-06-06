# The Longest Year — Story & Cutscene Brainstorm (WIP notes)

**Status:** brainstorming in progress (2026-06-06). NOT a spec yet. Do not implement from this.
These are running notes captured during an interactive brainstorm; they'll be hardened into a
design spec once the shape is settled.

## Scope decision
- **Full arc / trilogy track.** Not just re-staging existing beats — authoring the actual narrative
  spine across the planned TLY1→TLY2→TLY3 trilogy, with TLY1 cutscenes laying the track.
- Immediate concrete deliverable: replace the flat "very basic dialogs" (esp. the Day-28 black-screen
  text menus) with real cutscenes — movement, sprite work, camera, sound — not just portraits + text.

## Current state of "dialogue" tech (baseline)
- **Intro (Day 1, fresh run)** = a real vanilla `Event` (`IntroEventInjector.BuildIntroEvent`):
  Lewis on the farm porch → `changeLocation CommunityCenter` → Junimo explains the loop. Has warps,
  camera (`viewport`), temporary actors, sounds, pauses, facing. BUT it's talking-heads: heavy
  `speak` (portrait+text), almost no blocking / sprite motion.
- **Season turns (Day 28 fail / continue / win)** = NOT cutscenes. Self-drawn black-screen text
  menus (`Day28CutsceneMenu`, `VictoryMenu`). Pure text on black. **These are the flattest moments
  and the top priority to dramatize.**
- Driver pattern: `IntroSequenceDriver` / `Day28CutsceneDriver` open content on settled frames;
  gated by `RunActivation.IsActive` (dormant on non-TLY saves) + `HasSeenIntro` (one-time intro).

## The spine (what the trilogy is ABOUT)
Two braided themes under the donate-or-rewind mechanic:
1. **The loneliness of looping** — the player remembers every loop; the town forgets (faint déjà-vu).
   The ache: you build real bonds, then watch everyone forget you each rewind. Payoff across the
   trilogy = by TLY3 they finally remember *with* you.
2. **Healing a wounded valley** — each loop the land (and the Junimos) recover a little more.

**Joja is the persistent villain across all three years**, escalating:
- **TLY1 — The Community Center.** Joja wants to demolish the old hall. You loop the year donating to
  restore it. Win = you save the CC. Joja loses round one.
- **TLY2 — Ginger Island.** Rebuffed in town, Joja pivots to building a resort on Ginger Island.
  Hearts start to carry; the town begins to half-remember.
- **TLY3 — The Annexation.** Joja moves to swallow the whole valley. Endgame: bring *everyone* into
  the loop (the town wakes to the cycle alongside you) and reach **Perfection** — which is what
  charges the Junimos enough to **break Morris free of the darkness** driving him. The villain is
  *saved*, not destroyed. True "Perfection" = redeeming the man who tried to pave paradise.

## The antagonist (the "dark force")
- **Approach: keep it a mystery in TLY1; show glimpses only.** Don't name it yet.
- TLY1 hints: a **dark aura on Morris** when seen (he's being controlled); the **Junimos murmur in
  later seasons that something opposes them** — the valley's sickness has a will behind it.
- Identity (named entity) decided later when building TLY2/3. Leaning: the Junimos' opposite —
  something that feeds on disconnection & forgetting (which is *why* the loops erase memory).

## The reframed opening (THE keystone) — player as Joja defector
- Replaces the canonical "inherited grandpa's farm / quit Joja" intro.
- **You work for Joja.** You're SENT to the valley to secure the acquisition — to talk the old man
  (grandfather) into selling the farm.
- While packing to leave, **grandfather's letter reaches you.**
- You arrive, meet the townsfolk, feel the place, hear how much he loved it.
- **The turn:** you quit Joja and join the townspeople to restore the CC.
- **Thematic payoff — the player is Morris's mirror.** You arrive to do exactly what Morris does;
  the letter + the town turn you. Morris chose Joja over connection and got hollowed out. TLY3's
  "save Morris" lands because *you were almost him* — the version that didn't get the letter in time.
  Answers "why you?": the Junimos woke for the one Joja sent — the one who could understand the
  enemy from inside.

### Opening staging decision
- **Cinematic montage** (non-interactive staged cutscene; a "movie" you watch). The existing
  Lewis-porch + Junimo loop-explanation scenes get **rolled INTO the montage** (with edits) — it's
  one continuous opening, not montage-then-separate-intro.
- Prequel plays **once** (like the current intro / `HasSeenIntro`); every rewind resets to Spring 1
  on the farm, post-defection.

### Canon intro facts (verified — reuse these assets)
- Deathbed scene: grandpa gives CHILD-you a **sealed letter** ("open it when you're crushed by the
  burden of modern life"), then passes (years before the present). Grandpa is DEAD in canon.
- Time skip: adult-you miserable in the **Joja Corporation cubicle**.
- You open the envelope → it's the **deed to grandpa's farm**; you leave for the valley; Lewis meets
  you at the overgrown farm. (Skippable via checkbox in vanilla.)
- So vanilla already hands us: deathbed sprite scene, Joja office, the letter, the bus. REUSE them.
- Sources: stardewvalleywiki.com/Grandpa ; fandom User_blog:718A/Intro_Explained.

### Opening montage — RECONCILED beat sheet (grandpa stays dead; reuse vanilla assets)
Key fix vs. earlier draft: Joja isn't sending you to "convince" a dead man — the **deed is the hinge.**
You inherited the farm and never cared; Joja needs that keystone parcel and taps you (the heir) to
sell it over.
1. **Deathbed prologue** *(reuse vanilla)* — grandpa gives young-you the **sealed letter** (which
   contains the **deed**); passes. The letter/grandpa content stays innocent — he never knew about
   any darkness; it's pure, ordinary love. (That purity is *why* it works as the key — see below.)
2. **Joja cubicle** *(reuse vanilla)* — adult-you, a Joja drudge.
3. **The assignment** — Joja's valley takeover needs the keystone parcel = grandpa's farm. The deed
   has been unseen since his death, so ownership is in limbo. Joja taps YOU (the heir) to get
   **Lewis** (mayor / keeper of the records) to formalize your ownership **so you can sell it to
   Joja.** Personal connection weaponized; fat payout dangled. You think you're there to claim-and-flip.
4. **Bus stop → Robin's town tour** — **Robin** shows you around, you meet a few folk, and you catch
   **glimpses of Junimos doing sparkle-magic on YOU** (mechanic below) — loosening the darkness's grip.
5. **Robin walks you to the farm.**
6. **Lewis at the farm** — tells you the farm was **yours all along**, and to **open the letter.**
7. **Open the letter → the deed + grandpa's loving words.** Reading it **snaps the last chain of
   darkness** the Junimos had loosened. You decide, *right there at the farm*, to fight Joja instead
   of sign. (Ordering: darkness BREAKS here via the letter; the Junimos EXPLAIN the mechanism later.)
8. **Lewis names the CC** — and calls it hopeless (no time, no skill, nobody).
9. **The CC → the Junimos** — they **explain** what just happened: your heart was at a turning point,
   they freed you to choose for yourself. Then the loop is explained (folded-in + rewritten Junimo
   speech). → drops you at the farm, Spring 1.

### Grandpa's spirit (recurring ally)
- Grandpa is dead (canon) and never knew about the darkness — but his **spirit** (Grandpa's Shrine,
  outside the loop) becomes a **recurring force pushing the player against the darkness.** Grounds the
  earlier "a soul who remembers across loops" idea in canon. His pure love is the darkness's antidote.

### NEW locked ideas — the "freed to choose" mechanic (load-bearing for the whole trilogy)
- **Junimo sparkle-glimpses during the town walk** = the Junimos actively *lifting the darkness off
  the player*. Foreshadows that the darkness controls people (Morris) and that the Junimos can break
  its grip.
- **Junimo reveal in the CC:** you were **chosen because your heart was at a turning point**, and they
  **freed you to decide for yourself — pursue darkness (Joja) or side with nature.** They didn't make
  you good; they gave you back your free will at the crossroads.
- **Why this matters:** the intro **demonstrates the TLY3 climax in miniature, on the player.** Freeing
  the player from the darkness so they can choose IS exactly what they'll do to Morris at Perfection.
  TLY1 proves the redemption is possible by performing it on you first. (The player is Morris's mirror,
  now mechanically as well as thematically.)

## Locked assumptions (unless revisited)
- **Memory model:** player remembers all loops; town forgets, with faint déjà-vu. (Already the v1 plan.)
- **Morris appears in TLY1 at least once** so the dark-aura glimpse has a home.
- **Asset budget:** stage mostly from vanilla sprites + cheap effects (camera, fades, tint/aura,
  sound, weather); commissioned custom art is a "nice to have," not a dependency. (To confirm.)

## Build approach (DECIDED) — and the feasibility reality
- **Build all cutscenes as vanilla Stardew event scripts** (the `Data/Events` command language the
  intro already uses), staged in real locations with real sprites. One system, native feel, reuses
  everything; extend the command vocabulary as needed.
- **No artist is on the critical path.** Base-game cutscenes (bus intro, flower dance, CC Junimo
  reveal, Perfection/Summit ending) are composed from EXISTING sprites + event commands — zero new art.
  Custom art is optional gravy, never required.
- **Free primitives we can script today (no art):** camera pan/zoom, fade to/from black/white, screen
  flash, **screen shake**, move/animate existing sprites (villagers, Junimos, Joja, farmer), emotes
  (`?`/`!`), **full-screen color tint** (season-color sweep; purple-black darkness wash), existing
  particle effects (sparkles, magic poofs, Junimo star bursts, shadow wisps), weather, music/sound.
- Fallback option (NOT chosen, kept for reference): custom-drawn C# animated canvas (the Day-28 menus
  are already custom C# draws, so abstract spectacle *could* be animated there with total control).
- **User has no art/cinematic background and was worried "cinematic" was impossible — it isn't.** This
  was a key reassurance; keep expectations calibrated to "cinematic within the vanilla event toolkit,"
  which is genuinely strong.

## TLY1 cutscene moment map (DECIDED — all of these get real staging)
1. **Opening montage** (one-time) — the 9-beat defector sequence above.
2. **Restaged porch + Junimo intro** — folded into the montage; real blocking (Lewis walking up,
   Junimo hopping/emoting, camera) instead of static portraits.
3. **The rewind (season failed)** — the signature. **Tone = dark spectacle that lands intimate.**
   Beat sketch (all from free event primitives): void/black backdrop → darkness presses in (purple-black
   tint + shadow wisps + low ominous drone — *it gloats*) → **Junimos pop in around you, glowing,
   jittering as they strain** to push the dark back → screen **washes backward through the seasons**
   (winter-blue → fall-orange → summer-green → spring-pink tint sweep) → a **villager you'd bonded with
   flickers, throws a `?` emote, and turns away — forgetting you** (plants the déjà-vu thread) → white
   flash + reversed whoosh → wake in bed, Spring 1. Replaces the current static reset card.
4. **Season passed (turn forward)** — 3 per loop (Spring/Summer/Fall turns). Short Junimo beat:
   acknowledges progress / growing strength AND **escalating unease about the opposing force.** NOTE:
   the unease only rings true because of the **active-opposition / sabotage mechanic** below — without
   real opposition the Junimos have nothing to fear. The two are coupled: cutscene tone tracks the
   sabotage intensity (calm Spring → uneasy Fall → alarmed Winter).
5. **Morris dark-aura glimpses (rare, eerie — 2 sightings in TLY1):**
   - **(a) Exiting the CC, end of the opening montage** (right after the Junimo scene). Morris is
     waiting; draws the battle lines and starts the one-year clock diegetically. Dialogue beat
     (paraphrase): *"I hope you're not trying to do something with this old building. Joja gets what
     it wants — we want to turn it into a warehouse. You may keep the farmland from us, but in one
     year this place will be **ours**, and there's no way you can stop it."* On the final **"there's
     no way you can stop it,"** Morris flips to a **custom sprite with the eyes recolored glowing
     red** (cheap palette swap on his existing sprite — doable by us, NOT a real-art dependency). First
     darkness glimpse + stakes/deadline in one beat.
   - **(b) The ending** — Morris retreats with the **shadow still clinging** to him, vowing "greener
     ground" (→ Ginger Island). (See win/ending beat #6.)
   - Morris otherwise recedes in TLY1; the heavy Morris arc is TLY2/3. Keeps him mysterious.
6. **The win / ending (CC restored)** — **Triumph + ominous hook.** Full joyful payoff first: Joja
   **closes the town store**, **Junimo celebration**, the town whole. THEN a final dark beat: Morris
   leaves, the **shadow still clinging to him**, vowing Joja will "find greener ground" (→ Ginger
   Island / TLY2). You won the battle, not the war. Satisfying as a standalone 1.0 ending AND launches
   Year 2 on the same save. **Flows into the JP shrine** (folds in the TODO's "win→shrine transition
   is jarring" fix — make it one continuous beat, not a hard cut).

## Active opposition / sabotage mechanic (NEW — story-driven gameplay; spec WITH the story)
**Problem it solves:** the Junimos' escalating unease (season-pass cutscenes) only rings true if
something is *actively* working against the restoration. Morris's bad will isn't opposition; the
darkness must ACT.

**Concept:** the darkness (via Morris — *deniable, unprovable*) sabotages your CC progress, escalating
across the year, peaking in Winter. This is what gives the Junimos real fear AND raises late-game
difficulty (making the rewind a genuine threat in later seasons).

### ⚑ The darkness is an INDEPENDENT FORCE, not Morris's scheme (key worldbuilding lock)
- The darkness is the **Junimos' opposite number** — a supernatural force that acts on the world
  *directly*, the same way the Junimos do. Junimos = growth / connection / renewal (grow crops,
  restore the hall, rewind the year, peel the darkness off the player). Darkness = decay / consumption
  / **disconnection** — and it can reach into the world itself (undo donations, corrupt requirements,
  blight the land).
- **Morris is the human it has its hand on — its face among people, a SYMPTOM, not the mechanism.**
  The sabotage is the FORCE acting, not a man sneaking around. (Corrects any "Morris does the
  sabotage" reading.)
- Retro-fits cleanly: the opening sparkles peeling the darkness off the player, Morris's red eyes, the
  rewind's gloating shadow — all the same force.

**Thematic payoff (now at full strength):** the sabotage is invisible to everyone but the player + the
Junimos. The town sees crops rot and progress slip and thinks *the player* is failing — there's
literally "nothing there" to prove. Not just unremembered, not just unbelieved, but **alone with a
truth no one else can perceive.** Loneliness theme, maxed.

**Sabotage forms (CHOSEN) + their GRANULAR counterplay** (no blanket "ward" — split up, à la TLY's
whole upgrade philosophy; dropped "Joja inspection / theft" — corporate-physical, not the force):
- **Bundle reversion** — a completed donation slot quietly empties; redo required.
  → Counter: **per-room "Protect [Room] donations" JP powers** (~6, one per CC room). Buy per room.
- **Blight / spoilage** — crops/stored items rot/wither overnight; the force poisoning the land.
  → Counter: **per-season "Protect crops" JP powers** (Spring / Summer / Fall).
  *(Balance note: Winter has no field crops — decide if blight then hits stored items or is absent.)*
- **Requirement tampering** — an un-donated bundle requirement switches to a different item (ideally
  one not on hand) — attacks planning.
  → **WINTER-ONLY, UNAVOIDABLE. No counterplay power.** This is the deliberate finale pressure valve:
  once the player has armored rooms + crops, reversion/blight are neutralized, so Winter would be a
  cakewalk — requirement tampering is the darkness's last move that can't be bought off, forcing a
  scramble in the home stretch. The Fall→Winter season-pass cutscene foreshadows exactly this.

**Escalation schedule (CONCRETE — each season opens a NEW front):**
- **Spring** — clean (learn the systems).
- **Summer** — **blight** begins (mess with crops) → buy per-season crop protection.
- **Fall** — **+ bundle reversion** (undo donations); blight continues → buy per-room donation protection.
- **Winter** — **+ requirement tampering**, unavoidable, on top of the rest.
Each season hands you the prior season's JP to prepare a counter before the next front opens. Junimo
unease escalates because the darkness keeps finding NEW ways in.

**Fairness guardrails (assumed defaults):**
- **Telegraphed by SUPERNATURAL signs** (shadow residue, unnatural chill, withering) — invisible to
  the town by design, perceivable only by player/Junimos. NOT a Joja paper trail.
- **Bounded** — it nibbles, never wipes a run.
- **Escalating** — nothing in Spring → unsettling by Fall → active assault in Winter.
- **Counterplay is GRANULAR** (per-room / per-season powers), slotting into the per-year upgrade-layer idea.

**Scope note:** gameplay mechanic (needs balance/playtesting), but story-driven → spec it WITH the
story work, NOT in the save-architecture spec.

## Trilogy architecture decision (NEW — big, SEPARATE workstream from this story pass)
- **DECIDED: all three games (TLY1/2/3) run continuously on ONE save.** Not three separate runs/mods
  played independently — one evolving campaign. "Will need to adjust accordingly."
- **Escalating win bar per year:**
  - TLY1 = restore the Community Center (current).
  - TLY2 = CC + (if a-year-proves-too-easy) **basic Perfection** as added criteria.
  - TLY3 = **ultimate (full) Perfection**.
- **Each new year adds a new LAYER of Junimo upgrades** to help hit the (higher) seasonal goals in time.
- ⚠️ This is an architecture/progression workstream (save continuity, year/stage state machine, upgrade
  tiers, win-condition escalation) that is **independent of the cutscene/story authoring** we're
  brainstorming here. Recommend it gets its OWN design spec; this spec stays focused on TLY1 narrative +
  cutscenes (which seed the trilogy regardless of when the architecture lands).

## Open / still to brainstorm
- **Grandfather's status** — alive (sent to convince him to sell) vs. just-passed (vanilla-style
  posthumous letter) vs. ailing-then-passes. Load-bearing for the montage + emotional core. (NEXT.)
- How to **show the rewind** (the year unwinding) instead of black-screen text — the signature moment.
- The **win** cutscene restage (currently `VictoryMenu` text).
- Dialogue voice / tone calibration.
- Whether Morris's TLY1 appearance(s) are scripted scene(s) or ambient, and how they escalate.
