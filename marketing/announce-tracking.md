# The Longest Year (beta 0.9.2) — community announcement tracking

## 0.16.0 release announcement (2026-08-27) — PARTIALLY POSTED

The "last big engine update" announcement: ten difficulty dials plus the derived season gates.
Framing agreed with Jeff: harder now, but heavily customisable, so play with it and report what
feels terrible or broken. Bug reports to the Nexus page with SMAPI logs. Body text is the same
across venues; only the title prefix differs.

| Venue | Status | URL |
|---|---|---|
| r/StardewValleyMods | **POSTED** as a new thread (no flair) | https://www.reddit.com/r/StardewValleyMods/comments/1w018co/the_longest_year_0160_is_out_harder_but_now_you/ |
| r/SMAPI | **POSTED** as an update comment on the original thread (Jeff's call after the new-post composer failed) | https://www.reddit.com/r/SMAPI/comments/1txtkb4/beta_the_longest_year_a_roguelite_timeloop_over/ |
| r/StardewValley | **POSTED** as an update comment on the original thread | https://www.reddit.com/r/StardewValley/comments/1txuhfb/mods_my_timeloop_mod_the_longest_year_is_in_beta/ |
| forums.stardewvalley.net (thread 52534) | not attempted this round | — |
| playstarbound | still unposted (account activation, from 2026-06-05) | — |

**Automation lesson (2026-08-27), costly, read before the next round.** Reddit's composer and
comment boxes accept a click WITHOUT taking keyboard focus: `document.activeElement` stays on
`BODY`. Typing then goes to the page as **keyboard shortcuts**, not text. That silently ate the
first paragraph of the r/SMAPI comment, and `h` hid the post twice (recovered both times with the
"Undo" banner; verified afterwards via `r/SMAPI/comments/<id>.json` that `hidden` and `saved` were
both false).

**Always do this:** click the field, then check
`document.activeElement.isContentEditable === true` with `javascript_tool`, and only then type.
If focus is on `BODY`, scroll the field into view and click it by screen coordinates rather than
by element ref; the ref click alone often does not focus it.

Also: `ctrl+a` inside the comment editor can blur it, after which the next keystrokes act as
shortcuts again (one of them navigated the tab to `/submit/`). Prefer replacing text by selecting
inside the editor and retyping in one `type` action, and re-verify focus first.

The new-post composer on r/SMAPI and r/StardewValley was never made to work for the same reason.
Jeff's call was to post update comments on the existing threads instead, which is also what the
0.11.44 round did.

**Rules re-confirmed, and a correction:** r/StardewValley Rule 8 explicitly lists **"Mod pages"**
as fine to post directly, and Rule 11 asks for the **Mods** flair. The pinned "Self-Promo Tuesday"
megathread does NOT gate mod releases, and the 2026-06-05 `[Mods]` post is still live with 124
upvotes and 63 comments. Do not talk yourself out of posting there.

**Note on format:** past updates (e.g. 0.11.44) were posted as *comments on the existing threads*
rather than as new posts. This round Jeff asked for new posts. Both remain valid; comments on the
old threads reach the people already subscribed to them.


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

## Sweep 2026-08-26: a streamer found the mod

**emmalution** (YouTube, 82.7K subs) has been running TLY as a challenge series since 16 Jul 2026.
Spring episode 53.7K views, Summer 17K, plus livestreams LIVE 01-05. Nexus link + beta/Standard-Farm
caveat in every description; suggested to her by Tired Ginger Bri in her Discord. Details and the
bug/design signal harvested from ~140 of her comments are in TODO.md (2026-08-26 sweep) and
marketing/youtuber-outreach.md.

Reddit r/StardewValley thread picked it up too: Thrippalan (26 Aug) said her husband started the mod
after emmalution's videos. Jeff replied there and left a comment on the Spring video asking for
feedback. Other venues unchanged: r/StardewValleyMods and r/SMAPI quiet since 13 Jul, the SDV forum
thread still has zero outside replies, playstarbound still unposted.
