# The Longest Year — TLY1 Story & Cutscenes Design

**Date:** 2026-06-06
**Status:** Design spec — approved in brainstorm, pending user review. NOT yet planned/built.
**Companion notes:** `docs/superpowers/notes/2026-06-06-story-cutscene-brainstorm-notes.md`
**Separate dependent spec (TODO):** the "one-continuous-save trilogy architecture" (see Out of Scope).

---

## 1. Summary

Replace TLY1's flat dialogue/text moments with real **cutscenes** (movement, sprite work, camera,
sound — not portraits-and-text), and in doing so author the **narrative spine** that seeds the planned
TLY1→TLY2→TLY3 trilogy. Add an **active-opposition (sabotage) gameplay mechanic** that makes the
antagonist mechanically real and justifies the cutscenes' rising tension.

All cutscenes are built as **vanilla Stardew event scripts** (the `Data/Events` command language the
intro already uses) — staged in real locations with existing sprites. **No artist is on the critical
path;** the one custom asset is a trivial palette swap (Morris's eyes → glowing red).

---

## 2. Goals / Non-goals

**Goals**
- Turn the flattest moments (the Day-28 season-turn text menus, the static reset card, the placeholder
  victory menu) into staged cutscenes.
- Re-stage the existing porch/Junimo intro to the same bar and fold it into a larger opening montage.
- Establish the trilogy's themes, antagonist nature, and the player's role — so TLY1 plants TLY2/3 track.
- Add the sabotage mechanic (story-driven gameplay) that the season cutscenes reference.

**Non-goals (this spec)**
- The one-continuous-save trilogy **architecture** (save continuity, year/stage machine, escalating
  win bars, per-year upgrade layers) — its own spec (logged on TODO 2026-06-06).
- TLY2 (Ginger Island) and TLY3 (annexation + Morris redemption) content beyond seeding hooks.
- Naming/revealing the dark force (deliberately deferred — TLY1 only glimpses it).
- The déjà-vu villager-dialogue system (Gribbleby's idea, already on TODO) — may be its own spec; this
  spec only plants the thread (a villager forgetting you in the rewind).

---

## 3. Narrative spine

**Two braided themes under the donate-or-rewind mechanic:**
1. **The loneliness of looping** — the player remembers every loop; the town forgets (faint déjà-vu).
   You build real bonds, then watch everyone forget you each rewind. Trilogy payoff = by TLY3 they
   finally remember *with* you.
2. **Healing a wounded valley** — each loop the land (and the Junimos) recover a little more.

**Joja is the throughline villain, escalating across a one-save trilogy:**
- **TLY1 — The Community Center.** Joja wants to demolish the old hall; you loop the year restoring it.
  Win = you save the CC. *Joja loses round one.*
- **TLY2 — Ginger Island.** Rebuffed in town, Joja pivots to a resort on Ginger Island. Hearts begin to
  carry; the town starts to half-remember.
- **TLY3 — The Annexation.** Joja moves to swallow the whole valley. Endgame: bring *everyone* into the
  loop and reach **Perfection** — which charges the Junimos enough to **break Morris free of the
  darkness.** The villain is *saved*, not destroyed. True "Perfection" = redeeming the man who tried to
  pave paradise.

**The antagonist — an independent dark force (NOT Morris's scheme):**
- The Junimos' **opposite number** — a supernatural force of decay / consumption / **disconnection**
  that acts on the world *directly*, the same way the Junimos do. Junimos grow, restore, rewind, and
  peel the darkness off the player; the darkness undoes, corrupts, and blights.
- **Morris is the human it has its hand on** — its face among people, a *symptom*, not the mechanism.
- TLY1 only **glimpses** it (red-eyed Morris; the rewind's gloating shadow; the Junimos' growing unease;
  the sabotage). It is **named/revealed later** (TLY2/3).

**The player is Morris's mirror.** You arrive as Joja's agent, sent to do exactly what Morris does. The
letter and the town turn you; the Junimos *freed you* at a crossroads. Morris chose Joja over connection
and was hollowed out. TLY3's "save Morris" lands because *you were almost him* — the version that didn't
get the letter in time. (Answers "why you?": the Junimos woke for the one Joja sent — the one who could
understand the enemy from inside.)

---

## 4. The "freed to choose" mechanic & grandpa's spirit

- During the opening town-walk, the player catches **glimpses of Junimos doing sparkle-magic on
  *them*** — actively *lifting the darkness's grip*. The reveal (in the CC) explains it: the player was
  **chosen because their heart was at a turning point**, and the Junimos **freed them to decide for
  themselves** — pursue darkness (Joja) or side with nature. They didn't make you good; they gave back
  your free will at the crossroads.
- **Why it's load-bearing:** the intro **demonstrates the TLY3 climax in miniature, on the player.**
  Freeing the player from the darkness so they can choose IS exactly what they'll do to Morris at
  Perfection. TLY1 *proves the redemption is possible by performing it on you first.*
- **Grandpa's spirit:** grandpa is dead (canon) and never knew about any darkness — so his letter is
  pure, ordinary love, which is *why* it works as the final key (uncorrupted love is the one thing the
  darkness can't hold against). His **spirit** (Grandpa's Shrine — outside the loop) becomes a recurring
  force pushing the player against the dark, and grounds the "a soul who remembers across loops" idea.

---

## 5. Cutscene moment map

All built as vanilla event scripts. Existing canon assets we reuse: the deathbed scene, the Joja
cubicle, the letter/deed, the bus, real villager + Junimo + Morris sprites.

### 5.1 Opening montage (one-time, plays on a fresh run; folds in the current porch/Junimo intro)
The legal hinge that fixes the old logic gap: the **deed** has been unseen since grandpa's death, so
ownership is in limbo; Joja taps the heir (you) to formalize it **so you can sell.**

1. **Deathbed prologue** *(reuse vanilla)* — grandpa gives child-you the sealed letter (contains the
   deed); passes. Letter/grandpa content stays innocent.
2. **Joja cubicle** *(reuse vanilla)* — adult-you, a Joja drudge.
3. **The assignment** — Joja's valley takeover needs the keystone parcel = grandpa's farm. They tap YOU
   (heir; personal connection weaponized) + dangle a payout to come formalize ownership and **sell it
   over,** greasing the wider acquisition.
4. **Bus stop → Robin's town tour** — Robin shows you around; you meet a few folk; you catch **glimpses
   of Junimos sparkling on YOU** (loosening the darkness).
5. **Robin walks you to the farm.**
6. **Lewis at the farm** — tells you the farm was **yours all along**, and to **open the letter.**
7. **Open the letter → the deed + grandpa's loving words.** Reading it **snaps the last chain of
   darkness.** You decide, right there, to fight Joja instead of sign.
8. **Lewis names the CC** — and calls it hopeless (no time, no skill, nobody).
9. **The CC → the Junimos** — they **explain** what just happened (heart at a turning point; freed to
   choose), then explain the loop (rewritten version of the current Junimo speech).
10. **Exiting the CC — Morris (first darkness glimpse + stakes).** Morris waits; draws the battle lines
    and starts the one-year clock diegetically: *"I hope you're not trying to do something with this old
    building. Joja gets what it wants — we want a warehouse. You may keep the farmland, but in one year
    this place will be **ours**, and there's no way you can stop it."* On **"there's no way you can stop
    it,"** Morris flips to a **glowing-red-eyes sprite** (palette swap). → drops you at the farm, Spring 1.

### 5.2 The rewind (season quota missed → year unwinds to Spring 1) — the signature
**Tone = dark spectacle that lands intimate.** Replaces the current static reset card.
- Void/black backdrop → darkness presses in (purple-black tint + shadow wisps + low ominous drone — *it
  gloats*) → **Junimos pop in around you, glowing, jittering as they strain** to push it back → screen
  **washes backward through the seasons** (winter-blue → fall-orange → summer-green → spring-pink tint
  sweep) → a **villager you'd bonded with flickers, throws a `?` emote, and turns away — forgetting you**
  (plants the déjà-vu thread) → white flash + reversed whoosh → wake in bed, Spring 1.

### 5.3 Season-passed beats (×3 per loop: Spring/Summer/Fall turns)
Short Junimo beats at the **end of Spring / Summer / Fall**; the counterpoint to the rewind (earned,
not punished). Each acknowledges progress / growing strength **and raises escalating unease** that
tracks the sabotage fronts: **end-of-Spring** still hopeful (nothing has struck yet) → **end-of-Summer**
uneasy (blight has appeared) → **end-of-Fall** alarmed (reversion has joined it), **foreshadowing the
unavoidable Winter requirement-tampering.** (Winter 28 is the win/rewind, not a season-pass beat.)

### 5.4 The win / ending (CC restored) — Triumph + ominous hook
- **Triumph first:** Joja **closes the town store**; **Junimo celebration**; the town whole.
- **Then the hook:** Morris retreats with the **shadow still clinging**, vowing Joja will "find greener
  ground" (→ Ginger Island / TLY2). You won the battle, not the war.
- **Flows into the JP shrine** (folds in the TODO's "win→shrine transition is jarring" fix — one
  continuous beat, not a hard cut). Works as a standalone 1.0 ending AND a Year-2 launch on the same save.

---

## 6. Active-opposition (sabotage) mechanic

**Purpose:** make the antagonist mechanically real (it *costs you turns*), justify the Junimos' rising
unease, and keep late seasons from going slack. **Thematic payoff:** the sabotage is invisible to
everyone but the player + Junimos — the town sees crops rot and progress slip and thinks *you're*
failing. Not just unremembered, not just unbelieved, but **alone with a truth no one else can perceive.**

**Forms + granular counterplay** (no blanket "ward" — split up, per TLY's upgrade philosophy):

| Form | Onset | Effect | Counterplay |
|---|---|---|---|
| **Blight / spoilage** | **Summer** | Crops / stored items rot overnight; the force poisoning the land. | **Per-season** "Protect crops" JP powers (Spring / Summer / Fall). |
| **Bundle reversion** | **Fall** | A completed donation slot quietly empties; redo required. | **Per-room** "Protect [Room] donations" JP powers (~6, one per CC room). |
| **Requirement tampering** | **Winter** | An un-donated bundle requirement switches to a different item (ideally not on hand) — attacks planning. | **WINTER-ONLY, UNAVOIDABLE — no power stops it.** The finale pressure valve. |

**Escalation schedule — each season opens a NEW front:**
- **Spring** — clean (learn the systems).
- **Summer** — **blight** begins → react by buying per-season crop protection.
- **Fall** — **+ bundle reversion** (blight continues) → buy per-room donation protection.
- **Winter** — **+ requirement tampering**, unavoidable, on top of the rest.

Each season hands you the prior season's JP to prepare a counter before the next front opens. The
Junimos' unease escalates because the darkness keeps finding *new* ways in; the Fall→Winter season-pass
cutscene foreshadows the unavoidable requirement-tampering specifically.

**Fairness guardrails:**
- **Telegraphed by supernatural signs** (shadow residue, unnatural chill, withering) — invisible to the
  town by design; perceivable only by player/Junimos. NOT a Joja paper trail.
- **Bounded** — it nibbles, never wipes a run.
- **Escalating** — nothing in Spring → unsettling by Fall → active assault in Winter.

**Open balance question:** Winter has no field crops — decide whether blight then targets stored items or
is simply absent in Winter (requirement tampering carries the season regardless).

---

## 7. Technical approach & integration

**Canvas:** vanilla event scripts (`Data/Events` command language). Extend the vocabulary the intro
already uses (`warp`, `viewport`, `addTemporaryActor`, `speak`, `playSound`, `pause`, `faceDirection`)
with the free primitives: camera pan/zoom, `globalFade`/fade, screen flash, **screen shake**, sprite
`move`/`jump`/`animate`, `emote`, full-screen color **tint**, existing particle/`temporarySprite`
effects, weather, music.

**Existing code this touches:**
- **`IntroEventInjector` / `IntroSequenceDriver`** — already event-based. Rewrite/extend
  `BuildIntroEvent()` into the 10-beat opening montage; add the Morris exit beat. Reuse the one-time
  gating (`HasSeenIntro` / `IntroEventKeys.CcSeenMail`) and `RunActivation.IsActive` dormancy.
- **`Day28CutsceneMenu` / `VictoryMenu` (custom menus) → convert to events.** `RunController` sets
  `_pendingCutscene` (`Day28Branch.Fail/Continue/Win`) in `OnDayEnding`; `Day28CutsceneDriver` currently
  opens a menu. Change the driver to **start an `Event`** (as `IntroSequenceDriver` does) for the rewind
  (Fail), season-pass (Continue), and win (Win) branches, with the event's end calling back into
  `RunController.OnCutsceneEnded`. Preserve the existing "open while the wake fade is still dark" timing.
  *(Note from memory: Day-28 was originally a self-drawn menu specifically because a vanilla event can
  fight engine player-placement on the wake frame — verify the event approach handles the bed/wake frame
  cleanly, or keep a thin menu shell that hosts event-like staging. Decide during planning.)*
- **Sabotage hooks the donation layer** — `DonationService.OnItemDonated` → `Run.RecordDonation`;
  `RunController.Requirements` (`BundleRequirement`) + `Catalog` (`CcItem`). Reversion = un-record a
  recorded donation; requirement tampering = swap a `BundleRequirement`'s target item; blight = a
  separate crop/storage pass. New service (e.g. `SabotageService`) gated by `RunActivation.IsActive`,
  driven on day-start with escalation keyed to season/week.
- **Counterplay powers** live in the JP shrine catalog (`JunimoShrineMenu` / upgrade purchase service),
  as new per-room / per-season entries.
- **Morris red-eye asset** — one palette-swapped sprite/portrait variant shipped in `assets/`.

---

## 8. Testing / verification

- Reuse existing debug commands: `tly_replayintro` (re-fire the opening), `RunController.DebugForceFail
  Reset` / `DebugForceContinueCutscene` / `DebugForceWin`, `DebugSetDay` — to trigger each cutscene on
  demand without playing a full season.
- Add debug commands to force each **sabotage** form and to set its escalation tier, so reversion /
  tampering / blight can be observed in isolation.
- Engineering correctness (events fire, no crash/leak, gating holds on non-TLY saves) is verifiable
  solo. Reserve user playtests for *meaningful* feedback: cutscene pacing/emotion, and sabotage
  difficulty/fairness (per the "meaningful playtests only" workflow rule).

---

## 9. Open questions (logged, non-blocking)

1. **Dialogue voice/tone** — how the Junimos / Lewis / grandpa's letter / Morris each "sound." (Could be
   a short follow-up brainstorm before writing the scripts.)
2. **Déjà-vu villager dialogue** — the half-remembering-across-loops system (Gribbleby). This spec only
   plants the thread (rewind forgetting beat); the full system may be its own spec.
3. **Named antagonist** — identity/reveal timing deferred to TLY2/3.
4. **Winter blight target** — stored items vs. absent (see §6).
5. **JP costs** for the new per-room / per-season protection powers.
6. **Day-28 event vs. menu** on the wake frame (see §7 note).

---

## 10. Out of scope / dependencies

- **One-continuous-save trilogy architecture** — DECIDED (all three run on one save; escalating win bars
  CC → basic Perfection → ultimate Perfection; a new Junimo-upgrade layer each year). This is a separate
  systems spec, logged on TODO (2026-06-06) with an explicit "remind the user to do this" flag. The
  cutscenes seed the trilogy regardless of when that architecture lands.
- TLY2 / TLY3 narrative content beyond seeding hooks.
