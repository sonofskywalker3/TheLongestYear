# HANDOFF — 2026-08-21 — work to put into 0.12.x before it releases

Paste the **Prompt for the next agent** section into a fresh session, run from
`C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear`.

## Where things stand

- **Public:** `v0.12.0-beta.1` (GitHub + Nexus). All bug threads answered; the two private reports
  (1117543 muting, 1113831 Day-3 crash) are **Needs more info** with no log/save yet.
- **Local `master`, unreleased: `v0.12.3`** — 0.12.1 TLY Custom dropdown (eyeballed), 0.12.2/0.12.3
  empty-theme text ("Themed donations completed" on the hub card; "Themed donations completed -
  drawback lifted." HUD). Full loop-reset smoke of every beta bugfix PASSED 2026-08-21 (table in
  `TODO.md` "SMOKED 2026-08-21"). 670/670 tests. Working tree clean. **User decided NOT to release
  yet** — the items below go in first.
- A 0.12.2 release-notes commit was made and then reverted (`3d0e98f` → `2d908a8`): README,
  `docs/nexus-description.bbcode`, `CHANGELOG.md` still match the live 0.12.0-beta.1 page. Write
  release docs only once, at the end.

## Balance feedback — what we know (all against 0.11.60 / pre-engine)

khauser13 won first try on standard AND remixed; newmaaly won Winter 6 of loop 1 ("lucky remix, no
Brewer's/Dye"); Tutorem "CC very doable in Y1", ~1,000 JP by Spring 22 under the OLD double-pay
economy; xsansara breezed Spring; PokeTheSilver204: red cabbage is the only Y1 blocker and the 750 JP
cult upgrade deletes it after one loop; Dusklight7: deliberate Spring-28 reset farming is a JP cheese
path; jneedham2: a lucky cart buy (red cabbage/truffle/sandfish) upends a run; Thrippalan (the
intended experience): failed Summer on red cabbage, failed Spring 2 on catfish/rain, reached Fall in
loop 3.

What the engine already changed: board re-rolls every loop (kills remix luck / memorisation),
Vault +25%, quality asks, x40–99 forage stacks, authored bundles, checkpoint JP 150/250/400, donation
JP paid once (income DOWN), weather density fixed (Spring had 6 wet days on the smoke save), one-item
cart cap (JP-upgradable). What did NOT change: red cabbage + starfruit are still the only genuinely
year-1-impossible asks and the engine keeps rolling them; the cult upgrades are still "delete the
hard gate" buys. **Nobody has measured JP-per-loop on the engine.** That number, not opinion, gates
the repricing.

## Rules that bit (don't relearn)

- Versioning: plain semver, PATCH bump on every code commit (0.12.4, 0.12.5 …) until Jeff declares
  0.13.0; docs-only commits don't bump. Never push/release/post without an explicit "yes, push."
- Nexus/Reddit: Claude-in-Chrome on Jeff's regular browser. README ≡ Nexus description.
- Game-driving tooling from this session (lived in the session scratchpad; recreate if needed):
  console injection = `tools/send-smapi-command.ps1` (vanilla `debug …` works: `warp`, `sleep`,
  `season`, `completecc`, `spreaddirt`, `clearfarm`, `growcrops`, `ebi`, `setfarmevent`, `time`);
  screenshots via `PrintWindow` (call `SetProcessDPIAware` first); keys MUST be sent with
  `keybd_event` + `MapVirtualKey` scan codes (SendKeys is ignored by MonoGame); the game PAUSES while
  unfocused — click into the window after every console command or nothing processes; Gunther's
  "Action" tile is the counter front, not his own tile. Clone `None_443632257` as
  `ZZZSMOKE<N>None_443632257` (inner file renamed to match; no hyphens — the game strips them),
  load with `tly_loadsave <folder>`; the in-place reset rotates the folder to `None_<newId>` and
  deletes the pre-reset one. Delete clones when done.

## Prompt for the next agent

```
I'm continuing The Longest Year (Stardew Valley SMAPI mod). Read, in order: the workspace
`.claude/CLAUDE.md` (one folder up), `STATUS.md`,
`docs/superpowers/HANDOFF-2026-08-21-pre-0.12-release-work.md`, and the "SMOKED 2026-08-21"
entry at the top of `TODO.md`. Local master is v0.12.3, unreleased; the 0.12.0-beta.1 page is live.

Versioning: plain semver, PATCH bump on every code commit (0.12.4, 0.12.5 …) until I say 0.13.0.
Never push, release, or post anywhere without my explicit "yes, push." Brainstorm → spec → plan
before anything creative (Superpowers). Small commits, one change each.

Do these, in this order. Stop and ask me at the marked rulings.

A. QUICK ONES (each its own commit)
  1. Advanced Options screenshot for the mod page (promised to khauser13): launch the deployed
     build, New → Advanced Options, capture the dialog showing "TLY Custom", save as
     `release-notes/advanced-options-tly-custom.png`, and wire it into the README + Nexus Install
     step (content-identical; the Nexus image itself is uploaded later, by me, via the browser).
  2. Eyeball the "Themed donations completed" card line. Get a save into a state where a theme's
     open-slot pool is empty (donate that theme's remaining slots via `tly_donate` / `tly_testdonate`
     against the live board — read `RunController.PopulateBonusSlotsForCurrentSelection` +
     `SampleSlotsForTheme` to see what "empty" means), open the hub, screenshot, fix layout if needed.
  3. `EnableNonObjectDonations` mid-loop caveat: make an in-flight Gil's Trophies bundle
     donatable (or auto-complete/replace it) when the flag is turned off mid-loop, so the caveat
     can be deleted from the docs. Spec first; smallest safe fix.
  4. Curated per-name quota ramps for the remix pool: `BundleClassifier` derives
     `floor(X*[0.25,0.5,0.75,1.0])` for unknown X-of-Y bundles. Survey the live remix + authored
     names (`tly_genbundles` / `tly_classify` output), propose curated ramps where the derived one
     is clearly wrong (e.g. Winter asks). Spec → RULING FROM ME before implementing.

B. CULT REPRICING — MEASURE FIRST
  5. Build a diagnostics-only console command `tly_jpbudget` that, for the CURRENT loop's generated
     board, computes the maximum attainable JP in that loop: sum of every slot's donation JP
     (JpCalculator, season multipliers as they'd apply by the slot's earliest season) + checkpoint
     awards + weekly-theme bonuses at the cap + room-completion bonuses. Log a per-season breakdown
     and the total. Run it on a cloned save across 5 different loop seeds (`tly_reset` between;
     unattended resets must use `tly_reset`, not `tly_failreset`) and record min/median/max in
     `docs/superpowers/notes/2026-08-XX-jp-budget.md`.
  6. With that number, propose prices for the audit list — `cult_red_cabbage` (750),
     `cult_starfruit` (750), `keep_bus_unlocked` (1500), `fortune_rare_fish` (525) and the Cart
     Stall tiers — against the rule "a hard-gate deletion should cost more than a strong player
     banks in one loop". RULING FROM ME before changing `UpgradeCatalog`. Also propose whether the
     cult upgrades should become per-season obtainability (e.g. cabbage seeds at Pierre in Summer)
     rather than a mixed-seeds roll, if the budget shows the roll is the wrong lever.

C. BUNDLE SOURCE: ENGINE | VANILLA (brainstorm + spec, then build)
  7. Design `BundleSource` config (`Engine` default | `Vanilla`). Vanilla = keep whatever board
     the save has (vanilla Standard/Remixed OR another bundle mod's), classify it via the existing
     `LegacyReadAndClassify` path, and on every reset regenerate the vanilla board honouring the
     player's Standard/Remixed choice — that means persisting `Game1.bundleType` in `MetaState`
     (the root cause of Nexus bug 1108030). Audit `BundleClassifier` gaps that ada113 hit with
     Challenging CC Bundles (season goals loaded partial items/quantities/qualities) — Vanilla mode
     is the cheap route to that compat ask, so classification must be complete for arbitrary
     boards. UX question for my RULING: the new-game Advanced Options row currently shows only
     "TLY Custom"; under Vanilla it should show Normal/Remixed again — decide whether "TLY Custom"
     becomes a third dropdown entry that sets the config, or the config drives the dropdown.
     Also decide what happens when the config flips mid-loop (recommend: takes effect at next
     reset, say so in GMCM). Spec → RULING → plan → implement → tests → smoke a reset in each mode.

D. WHEN A–C ARE GREEN
  8. Release docs once: What's New in README + `docs/nexus-description.bbcode` (identical),
     `CHANGELOG.md` entry covering 0.12.1 → 0.12.N, `release-notes/<ver>-nexus-changelog.txt`,
     STATUS/TODO refresh, commit. Then STOP and ask for "yes, push" (then `release.ps1
     -SkipNexusDesc`, description/version sync + changelog paste via Claude-in-Chrome, verify live).

Parked for 0.13.0 (don't start): difficulty toggle / Golden-Jewel-Legendary hard bundles,
multiplayer, JP spend after a successful season, befriending side-quests, netWorldState keep/wipe
audit, reset-path consolidation, Cart Catalog porch-crate loop pollution, animated loop/ending
cutscene, déjà-vu dialogue.
```
