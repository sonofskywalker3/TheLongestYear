# SDD ledger - plan: docs/superpowers/plans/2026-09-15-darkness-obtainability-wiring.md
Spec: docs/superpowers/specs/2026-09-15-darkness-obtainability-wiring-design.md (read; binding authority). Branch story, base commit 9f169b4 (plan fixed), 2285 tests at start.
Workspace is NOT git-ignored in this repo (the phase 2 workspace was committed too); the ledger and briefs are committed with the tasks, so no em dashes here either.

## Pre-flight scan (2026-09-15)
| Pair / task | Produces vs consumes | Found |
|---|---|---|
| T1 / T8 | DifficultySettings.Darkness (nullable), DarknessOrLowest, MigrateDarkness | agree |
| T1 internal | test "old config migrates" asserts Darkness null, but the plan initialised the property to Normal, so JSON without the key could never read null | DEFECT, fixed in the plan (9f169b4): no initializer on Darkness and DarknessStep; fresh-config test rewritten. Ruling: the migration must be detectable, the spec's 2.3 depends on it. Cost if wrong: none, a fresh config still reads Normal through the accessor |
| T2 / T3 / T6 | StrikesTonight shim: T2 adds, T3 repoints at NightRoll.SeasonChance, T6 deletes | agree (churn accepted, keeps every task green) |
| T2 / T6 | BlightRule.Count(crops, season, level), SpoilCount(units, season, level); BlightPass.CountFor(season, level) | agree |
| T2 / T7 / T6 | SpoilagePass.StoredUnits(bool), Strike(int, Random, bool): T6 adds placeholder overloads, T7 replaces | ModEntry's tly_sabotage blight case calls the no-arg StoredUnits; T7 removes it. Ruling: T7's dispatch tells the implementer to update ModEntry's blight case to pass DarknessLevels.StorageReachesEverything(level). Cost if wrong: a build break caught by the task |
| T3 / T6 | NightRoll API (ChanceTonight, RecordStrike, Options, Pick, IsGuaranteedTamperNight, UnmoderatedFires), RunState fields, MetaState.FirstWinterTamperSeen, SabotageSchedule.Rng(seed, day) | agree |
| T3 internal | FixedRng overrides Random.Next(int)/NextDouble (virtual); even-split range 1150..1520 of 4000 over 3 options; BeginNewRun signature to be copied from the existing reset test | agree |
| T4 / T6 | SaveSnapshot ctor order (recipes, buildings, machines, craftable, animals, friendship, mail, floor, skills); FairnessRule.Counts/Judge/Explain/ReversionDeadline/TamperDeadline | agree |
| T4 internal | every expected value re-derived by hand: deadlines 84/112, mine gaps 4/12/1, skill gaps 7/2/10, building 110+3+1=114 out, friendship +14, machine +1; day-after-hit start; unnamed conditions met | agree |
| T5 / T6 | ReversionRule.Pick(ledger, requirements, Func<string,bool>, Random) | agree |
| T6 internal | nested NightPlan reaches the outer service's private members (legal C#); die rolls before TakeArmed so later nights are unchanged | agree |
| T7 internal | OverlaidDictionary.Pairs to be confirmed in the decompile (brief says so) | agree |
| T9 internal | BoardFiles list may name a file that does not exist under that name; brief says fix the list, never delete an entry | agree |
| Interpretation | a missing recipe rules a route out unless the unlock is a shop sale or a Queen of Sauce episode (the game prices those) | Ruling: needed so the spec's year 2 TV rule on Hard can ever fire; flagged to Jeff in the plan handoff. Cost if wrong: Normal/Hard slightly gentler on shop-taught dishes |

Model plan: implementers sonnet (T6 opus: a large glue rewrite), reviewers sonnet (T6 opus), final review opus. Task 0 is an automated launch on None_449077472 (the agent's, not Jeff's).

