# HANDOFF — 2026-08-21 — after the 0.12.0-beta.1 release

Paste the **Prompt for the next agent** section into a fresh session, run from
`C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear`.

## Where things stand

- **Public:** `v0.12.0-beta.1` is live on GitHub + Nexus (file via workflow run 32484136636,
  description + version field synced from Jeff's regular Chrome). All nine 0.11.60 bug threads
  are answered and set **Fixed**; posts-tab replies to faldans / CausticOptimist / SilencedLink /
  Bumblewyn; Reddit reply to Thrippalan. The two **private** reports (1117543 muting, 1113831
  Day-3 crash) are answered and set **Needs more info**.
- **Local `master`, unreleased:** `v0.12.1` = new-game Advanced Options "Community Center
  Bundles" dropdown shows a single **TLY Custom** entry (+tooltip) while the mod is enabled
  (`src/TheLongestYear/Loop/BundleOptionPatch.cs`). Build clean, 670/670 tests, deployed to PC
  Mods. **Not yet eyeballed in-game.**
- `TODO.md` → "✅ RELEASED 0.12.0-beta.1" entry has the full root-cause/fix matrix; `STATUS.md`
  is current; `CHANGELOG.md` has the consolidated entry.

## Rules that bit this session (don't relearn them)

- **Versioning:** no `-beta`/`-rc` suffixes ever again — below 1.0 *is* beta. Every code commit
  bumps PATCH (0.12.2, 0.12.3 …) until Jeff declares 0.13.0. (User memory
  `tly-versioning-no-prerelease`.)
- **Nexus/Reddit posting:** use Claude-in-Chrome on Jeff's regular browser, not the Playwright
  automation profile (its Nexus session is dead; `release.ps1`'s description step will fail — run
  `release.ps1 -SkipNexusDesc` then sync the description by hand). Legacy bugs-tab mechanics +
  flood-control gotcha are in user memory `nexus-use-regular-chrome`.
- **Never push / post without an explicit "yes, push."**
- README ≡ Nexus description (house style); bump manifest on every code commit.

## Prompt for the next agent

```
I'm picking up The Longest Year (Stardew Valley SMAPI mod) right after the 0.12.0-beta.1
release. Read, in order: `.claude/CLAUDE.md` (workspace rules), `STATUS.md`,
`docs/superpowers/HANDOFF-2026-08-21-post-0.12-release.md`, and the top "✅ RELEASED
0.12.0-beta.1" section of `TODO.md`. Local master is at v0.12.1 (unreleased).

Versioning: plain semver, no prerelease tags; bump PATCH on every code commit (0.12.2, 0.12.3…)
until I say it's 0.13.0. Never push, release, or post anywhere without my explicit "yes, push."
For Nexus/Reddit use Claude-in-Chrome on my regular browser (see user memory).

Next steps, in priority order:

1. **Eyeball the TLY Custom dropdown.** Launch the deployed build, title screen → New →
   Advanced Options: the "Community Center Bundles" row must show a single "TLY Custom" entry
   with its tooltip, and a new farm must still start normally. Fix `BundleOptionPatch` if not.

2. **Human smoke of a loop reset on the 0.12.1 build** (clone a save first). Checklist from the
   bugfix pass: deposit in the Junimo Stash on day 28 → survives; kept coop comes back WITH the
   hay hopper; kept rod keeps bait; re-donate an artifact → museum reward re-granted; Caroline's
   2-heart tea event replays; Rain Totem → rain next day, and a season has >2 wet days; Mixed Seeds
   in Summer roll Red Cabbage/Starfruit with the Cultivation upgrade; CC win → the Town ceremony
   plays; a FAIL night with an overnight farm event still rewinds. Log what you ran + what passed.

3. **Sweep feedback** on the beta: run
   `AndroidConsolizer/release-notes/sweep-forums.mjs` (needs NEXUS_PW_PROFILE — works for public
   pages) and check the two Needs-more-info private bugs in the browser. Muting (1117543): the log
   ends in `[ALSOFT] (EE) Failed to get padding: 0x88890004` = Windows audio device lost → mark
   Not a bug once a full log confirms. Day-3 crash (1113831): if a log/save arrives, investigate
   TLY's quest/mail patches (`RatProblemQuestPatch`, `OnboardingMailService`, weekly-theme quest
   service) around accepting Emily's help-wanted post.

4. **Promised to Bumblewyn:** reword the "no open slots this week → drawback lifted" message so
   players don't read empty themes as a bug (`hud.nothing-to-donate` in i18n + wherever the hub
   shows it). Small commit, bump PATCH.

5. **When 1–4 are green, release 0.12.x:** What's New in README + `docs/nexus-description.bbcode`
   (identical content), `CHANGELOG.md`, `release-notes/<ver>-nexus-changelog.txt`, commit, then
   `pwsh -NoProfile -File release.ps1 -SkipNexusDesc` (after my "yes, push"), then sync the Nexus
   description/version via Claude-in-Chrome and verify on the public page.

6. **Parked for 0.13.0 (ask me before starting):** Normal-bar `PoolTuning` playtest loop + cult
   (red cabbage/starfruit) repricing; feature asks from the sweep — multiplayer, Challenging CC
   Bundles compat / JP preset, difficulty toggle, spending JP after a successful season,
   befriending side-quests.

Start with step 1 and tell me what you see before touching anything else.
```
