# The Longest Year (beta 0.9.2) — community announcement tracking

## 0.11.44 release update (2026-07-13) — POSTED
Update comment (`2026-07-13-01144-update-comment.md`) posted + verified on all three Reddit
threads (r/StardewValley 1txuhfb, r/StardewValleyMods 1txu610, r/SMAPI 1txtkb4) and as a reply
on the forums.stardewvalley.net thread (52534, BBCode variant). VeggieGirl43 got the Better-
Chests retest DM via Reddit chat (`2026-07-13-veggiegirl43-dm.txt`). Nexus posts tab skipped
by user decision (the mod page announces itself). Scripts: `tly-reddit-comment-01144.mjs`,
`tly-xenforo-reply-01144.mjs`, `tly-reddit-dm-veggiegirl-01144.mjs` (AC/release-notes).
NOTE: forums session had expired — logging in via `tly-forum-wait-login.mjs` re-armed it.

Mod facts (do not invent beyond these):
- Nexus: https://www.nexusmods.com/stardewvalley/mods/47192 · GitHub: https://github.com/sonofskywalker3/TheLongestYear
- PC-only, SMAPI 4.0+, new save on **Standard** farm, beta `0.9.2`.
- Hook: restore the Community Center within one year, or the Junimos rewind to Spring 1 and you start again a little stronger. Roguelite time-loop: per-season donation minimums; Junimo Points bank across loops → carry-forward upgrades (skills, tools, recipes, buildings, kept pet); weekly themes (paired bonus+liability); carryover surfaces (Bundle Log, Cookbook/Craftbook, Junimo Stash).
- Explicit ask: feedback on **difficulty, pricing, pacing** + bug logs.
- Art call: book / sprite artwork (Cookbook/Craftbook). **Banner DONE 2026-06-06 — made by cwybabiesucks** (from the r/StardewValleyMods thread); now live on Nexus + credited in README/description. Drafts updated to drop the banner ask + credit them.

## Venue rules (researched 2026-06-05)