Task 0: baseline captured (414 lines) from the running game on None_449077472, build 9f169b4.
Task 0: dispatched (BASE 9f169b4, implementer sonnet; automated launch/reuse on None_449077472)
Task 0: DONE_WITH_CONCERNS 4613d36 (414-line baseline from the already-running game, no launch). Ruling: .superpowers/sdd/.gitignore is a blanket `*`, so the workspace IS git-ignored scratch after all (the header line above was wrong); the forced add of the baseline stands (already pushed, harmless, and Task 9 diffs against it), but no later task force-adds workspace files: Task 9 records the diff result in STATUS.md and commits only the guard test and docs. Cost if wrong: one scratch file in history. Ruling: the game's own log lines carry em dashes (BundleEngine/gatecheck format); a captured artifact keeps them byte for byte, the ban is on authored text. Cost if wrong: none.
Task 0: complete (commits 9f169b4..4613d36, review clean; the reviewer's two Important items are the two rulings recorded above)
Task 1: dispatched (BASE 4613d36, implementer sonnet)
Task 1: implementer DONE 05d20f6 (2295 passed); review dispatched
Task 1: complete (commits 4613d36..05d20f6, review clean; 2295 passed)
Task 2: dispatched (BASE 05d20f6, implementer sonnet)
Task 2: implementer DONE_WITH_CONCERNS a8f0fa6 (2312 passed). Ruling: the plan's verbatim `Math.Ceiling(have * share)` overshoots on binary fractions (100 * 0.07 = 7.000000000000001 gives 8, the spec says 7); the implementer's rounding to 6 decimals before the ceiling is accepted as the plan's correction. Cost if wrong: none, the spec's shares have two decimals. Review dispatched.
Task 2: minor (deferred): SabotageService.Tamper still computes maxCount inline without the legendary rule; Task 6 rewrites that method to call TamperRule.MaxCount (verify in its review)
Task 2: complete (commits 05d20f6..a8f0fa6, review clean; 2312 passed)
Task 3: dispatched (BASE a8f0fa6, implementer sonnet)
Task 3: implementer DONE 35a68f9 (2332 passed); review dispatched. Minor (deferred): NightRoll.NoWeek const unused (from the plan text)
Task 3: complete (commits a8f0fa6..35a68f9, review clean; 2332 passed)
Task 4: dispatched (BASE 35a68f9, implementer sonnet)
Task 4: implementer DONE 82d7ac5 (2370 passed); review dispatched (opus: the rule every fairness promise rests on)
Task 4: review: 2 Important (plan-mandated code wrong against the real model): friendship step split on the first space drops multi-word animal ids ("friendship:White Chicken 200"); the animal condition is read from Requires but the model only writes "animal:X (not sold)" there and puts the purchasable animal in Setup ("animal:X" 1 day). Ruling: price animal and friendship off source.Setup (building stays off Requires with its Setup days; sapling and tea bush steps are never added), split friendship at the LAST space, keep the Requires "animal:" branch for the not-sold rule and any fabricated shape without double-counting; tests rewritten to the model's real shapes plus a multi-word friendship case and one Hard row. Cost if wrong: animal routes priced twice or not at all; the fixed tests pin both. Minors deferred: skill:<Name> N in Requires is unhandled (all Chance sources today, comment it); added days are applied after the landing rather than shifting the start (plan-mandated approximation, comment it); Policy.For default arm; no Hard rows on the day-adding cells; RouteVerdict.LandingDay name; unlock:l N counts as met.
Task 4: fix round 1/5 (2 addressed per implementer, 0 open; commits 82d7ac5..2d34191, 2371 passed); re-review dispatched
Task 4: re-review clean (both addressed, no new breakage)
Task 4: complete (commits 35a68f9..2d34191, review clean after fix round 1; 2371 passed)
Task 5: dispatched (BASE 2d34191, implementer sonnet)
Pre-verified in the decompile for Tasks 6 to 8: GameLocation.getAllFarmAnimals() (Farm inherits it), FarmAnimal.type and friendshipTowardFarmer (NetString/NetInt, read .Value), Building.buildingType (NetString), Game1.buildingData (IDictionary<string, BuildingData>) with BuildingData.BuildingToUpgrade, Farmer.GetSkillLevel(int), Farmer.cookingRecipes/craftingRecipes (NetStringDictionary, .Keys), Farmer.mailReceived, CraftingRecipe(string name, bool isCookingRecipe).createItem(), OverlaidDictionary.Pairs (IEnumerable<KeyValuePair<Vector2, Object>>), MineShaft.lowestLevelReached (static, StardewValley.Locations). ModEntry.Entry always calls helper.WriteConfig(_config) after the migration block, so Task 8 needs no extra write.
Task 5: implementer DONE 77d6c72 (2372 passed); review dispatched
Task 5: complete (commits 2d34191..77d6c72, review clean; 2372 passed)
Task 6: dispatched (BASE 77d6c72, implementer opus)
Task 6: implementer DONE f56d981 (build 0 errors, 2372 passed). Ruling: the unmoderated flag is spent only when the unmoderated strike actually lands (Jeff: "only 1 per loop even if it picks an easy item"; a roll with nothing to act on is not a hit). Cost if wrong: a Hard loop could roll the unmoderated die more than once before landing it once. Minors deferred: no Warn at night when the model is null (Status shows it); Candidates judges the whole catalog per tamper night (memoise if slow). Review dispatched (opus).
Task 6: review: 1 Important (plan-mandated): unplaced catalog items got effort 0 in Candidates, which makes "five closest in effort" the alphabetically first unplaced items. Ruling: use ItemAvailabilityModel.UnrecognisedEffort (6, the model's own neutral default) for the unplaced case; the plan's ": 0" was wrong. Cost if wrong: none beyond ranking. Folded into the same fix round as two minors: guard Game1.netWorldState in Execute(Tampering) like the debug path; warn on an unparseable level in tly_sabotage fair. Minors deferred: Candidates judges the whole catalog per plan; BlightRule.OneTarget is now unreachable from the service (delete in the final wave if the final review agrees); SpoilagePass stubs until Task 7.
Task 6: fix round 1/5 (3 addressed per implementer, 0 open; commits f56d981..b8e7495, 2372 passed); re-review dispatched. Task 9 live check: watch tamper swaps for alphabetical runs or wild mispricing (effort default)
Task 6: re-review clean (3 addressed)
Task 6: complete (commits 77d6c72..b8e7495, review clean after fix round 1; 2372 passed)
Task 7: dispatched (BASE b8e7495, implementer sonnet)
Task 7: implementer DONE 6598daf (2376 passed); review dispatched
Task 7: complete (commits b8e7495..6598daf, review clean; 2376 passed). Minor (deferred): rings/boots/hats cost 1 unit on Extreme, untested; SpoilagePass has no unit tests (SMAPI types)
Task 8: dispatched (BASE 6598daf, implementer sonnet)
Task 8: implementer DONE afdcb72 (2376 passed); review dispatched
Task 8: complete (commits 6598daf..afdcb72, review clean; 2376 passed)
Task 9: dispatched (BASE afdcb72, implementer opus; automated launch on None_449077472, the agent's own)
Task 9: guard test written first and green on the first run (BoardPathsNeverReadTheModelTests, no forbidden word in any of the eleven board files; every listed file exists, so the list is unchanged from the brief).
Task 9: live check on my own automated launch (deploy.ps1 -Minimized, tly_loadsave None_449077472, Run 168 Summer day 15, waited for "Run 168 ready" plus 62 s). Captured: the Obtainability model line (1113 items, 7 passes, 45 unresolved), the whole tly_difficulty table including the darkness row (Normal -> Normal), tly_sabotage status, the three fair readouts and the Warn for the bad level "hrd", and tly_sabotage arm tamper (reports tampering not open in Summer). No exceptions in the log. Game left running minimized.
Task 9: byte-identical proof PASSED. baseline-genbundles.txt 414 lines, after-genbundles.txt 414 lines, git diff --no-index EMPTY. after-genbundles.txt stays uncommitted (the workspace is git-ignored; the result is recorded in STATUS.md instead).
Task 9: fair-readout sanity. Legend at Extreme does not count: the model has only two dependable Spring-rain sources and neither lands again from day 44, which Extreme cannot ignore (it ignores conditions, not landing tables). Goat Cheese at Normal does not count on "machine (BC)16 not owned and not craftable", the spec's own 1.2 rule; no Goat Cheese route names building:Barn, so the brief's expected barn days do not exist to add. Parsnip at Normal counts through the greenhouse route only (the save has ccPantry). Neither reads as a defect; both are reported to Jeff as expectation mismatches, not bugs.
Task 9: 2377 tests passing (2376 at afdcb72 plus the guard test), Release, 0 failures.
