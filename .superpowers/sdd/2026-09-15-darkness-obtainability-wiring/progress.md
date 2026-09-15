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