| Venue | Exists / active | Relevant self-promo rule | Verdict |
|---|---|---|---|
| **r/SMAPI** (~8.1K) | Yes | Rule 2: threads must be SDV-mod-relevant — "questions, **announcements**, or discussions about SMAPI, modding in general, or specific mods" explicitly allowed. Rule 5: no links to other subreddits/Discords (Nexus/GitHub fine). | **KEEP — best fit** (modder/playtester audience, announcements welcome) |
| **r/StardewValley** (~1M) | Yes | Rule 8 "Limits on promotion": **"Mod pages"** are explicitly listed as fine to post directly (Let's Plays / streams / Discord ads are not). Rule 11: flag modded content with **Modded** flair. Rule 3: descriptive title, no duplicate within 2 months. | **KEEP** (huge reach; must use Modded flair) |
| **r/StardewValleyMods** (~1.1K weekly contributions) | Yes, active | Community purpose: "a place where the community can share ideas and **discuss mods** for Stardew Valley." No restrictive promo rule widget; users routinely share their own mods/guides. | **KEEP** (on-topic by design) |
| **forums.stardewvalley.net** — Modding Discussion & Creation / Mods (Resources) | Yes | Mod releases go in the **Mods** (resource manager) section, which auto-creates a discussion thread; the Modding Discussion forum is for showing off / discussing mods. General rule: no thread whose *main intent* is advertising — a mod release in the modding area is expected, not "advertising." Don't relink others' mods (mine is fine). | **KEEP** (official forum) |
| **community.playstarbound.com** — Mod Releases/WIPs (sdv-mods) | Yes, active (latest posts Oct 2025) | Purpose-built "Mod Releases/WIPs" board. | **KEEP — lower priority** (smaller/older but valid) |

No venue dropped. Recommended posting order: r/SMAPI → r/StardewValleyMods → forums.stardewvalley.net → r/StardewValley → playstarbound.

## Status (2026-06-05)
- [x] Researched venue rules
- [x] Drafted + approved + POSTED: r/SMAPI, r/StardewValleyMods, r/StardewValley, forums.stardewvalley.net (4 live)
- [ ] playstarbound — drafted + approved, blocked on account email activation; post when the user is logged in
- FYI for the author: live Nexus description + README still say "The Junimo Shrine. Spend JP on upgrades…" but JP is actually spent at the loop reset (not at a shrine). Same wording corrected in these announcement drafts. Consider fixing the Nexus/README copy later.

Reddit login state (2026-06-05): the dedicated Chrome profile is **logged OUT of Reddit** (Nexus only). Reddit posting needs either (a) a one-time Reddit login in that profile so I can automate, or (b) manual copy-paste by the user.

| Venue | Approved? | Posted URL |
|---|---|---|
| r/SMAPI | **APPROVED + POSTED** (flair: "new mod") | https://www.reddit.com/r/SMAPI/comments/1txtkb4/beta_the_longest_year_a_roguelite_timeloop_over/ |
| r/StardewValleyMods | **APPROVED + POSTED** (no flair) | https://www.reddit.com/r/StardewValleyMods/comments/1txu610/beta_i_made_a_roguelite_timeloop_mod_restore_the/ |
| forums.stardewvalley.net | **APPROVED + POSTED** (Modding Discussion & Creation, WIP prefix; logged in as SonofSkywalker3) | https://forums.stardewvalley.net/threads/beta-the-longest-year-a-roguelite-time-loop-over-community-center-restoration-pc-smapi-4-0.52534/ |
| r/StardewValley | **APPROVED + POSTED** (flair: "Mods"; title prefix changed [Modded]→[Mods] for flair coherence) | https://www.reddit.com/r/StardewValley/comments/1txuhfb/mods_my_timeloop_mod_the_longest_year_is_in_beta/ |
| playstarbound | **DRAFTED + APPROVED — BLOCKED** on account activation (registration email not yet received as of 2026-06-05). Draft ready at marketing/draft-5-playstarbound.md; post to Mod Releases/WIPs (/forums/sdv-mods/) once logged in. NOTE: playstarbound runs older XenForo — re-recon the editor (it may not have the Froala "Toggle BB code" the SDV forum has) before automating. | — (pending) |

## Engagement harvest — r/StardewValley thread (2026-06-05)
OP Reddit handle: **u/Plastic-Difference-3**. Thread: **98 upvotes, 30 comments, 20k+ views** — the standout.

**Community contributions (credit if they ship):**
- **u/Gribbleby** → déjà-vu / "villagers retain some memory → Groundhog Day dynamics" idea → captured in `TheLongestYear/TODO.md` [1.0.0].
- **u/dcempire** → "give the CC importance after you complete it" idea → captured in `mod-ideas.md` #3.

**Other useful signal:**
- **YouTuber outreach** — OP's shortlist: CharlieBarley, Salmence, Fungus; commenter (u/Khajiit-ify) added **Emmalution** (challenge-run channel). Lead for promo.
- **Compatibility** — OP confirmed in-thread it ran alongside Stardew Valley Expanded (disabled only for testing); no hard block on other mods, wants conflict reports.
- **Recommended pairing** — OP suggested **remixed bundles** for added challenge (re-shuffles bundles/items each run) — relevant to the bundle-randomizer eval in `mod-ideas.md` #2.
- No real bug reports in this thread (mostly enthusiasm + questions). One downvoted troll (u/throwawayt44c, -24) — ignore.

### Other threads — sweep 2026-06-05 (r/StardewValley is the goldmine; others minor)
- **r/SMAPI:** 3 upvotes, **0 comments.** Quiet.
- **r/StardewValleyMods:** 26 upvotes, 8 comments. All positive, **no bugs, no new creditable ideas.** Notables:
  the recurring "poor Kent is stuck at war forever (nobody else remembers the loop)" joke (u/concentrate7 et al. — possible
  flavor opportunity), and **u/astralprojekts** praised the *no-AI-art* placeholder approach and suggested the free pixel
  tool **LibreArt** (relevant to the "art wanted" call). u/Lagao raised the "unlucky farmer never gets red cabbage" RNG
  worry — OP noted an upgrade eases it (ties to the JP-makes-items-reachable design).
- **forums.stardewvalley.net:** **0 replies** yet. (Title confirms the **WIP** prefix applied correctly.)
- **YouTuber outreach list:** `marketing/youtuber-outreach.md` (CharlieBarley, Salmence, Fungus, Emmalution).
