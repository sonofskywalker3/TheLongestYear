# SDD ledger — plan: docs/superpowers/plans/2026-09-21-darkness-agents-and-gate-scenes.md

Spec: docs/superpowers/specs/2026-09-21-darkness-agents-and-gate-scenes-design.md (read; binding).
Branch story, working in the main checkout (tools/deploy.ps1 and the game's Mods folder are tied to this path; story is not the release line).
Start BASE: see `git log` at "docs(spec): the thief scene shows the worst-hit chest".

## Pre-flight scan

| Tasks | Produces vs consumes | Found |
| --- | --- | --- |
| 1 alone | tests call Lines(kind, rewound); code defines it; AllLineKeys used by I18nGuard | agrees |
| 2 alone | deletes SabotageMailService; SabotageService ctor loses `mail` param; ModEntry call site updated in same task | agrees |
| 3 -> 5 | StrikeGuarantee.ForcedTonight(Season,int,IReadOnlyCollection<string>,Func) consumed in RunNight with Run.StruckEvents (HashSet<string>) | agrees (HashSet implements IReadOnlyCollection) |
| 4 -> 5,6 | StrikeScenes.IsDue/IsSkippable/MarkPlayed; RunState.StrikeScenesPlayed, MetaState.StrikeScenesSeen | agrees |
| 5 -> 6 | SabotageService.Pending, ApplyPendingIfAny(string), SceneCanPlay (default false) | agrees; Task 5 is shippable alone because SceneCanPlay defaults false |
| 5 -> 7,8 | PendingStrike.CropTiles, .Hits (SpoilagePass.Hit with Chest, Location) | agrees; Task 5 sets Entry.Location for chests |
| 6 -> 7-10 | StrikeSceneBase(strike, skippable, monitor, onFinished), Stage/Build/Paint, stub scenes created in 6 and replaced later; ThiefScene.PickChest needed by factory in 6, full code shown in 8 | Ruling below |
| 6 -> 12 | factory lambda calls _witness?.OnScenePlayed; plan says omit until Task 12 | agrees |
| 7 -> 8,9 | SceneCamera created in 7, used by 8 and 9 | agrees (sequential) |
| 11 alone | reads RunState.Tampers (exists) | agrees |
| 12 alone | tests vs WitnessLines code | agrees |
| 13 | reads everything | agrees |

Ruling: Task 6 must include ThiefScene.PickChest with the full code from Task 8 Step 1 (the factory needs it to compile) — carried in the Task 6 dispatch — cost if wrong: none, same code lands either way.
Ruling: Jeff did not answer "spread across chests or one chest" for the thief; building the spec as written (spread, scene at the worst-hit farm chest) — spec is binding — cost if wrong: SpoilagePass.Plan and PickChest rework, about one task.
Ruling: no separate git worktree; work in the main checkout on `story` — deploy tooling and the SMAPI Mods deploy are path-bound — cost if wrong: none (story is the feature branch, pushed after every commit).

## Progress
Task 1: minor (deferred): SeasonTurn.AllLineKeys could carry a one-line comment that the Junimo index is deliberately discarded.
Task 1: complete (commits c1313a1..2d20d3b, review clean; trailer and push verified by controller)
Tasks 2-4: dispatched as one batch (BASE 2d20d3b), one commit per task, one review
Tasks 2-4: minor (deferred): StrikeGuarantee and StrikeScenes have no callers until Task 5/6; final review to confirm they are wired.
Task 2: complete (commit c359394, review clean)
Task 3: complete (commit 401c355, review clean)
Task 4: complete (commit 2760ab6, review clean)
Task 5: dispatched (BASE 2760ab6), model opus
Jeff ruled 2026-09-21 (mid Task 5): chest blight hits ONE chest per strike. Supersedes my earlier "spread" ruling. Sent to the Task 5 implementer as a requirement change; spec and plan amended; Task 8 brief regenerated; Task 6 dispatch must use PickChest = strike.TargetChest on a farm map.
Jeff refined 2026-09-21: one chest per night, but machines stay multi-target as before; scene staged at PendingStrike.SceneTarget (chest, else one machine). Second message sent to Task 5 implementer; spec/plan amended; Task 6 factory must use SceneTarget on a farm map.
Task 5: review 1: spec X (Step 7 finding not written to HEADLESS_DRIVING.md/plan), Important: strike recorded before the effect can fail. Minors: semicolons in new text, "no scene due" reason string, unguarded SceneCanPlay, Pending overwrite, loose assertion, no rng-count test, file size.
Ruling: a strike whose effect fails at apply time is un-owed, not rolled back — PendingStrike.Apply reports success; on failure remove the event from Run.StruckEvents (the guarantee still owes it) and log Info; the week's chance drop and cap slot stay spent — a full rollback of NightRoll state is fiddly and a wasted night is harmless, while silently satisfying Jeff's every-loop guarantee with a no-op is not — cost if wrong: one quiet night a player never notices.
Task 5: minor (deferred): SabotageService.cs ~700 lines; split point before Task 8 (final review to weigh).
Task 5: fix round 1/5 (8 addressed, 0 open; commits fced37f..d9e4a19)
Task 5: complete (commits 2760ab6..d9e4a19, review clean after round 1)
Task 6: dispatched (BASE d9e4a19), model opus
Task 6: review 1: Important: postfix takes a null slot, which eats pickPersonalFarmEvent (births, spouse question) and farmEventOverride. Minors: not-staged ending skips onFinished entirely (Task 12 witness hook hangs there), Status() mutates state, stub duplication (temporary), no tests.
Task 6: minor (deferred): the four stub scene bodies are duplicates until Tasks 7-10 replace them; final review to confirm none survive.
Task 6: minor (deferred): multiplayer: a mod FarmEvent type in the NetRef<FarmEvent> slot is untested; darkness is host-only single-player by HostCanAct.
Task 6: fix round 1/5 (3 addressed, 0 open; commits b55eecf..4525036)
Task 6: minor (deferred): StrikeScenePatch comment says the personal-event probe "mutates nothing"; canGetPregnant writes defaultMap (idempotent, same as vanilla). Fix the comment.
Task 6: minor (deferred): QiPlaneEvent keeps the slot (conservative); fine unless Jeff wants otherwise.
Task 6: complete (commits d9e4a19..4525036, review clean after round 1)
Task 7: dispatched (BASE 4525036), model opus
Task 7: review 1: Critical: SceneActor.Animate assumes a 4-wide sheet so Linus never turns. Important: eye dots read as red squares / land on the body in peck frames; actors drawn unlit over a night frame; MoveEverything unguarded and ScenePreview.Stop skips End/Cleanup; semicolons in new text. Spec: shake is 6px vs brief 2px. Unverified: all screenshots are the mid-day preview, none of the real overnight path.
Task 7: minor (deferred): fadeClear state not restored; HoldNight forces timeOfDay in preview while the clock runs; lift-off bunching; night reads as dusk (SceneCamera.NightAmbient, Jeff's call).
Task 7: fix round 1/5 (7 addressed, 1 withdrawn (F1: the implementer was right, stride 4 is the engine's walk layout), 0 open; commits bbaf305..1f4e440)
Task 7: minor (deferred): `tly_sabotage fixture [rows]` clears up to 200 tiles on whatever save is loaded, no guard; chest tile falls inside the planted block at rows>=3. Debug only. Final review: add a throwaway-save guard or drop the arg.
Task 7: minor (deferred): CrowsScene._eyeGlow static Texture2D never disposed / not rebuilt after device reset; retry-every-frame on failed build.
Task 7: minor (deferred): unseeded Random for lift-off drift (visual only).
Task 7: complete (commits 4525036..1f4e440, review clean after round 1)
Task 8: dispatched (BASE 1f4e440), model opus
Task 8: review 1: Important: SceneSleepers does not restore NPC.controller / Halt flags (preview path can strand a spouse or child); fade + world-pump block duplicated verbatim across Crows and Thief (belongs in the base); child-in-bed beat claimed, not shown. Minors: target tile not blocked in ScenePath; SceneChestLid holder not cleared on return-to-title; blinkTimer not recorded; grid rebuilt 3x; fixture arg parsing; misleading exception; sentinel && vs ||; report accuracy on two screenshots.
Task 8: minor (deferred): Farm night frames read deep red/maroon rather than blue-black (farm-2, farm-3): for Jeff to judge when he watches.
Task 8: untested live: machine target, warded-chest pick, Cellar. Task 13 or Jeff's pass.
Task 8: fix round 1 implementer hit two server errors (529, 500) after editing but before commit. Uncommitted diff saved to task-8-fix1-uncommitted.diff. Game left running. Handing completion (verify, live check, report, commit) to a fresh agent.
Task 8: fix round 1/5 (9 addressed, 0 open; commits b391714..3f55cea; finished by a fresh agent after two server errors)
Task 8: minor (deferred): SceneSleepers.Restore writes back a captured PathFindController; on the mid-day preview a spouse re-routed during the scene would get the stale one. Inherent to record-and-restore; overnight path unaffected.
Task 8: complete (commits 1f4e440..3f55cea, review clean after round 1)
Task 9: dispatched (BASE 3f55cea), model opus
Task 9: review 1: zoom question closed SAFE from the decompile (render-to-buffer at zoom != 1). Important: the 5-tile Trim cancels the Saloon-side walk-in (Shane comes straight up the door column); PrepareRevert duplicates Revert's fairness setup. Minors: FrontWindows measured off the abandoned CC art with no guard for a restored CC (preview only); 1-texel sliver; HallScene near 400 lines; hall-3 description overstates the jump.
Task 9: fix round 1 implementer died mid-round (session ended). Its uncommitted edits (HallFacade split + IsRestored guard, Saloon-side walk no longer trimmed) built clean and were committed by the next controller as a6ec9f1 (2026-09-23), unreviewed, no fix report. Fresh implementer dispatched to verify/finish round 1 (BASE c73a1fc, FIX_BASE for re-review c73a1fc).
Ruling: the unrelated branch feat/tly1-story-cutscenes (June FarmEvent cutscenes, 23 commits) was NOT merged into story on Jeff's "merge everything" — story replaced that system (rewind sequence, strike scenes) and the merge conflicts by deleting files story still uses — cost if wrong: Jeff wants something from it and a cherry-pick is needed.
Jeff 2026-09-23 (hall): shadows smaller and man-shaped, window light dimmer, Shane walks PAST the hall on real town paths to clear his head then heads home (never to the door). Sent to the Task 9 fix-round implementer as a requirement change; it amends spec + plan.
Task 9: fix round 1/5 (8 addressed, 1 open: Jeff's walk-past route, Shane still climbs the CC cobble walk to the steps and crosses lawn off-path; commits ff73915..74bd6ec, incl. a6ec9f1)
Task 9: minor (deferred unless fixed in round 2): fallback way-out is a straight column with no passability check.
Ruling: for the walk-past, the camera may move/zoom out and the scene may run longer than 7 s (cap 10 s) so Shane walks a real through path in front of the hall at a natural pace, with a real hold on the windows after he leaves — Jeff's "walking past to clear his head" outranks the brief's 7 s timeline — cost if wrong: a scene a few seconds longer than planned, one constant to change back.
Task 9: fix round 2/5 dispatched 2026-10-07 to a fresh implementer (opus; round-1 agent gone), BASE 5194e4a, FIX_BASE 74bd6ec, findings task-9-fix2-findings.md. Jeff approved driving the game for this round's live check.
Jeff 2026-10-07 (hall, from the fix1 frames): man-shaped silhouettes are "about as intimidating as a mens room silhouette"; only ONE silhouette; make it an actual dark enemy from the mines, completely blacked out; keep the firelight ("really good"). Sent to the fix-round-2 implementer mid-round.
Ruling: the mines enemy is the Shadow Brute sprite, Shadow Shaman as fallback if the Brute reads badly at window scale. Why: "dark enemies from the mines" are the Shadow family and the Brute is the man-sized, most menacing one. Cost if wrong: one sprite name to change.
Jeff 2026-10-07 (hall): the Brute is not standing in the window looking out at Shane; he is working on something. Sent: side-on or turned away, busy hands, small working motion, still one solid-black figure.
Task 9: fix round 2/5 (6 addressed incl. Jeff's 3 window changes, 1 open: on very tall viewports (tile 42 in frame) SceneRoute.OutOfFrame returns the whole route and SceneWalk.At extrapolates Shane south past (49,42) over unverified (49,43); commits 5194e4a..b67870f)
Task 9: minor (deferred): HallScene.cs and SceneWindowGlow.cs class summaries have unwrapped very long lines.
Task 9: minor (deferred): camera tilt crops roof and clock; zoom-out allowed, offered to Jeff.
Task 9: fix round 3/5 (1 addressed, 0 open; Shane's way home clamped to checked tiles via TileRoute.At, also fixed the hair sliver at 1080p; commits b67870f..5ecb1ba)
Task 9: minor (deferred): HallWalker private OutOfShotTiles duplicates HallRoute.OutOfShotTiles; new Shane tests cover WayOut only, not a detoured route.
Task 9: complete (commits c73a1fc..5ecb1ba, review clean after round 3). Open for Jeff: smaller Brute? zoom out to keep roof/clock?
Task 10: dispatched (BASE 5ecb1ba), model opus. Jeff approved game driving for tasks 10-13 (2026-10-07).
Jeff 2026-10-07 (cloud, mid Task 10): farm concentration a bit too much, everywhere else not enough; effect good; drift in slowly, not dart. Sent to the Task 10 implementer.
Ruling: rest split 75% scattered / 25% farm (was 60/40) with more non-farm coverage, farm still darkest; blobs staggered over ~5000 ms, each travelling ~3000-4500 ms with ease-out, farm settles ~9000, strike/sound/full dim at settle, fade ~2000 later, scene lengthened to fit. Why: Jeff's words, numbers mine. Cost if wrong: constants to retune.
Jeff 2026-10-07 (hall): keeps the big Brute; framing fine as is. Both open hall questions closed, no change.
Jeff 2026-10-07 (cloud): not just over places that show houses; the cloud sort of everywhere across the whole map. Sent: even map-wide distribution (jittered grid / blue noise over the full map rect), farm a bit thicker and darkest.
Jeff 2026-10-07 (cloud): fullscreen showed a small map ("if that's the best you can do fine"). Ruling: drop "same fit as the map tab"; scale the map to the largest fit in the viewport, blobs and farm rect scaled with it. Why: we can do better than MapPage's fit and he'd prefer it bigger. Cost if wrong: one scale rule to revert.
Jeff 2026-10-07: tamper-1 too abrupt, needs setup. His line, verbatim: "@, last night the darkness struck! It has tainted all the {{old}}." Sent to the Task 10 implementer (default.json only; tamper-2/3 unchanged).
Jeff 2026-10-07 (tamper morning): rejected the wake-up text box followed by a blink to the doorstep. Map scene plays overnight; the Junimo tainted scene plays when the farmer first leaves the farmhouse onto the Farm, from the door, staged behind black, once. Sent to the Task 10 implementer.
Jeff 2026-10-07: asked why "Some of your things have gone missing overnight" showed. Cause: the test chain, tly_sabotage scene cloud applies a tamper on the preview, then debug sleep let the real night roll fire ChestBlight (thief took 1 item). Not a real-play bug; offered to suppress the night roll in the preview.
Jeff 2026-10-07: plurals must be correct; the dark fade on the Junimos' middle line is not good. Sent: {{old}} plural via the game's pluralizer with overrides, {{new}} plural when count > 1, new key tamper-3-plural "Bring us {{new}} instead. They remain pure."; remove the mid-line darkening.
Ruling: tamper-3-plural wording "They remain pure." is the minimal agreement fix to Jeff's line. Cost if wrong: one string.
Task 10: implementer DONE_WITH_CONCERNS, commits 5ecb1ba..81ee071 (+37108ac report). Push of 37108ac rejected by GitHub (500) x4, retry pending. Review dispatched (opus) with task-10-changes.md.
Task 10: review 1: spec ✅ except change 6 PARTIAL. Important: TamperPorchRule fires on ANY Farm entry (Forest/Backwoods/BusStop edges, warp totem), so Junimo marks placed relative to an arbitrary tile can land off-map or on cliffs/water.
Ruling: the scene starts only on a Farm entry whose OldLocation is the FarmHouse (Cellar excluded; the cellar exits into the house). Why: Jeff said "step out of the farmhouse". Cost if wrong: a player who leaves by totem waits for his next door exit.
Task 10: minor (deferred): ShowMorning(continueWith) now always returns false, continueWith unused; RunController.cs:982-984 comment stale.
Task 10: minor (deferred): a carried-over tamper report also holds back the next night's other strike HUD lines until the Farm entry.
Task 10: minor (deferred): blob centres truncated to whole map pixels before scaling; 2-3 px drift steps at 11x.
Task 10: minor (deferred): WorldMapManager.ReloadData() on every Stage; drop or comment why.
Task 10: minor (deferred): "Bring us 3 Beer instead. They remain pure." asked of Jeff.
Jeff 2026-10-07 (cloud): all clouds come in from the north, not start on the map and spread outward. Sent with fix round 1: every blob starts above the map's top edge, spread across the width, drifts south to rest; tests assert starts outside the map rect.
Jeff 2026-10-07 (cloud): blobs blew in transparent and darkened halfway down. Sent: full alpha by the time a blob crosses the map top edge; darkening comes from overlap + global dim, not per-blob fade on the map.
Task 10: fix round 1 implemented (porch rule farmhouse-door only; north entry; full alpha at the edge; clipped to map), commits 81ee071..b969bb8, pushed. Re-review dispatched.
Task 10: fix round 1/5 (3 addressed, 0 open; commits 81ee071..b969bb8)
Task 10: minor (deferred): alpha steps 0 -> 0.7 at StartMs, hidden only by the off-map mask; leftover north720 scratch frames in test-output.
Task 10: complete (commits 5ecb1ba..b969bb8, review clean after round 1). Pending Jeff: framed map at native size vs filled screen; heavier blobs?
Jeff 2026-10-07 (cloud): blown-up map looks bad; asked about extrapolating art (not feasible, explained). Approved a trial: map at map-tab scale inside the map-tab frame, dark backdrop, cloud over the whole screen from above the screen top, farm darkest; keep the filled path switchable. Dispatched as Task 10 follow-up to the same implementer (HEAD b969bb8).
Task 10 follow-up (framed map): built b969bb8..eadf9dc, pushed. Backdrop Color(14,16,32); farm blobs fixed at 10 (deviation from strict 75/25 on large screens, else farm went solid black at 4K); CloudScene.FillScreen switch keeps the old path. Frames sent to Jeff; awaiting his pick. Not yet reviewed.
Jeff 2026-10-07: framed version approved ("that's good").
Task 10 follow-up: review dispatched (sonnet), b969bb8..eadf9dc. Task 11: dispatched (BASE eadf9dc), model sonnet (plan text carries the code).
Task 10 follow-up: review clean (Approved; farm-fixed-at-10 deviation judged acceptable). Commits b969bb8..eadf9dc.
Task 10: minor (deferred): frame margin/piece use raw uiToPaint while the map uses rounded perArt (off at non-integer UI ratios); fit computed once in Stage (resize mid-scene); FarmShare doc line overlong; FillScreen is a compile-time switch; draw truncation.
Task 11: implementer DONE_WITH_CONCERNS eadf9dc..5151906 (shadowTexture tint showed no purple; replaced with a generated white disc tinted (120,20,180); held glow tints top of hair; debug tly_ringtest keep and tly_sabotage aurachest added). Review dispatched.
Task 11: review 1: spec MET (texture swap judged correct). Important: ColoredObject.drawInMenu overrides without calling base (and drawWhenHeld only calls base in one branch), so coloured flowers, roe, aged roe, dyed/flavoured items get no aura; extending the held patch must not double-draw.
Task 11: minor (deferred): HeldDepth uses 3 where vanilla uses 4, uncommented; _glow field declared after first use; cache keyed on count misses an in-place id edit (tampers only append/clear); held aura tints top of hair (Jeff told).
Task 11: fix round 1 implemented 5151906..0bba578 (ColoredObject patched, Object held prefix skips ColoredObject; taint keyed on base id so every flavour glows). Re-review dispatched.
Task 11: fix round 1/5 (2 addressed, 0 open; commits 5151906..0bba578)
Task 11: minor (deferred): tly_additem query has no null check (debug); a tampered flavoured slot may leave its old WrittenBoardFlavors entry, effect on FlavoredSlotPatch untraced (final review to check); aura keys on base id so every flavour glows (Dried Apple slot -> all Dried Fruit), consistent with the tamper record.
Task 11: complete (commits eadf9dc..0bba578, review clean after round 1)
Task 12: dispatched (BASE 0bba578), model sonnet. Ruling: witness recorded when a scene played or was skipped, not when the strike landed with no scene (nobody saw anything). Cost if wrong: a missing line on scene-less strikes.
Jeff 2026-10-07 (tamper): "it can't taint all the potatoes but still require them for 2 other bundles"; tainting Dried Apples must not make the Dried Cucumbers another bundle needs look tainted. Code check: WriteTamper rewrites ONE slot; taint keyed on base id. Queued as follow-up "tamper-all" after Task 12 (shares ModEntry/RunState): every open slot asking for the exact item (flavour included) is rewritten, each with its own fair pick; filled slots stay; TamperRecord carries the flavour; aura and the scene's {{old}} name use the exact flavoured item. Jeff approved ("do that").
Controller note: I let the base-id taint pass in Task 11's re-review on "consistent with the record" without weighing what the player reads. Wrong call; corrected here.
Jeff 2026-10-07: simplify: the tamper may only pick an item that appears in one bundle. Ruling: exact item (id + flavour) in exactly one slot on the whole board, filled or open; still one slot rewritten; exact-item taint, flavour cleared on rewrite, flavoured plural name kept. Cost if wrong: if he meant 'one bundle' allowing two slots in the same bundle, a constant.
Task 12: implementer DONE 0bba578..8fb3992 (scene day read from the run calendar; witness recorded when onFinished(shown); debug tly_witness). Review dispatched. Tamper follow-up dispatched (BASE 8fb3992, opus).
Task 12: review 1: spec MET, Approved.
Task 12: minor (deferred): CmdWitness inserted between CmdEventStep's doc comment and its method; no duplicate-push guard in OnDayStarted; purge duplicates the window rule outside Core and reads Game1 date not the run calendar; no host guard (harmless, differs from darkness code); NPC-not-found skips silently; gift/proposal flows clear the stack (line re-pushed next morning).
Task 12: complete (commits 0bba578..8fb3992, review clean)
Tamper follow-up: implementer DONE 8fb3992..c14b656. No-target uses the existing path. Unflavoured slot (accepts any flavour) taints every flavour. Open for Jeff: replacement may already be asked elsewhere (Beer). Review dispatched.
Tamper follow-up: review 1: spec MET; Important: a later strike's replacement can be a previously tainted item (all flavours; replacements are written unflavoured), breaking "nothing on the board still wants the tainted thing". Fix round 1: exclude every Run.Tampers OldItemId (all flavours) from the replacement pool, with a test.
Tamper follow-up: minor (deferred): report wrongly says Vault/Joja counted (they are not in requirements; nil effect); category slots not seen by SlotsAsking (generated boards have none; vanilla mode unverified); ItemPlurals last-word mass-noun change alters asks ("3 Wild Honey", "3 Sea Jelly") - tell Jeff; TargetLine duplicate spellings (debug); unrealistic Blueberry Jelly flavored=true test.
Tamper follow-up: fix round 1/5 (1 addressed, 0 open; commits c14b656..e313d5c)
Tamper follow-up: minor (deferred): 2026-09-15 spec Tampering paragraph has a spliced sentence with a duplicated subject.
Tamper follow-up: complete (commits 8fb3992..e313d5c, review clean after round 1). Open for Jeff: replacement may be an item another untainted slot asks for; "3 Wild Honey" style asks.
Task 13: dispatched (BASE e313d5c), model opus. Rulings: Step 5 is prep only (farms saved, no launch for Jeff without asking); Step 2/3 failures are reported, not patched in this task; Jeff's open questions recorded in TODO, not decided.
Jeff 2026-10-07: replacements skip anything on the board ("it's something new"); counted asks read "jars of wild honey", "sea jellies", "bottles of blueberry wine". Queued as follow-up "asks" after Task 13 (shares SabotageService). Brief: asks-brief.md.
Ruling: container words for liquids/spreads (bottles, jars, cups, tins), proper plurals for countables, bare form for bulk materials; count 1 unchanged; "all the X" unchanged. Final table goes to Jeff. Cost if wrong: table entries.
Jeff 2026-10-07: "mugs of beer" (Beer and Pale Ale take mugs).
Jeff 2026-10-07: "glasses of Pale Ale" (pilsner-glass sprite). Containers follow the sprite.
Jeff 2026-10-07: "cups of coffee" confirmed.
Jeff 2026-10-07: confirmed tins of Caviar, clusters of Roe, jars of Aged Roe.
Jeff 2026-10-07: milk (Milk, Large Milk, Goat Milk, L. Goat Milk) takes bottles, confirmed.
Jeff 2026-10-07: syrup and oil take bottles, confirmed.
Task 13: implementer DONE e313d5c..3e23b10 (collision + guarantee passed live; Jeff's farms standard_451080087 Summer 14, standard_451080418 Fall 14, standard_451080615 Winter 1). Review dispatched. Asks follow-up dispatched (BASE 3e23b10).
Task 13: review 1: Approved, spec MET.
Task 13: minor (deferred): STATUS.md:44 sentence ends "Details in" with no reference; STATUS.md says last public release 0.19.0 (should be 0.19.1); vague commit range "c1313a1.."; Witness lines pending lists expired-but-unpruned records (cosmetic). Also asks follow-up will make the TODO's two designer questions answered (update TODO in final fix wave).
Task 13: complete (commits e313d5c..3e23b10, review clean)
Asks follow-up: implementer DONE 3e23b10..6908dda. Review dispatched; container table sent to Jeff.
Asks follow-up: review 1: Approved.
Asks follow-up: minor (deferred): no test of the SabotageService ask wiring; "(O)DriedMushroom" singular id not in AskNameKeys/table; more tamper-skip nights unmeasured; SabotageService.cs well over 400 lines (split point for final review).
Asks follow-up: complete (commits 3e23b10..6908dda, review clean). Container table with Jeff.
Final review dispatched (opus) over c1313a1..6908dda, package final-review.diff restricted to the plan's files.
Final review: 5 to fix (I1 strike scene eats the Wildcard night_event twist; I2 thief can pick a Mini-Shipping Bin emptied before the strike; stale TODO designer questions; M6 comment; M7 stale comment + dead param) plus minors.
Ruling: I1: on a Wildcard night_event night the twist keeps the overnight slot; the strike lands without its scene (ApplyNow) and the scene stays due. Why: the player was told "Something will happen on the farm tonight" that morning. Cost if wrong: Jeff may prefer the strike scene to win; a one-line swap.
Ruling: I2: skip MiniShippingBin and JunimoChest in the thief/spoilage chest pool. Why: shipped/emptied before apply, and Junimo chests share one inventory. Cost if wrong: Junimo chests safe from blight.
Ruling: M1 + carried reports: remove only the tamper report that was shown; non-tamper strike reports show on waking as before even when a tamper report is carried. Why: Jeff said other strikes' mornings stay unchanged. Cost if wrong: HUD order.
Ruling: include cheap minors in the one fix wave (M2 log/flag, M3 activation guard + try/catch + device-aware glow, M4 logs, M5 indexed for, STATUS.md release line and range) and split SabotageService.cs into partial files as a separate no-behaviour commit. Why: workspace 400-line rule; reviewer gave split points. Cost if wrong: churn in one file.
Final-review fix wave: DONE c9bb44b..7b24951 (code c9bb44b..b549fe1, docs 7b24951), 4007 tests pass, pushed.
Final fix wave: DONE 6908dda..7b24951, 4007 tests. Scoped re-review dispatched.
Final fix wave: re-review clean (all 11 addressed). Plan complete 2026-10-07. Workspace kept: task reports are git-tracked (force-added), deleting would dirty the tree.
Jeff 2026-10-07 corrections to my rulings:
- Junimo chests ARE thief targets; the item leaves the shared inventory (gone from all of them). Revert the JunimoChest exclusion; keep MiniShippingBin excluded.
- "we don't delay scenes without delaying the effect of them": on any night where the overnight slot belongs to something else (Wildcard night_event, WorldChangeEvent like the bus repair, wedding, birth, farmEventOverride...), the strike does NOT land; effect and scene both wait for a later night. Supersedes the Task 6/13 ApplyNow-without-scene behaviour.
- Two tampers waiting: only one fires per day with forced days between; I explained it needs days without visiting the farm.
- Farm entry: ANY arrival on the Farm map shows the tamper scene (like vanilla's CC cutscene on entering Town): stage at the porch spot behind black, play, then return the farmer to the tile where he entered.
- Single-slot rule: per BUNDLE. An item may appear twice inside one bundle (Construction's double Wood) and still be a target, if no other bundle asks it.
- Container table approved as is.
Ruling: a target with two slots in its one bundle: every open slot of that exact item in that bundle is rewritten to the same new item with the same stack (filled ones stay); the Junimo line names that item and the per-slot count. Why: "tainted all the wood" cannot leave a wood slot. Cost if wrong: stack/line detail.
Ruling: a postponed strike keeps the guarantee owed and the night roll un-spent (as if no strike that night). Cost if wrong: strike frequency.
Corrections (Jeff 2026-10-07): implementer DONE bf0c33e..dec5733 + report, 4032 tests, pushed. Report: corrections-report.md.
Corrections: DONE 7b24951..856a811 (4032 tests). Implementer kept 'scene takes slot but cannot stage -> lands without scene', which contradicts Jeff's rule; flagged to the reviewer. Handoff farms still valid.
Corrections review: Changes requested. C1 a scene that takes the slot but cannot stage (or setUp throws) lands the strike bare: violates Jeff's rule, fix = commit only on successful staging, otherwise postpone. C2 ChestBlight on a chest off the scene maps (Greenhouse, Coop, Barn, Island) lands bare while the thief scene is due: asked Jeff. I1 a failed apply after a postponement drops the guaranteed Winter tamper. M1 stale StrikeScenes comment; M2 SabotageRules.cs 490 and Night.cs 403 lines; M3 one Circle of Warding wards the whole Junimo network (asked Jeff); M4 closed by C1; M5 thin service-path tests.
Fix round 1 dispatched for C1, I1, M1, M2, M5 (C2 and M3 wait for Jeff).
Corrections fix round 1: DONE 856a811..1d37eb8 (4037 tests). Re-review dispatched.
