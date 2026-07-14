# Handoff — 2026-07-14 session (v0.11.60 SHIPPED; pre-0.12 queue CLOSED; next = 0.12.0)

**Repo state:** master at `ca31c89`, clean tree, all pushed. **v0.11.60 released to GitHub +
Nexus 2026-07-14** and deployed to PC Mods (game may be running the user's Run 31 test save —
never force-close it; read the live log). Suite: 563/563.

## What shipped this session

### The full pre-0.12 queue (user directive) — ALL THREE DONE
1. **Advanced Options screenshot (khauser13)** — captured via `test-output/printwindow.ps1`
   (now finds the game window by `SDL_app` class AND is DPI-aware — two real bugs fixed),
   cropped + red-highlighted, uploaded to the Nexus 47192 media gallery. Asset committed:
   `release-notes/advanced-options-remixed.png`. Caption shows "No title" (new Nexus edit UI
   exposed no caption field to automation) — acceptable, image is self-explanatory.
2. **i18n extraction (v0.11.46–0.11.60)** — every player-visible string (~250) now in
   `src/TheLongestYear/i18n/default.json`. Spec:
   `docs/superpowers/specs/2026-07-13-i18n-extraction-design.md`; plan:
   `docs/superpowers/plans/2026-07-13-i18n-extraction.md`; executed subagent-driven (per-task
   review + final whole-branch review = READY, all fixes landed). Architecture: `Strings`
   facade in Core (delegate injected by ModEntry with `.Default(key)` echo), lazy key
   resolution in `UpgradeDefinition` (id-derived keys + `upgrade-tpl.*` templates with
   `i18n:`-prefixed lazy tokens), `ThemeDisplay`/`QualityTags` helpers, GMCM live-switch
   lambdas, mail/furniture re-inject on LocaleChanged. Guard tests (563 total) fail CI on
   missing/orphan keys or broken `{{tokens}}`. English is byte-identical to 0.11.44.
   `docs/TRANSLATING.md` = translator guide (warns: `furniture.*` and `event.intro.*` values
   must not contain `/` or `"`). SDD ledger: `.superpowers/sdd/progress.md`.
3. **Nexus upload v3 migration — VERIFIED LIVE.** The v3 `file_id` IS the old
   `file_group_id`. TLY's release run 29331424115 exercised it successfully; probe workflow
   deleted. AC (`7118491`) + CartCatalog (`7497950`, branch `main`) migrated but unexercised
   until their next releases; rollback pin `ee1af4be` + old input names dies 2026-09-09.

### Release/community actions (all user-approved this session)
- `release.ps1 -SkipBuild` shipped v0.11.60; description + version synced (Translations
  section + What's New live; README ≡ bbcode maintained).
- **Fluxwb reply posted** on TLY's posts tab (bilingual @Fluxwb comment, source
  `release-notes/fluxwb-reply-01160.txt`, posted via
  `AndroidConsolizer/release-notes/tly-reply-fluxwb.mjs`): translations = JSON file now,
  no DLL edits. Awaiting their updated zh.json → then credit + link on the mod page.

## Loose ends (small)
- **Nexus CHANGELOG field paste for 0.11.60** — the one manual step, text ready in
  `release-notes/0.11.60-nexus-changelog.txt`. USER does the browser paste.
- **Leading-space eyeball**: next time a shrine menu is open in-game, confirm
  "  (insufficient)" / " (gold)" render with their leading spaces (only rendering path
  verified via tests but not yet eyeballed live; everything else verified).
- Fluxwb zh.json follow-up when they respond; VeggieGirl43 BC-retest DM still unanswered;
  next forum sweep (`sweep-forums.mjs`) will catch 0.11.60 reactions.
- `EnableThemeReroll: true` still set in the DEPLOYED config.json (playtest convenience).

## NEXT MILESTONE: 0.12.0 balance/clarity
Roadmap memory `tly-012-013-014-roadmap.md`: 0.12.0 clarity/balance → 0.13.0 owned-bundle
difficulty engine → 0.14.0 seasonal escalations. Driving inputs: three players finished
year 1 without looping (khauser13's remixed run, PokeTheSilver204's red-cabbage-cultivation
observation — see the posts-tab digest in this session's scrape), plus Dusklight7's notes.
Start with **brainstorm → design spec** (Superpowers flow). Remember: roguelite — failure is
intended; user prefers minimal fixes over new systems (`feedback_minimal_fixes...`).
Parked separately: ChampionService/CurrentChampion code rename (do NOT fold into 0.12.0).
