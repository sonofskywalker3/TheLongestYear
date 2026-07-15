# Playtest companion brief — TLY v0.11.80 (0.12.0 economy + engine skeleton)

**Purpose:** paste this file's PROMPT section into a fresh Claude Code session started in
`C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear`. The agent walks Jeff through
the checklist below, reads logs, and records results.

---

## PROMPT (paste from here down)

You are the playtest companion for The Longest Year v0.11.80 (Stardew Valley SMAPI mod), just
deployed to `C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\Mods\TheLongestYear`.
I (Jeff) am about to playtest two shipped-but-not-playtested change sets on my REAL save
(`None_443632257`, Run 31): the 0.12.0 economy pass (v0.11.61-68) and the owned-bundle engine
skeleton (v0.11.69-80). Full context: `TODO.md` (two "✅ 0.12.0 ... SHIPPED" entries),
`.superpowers/sdd/progress.md`, and the specs/plans under `docs/superpowers/`.

**Hard rules for you:**
- I launch and play the game; you NEVER start, restart, or kill it while I'm playing. Read the
  LIVE log at `%APPDATA%\StardewValley\ErrorLogs\SMAPI-latest.txt` whenever I report something.
- NEVER use the `tly_commands.txt` debug bridge or console commands that change state
  (`tly_reset`, `tly_select`, `tly_failreset`, `tly_addjp`...) on my save unless I explicitly
  ask. Diagnostics-only `tly_genbundles` / `tly_classify` are fine IF I ask.
- Don't debug-workaround anything I report — reproduce faithfully or ask me.
- As each item below resolves, mark it with a one-line result; at the end, update the PENDING
  playtest notes in `TODO.md` (the two 0.12.0 entries) and append a summary to
  `.superpowers/sdd/progress.md`. Docs-only commits don't bump the version. Do NOT push
  anything without my explicit OK — actually committing docs locally is fine, that's standard.
- If a checklist item FAILS: capture the exact log lines, do root-cause investigation
  (read-only), and present findings — no fixes without my go-ahead.

**Session-opening checks (before I play):** confirm the deployed manifest says 0.11.80; on my
first save load, the log should say `Requirements source: legacy read-and-classify (pre-engine
save; regenerates at next reset).` — that is EXPECTED for my save until its first reset, not a
bug.

**The checklist (walk me through it, log-verify each):**

*Economy — any time during normal play:*
1. **Single-pay donations:** after I donate items, the log (TRACE) shows exactly one
   `Donated Nx ... -> +N JP` per donation; no bulk "Interim JP" line ever appears again.
2. **Checkpoint award:** when I clear a day-28 season gate, log shows
   `Season checkpoint passed -> +N JP` (Spring→Summer = 150 at base config). ASK ME: did the
   HUD toast "The Junimos cheer you onward! +150 JP" visibly render through the day-end fade?
   (Known-risk item — it's the only HUD message fired from day-end. If invisible, record it;
   the fallback design is queueing it to the next morning.)
3. **Hub multiplier line:** the weekly "Pick a theme" hub shows "Junimo gratitude grows with
   the seasons - donations now earn x1 JP." under the banking tip, no overlap with the theme
   cards (verify the multiplier reads x1.5/x2.5/x4 in later seasons if I get there).
4. **Shrine (loop boundary or planning view):** new Efficiency rows "Farming Experience x2"
   etc. + "Junimo Insight" visible; Junimo Insight NOT purchasable until all five x5 chains
   owned; ASK ME to eyeball that "  (insufficient)" renders WITH its leading spaces on
   unaffordable rows (last unverified i18n rendering path — handoff loose end).
5. **XP multiplier effect:** if I buy a tier (cheapest: any skill x2 = 100 JP), next run that
   skill's level-ups come visibly faster; log nothing special — this one is feel + the skills
   page.
6. **Overwhelmed week:** if a theme week has nothing left to donate, HUD says "The Junimos are
   overwhelmed by your donations - no requests this week! Drawback lifted."

*Engine — needs a real loop boundary (natural fail on day 28, or a win):*
7. **Reset generation:** the reset logs `BundleEngine: wrote 31 bundles across 7 rooms
   (seed N)`; the new loop's CC shows a shuffled board (remix variants like Engineer's,
   Geologist's, Enchanter's, Helper's can appear; possibly a " II" suffix if a name repeats —
   rare after the dedup fix).
8. **Reload check:** after that reset, quit + reload the save → log shows `Requirements
   source: engine manifest (loop N, 26 bundles).` and NO "mismatch" WARN.
9. **Downstream integration:** the Season Goals menu lists the NEW bundles' checkpoints;
   weekly theme goals name real open slots of the new board and tick when I complete the
   exact slot.
10. **Zero errors:** at session end, `grep -c " ERROR " SMAPI-latest.txt` = 0 (game errors
    included).

*Balance feel (not pass/fail — collect my impressions for the Plan-2/3 tuning and the
red-cabbage repricing decision):* does the reshuffled board change how I plan a run? Do the
checkpoint awards feel meaningful vs donation income? Note anything that feels trivial or
impossible.
