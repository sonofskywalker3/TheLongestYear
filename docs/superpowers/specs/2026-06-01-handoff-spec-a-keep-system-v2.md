# Handoff — 2026-06-01 — Spec A (Keep System v2) shipped; awaiting reset playtest

Picks up after Spec A (in-run reach gating + permanent-floor keeps) was implemented,
reviewed, deployed, and iterated on from live feedback. **SMAPI IS RUNNING** (relaunched
after the last deploy) — do NOT relaunch on startup; the user is mid-playtest and about to
(or just did) trigger a loop **reset** to verify the new behaviour.

## Branch state
- Branch: `feat/v1-plan-07-junimo-stash`
- Tip: `52065f8` — working tree clean, all work committed (local only; **never push**).
- 399 tests passing, 0 warnings. Deployed DLL = the tip (Steam `Mods/TheLongestYear/`).
- Spec: `docs/superpowers/specs/2026-06-01-keep-system-v2-design.md`
- Plan: `docs/superpowers/plans/2026-06-01-keep-system-v2.md`

## What shipped this session

### Spec B art/UX fixes (earlier in the session)
- **Books** = closed-cover sprites (spine + title plate + page sliver). The real bug was a
  texture-load failure: `Data/Furniture` is split on `/`, so the `Mods/.../Books` asset path
  was truncated to "Mods" — fixed by writing the texture path with **backslashes**.
- **Book inventory icons** drawn full-slot (vanilla caps 1x1 furniture at half size) —
  `BookFurniture.DrawInMenuPatch`.
- **Stash chest** = the real Junimo Chest sprite recolored **purple-and-black** (dark recesses
  kept black), animated via `currentLidFrame` (5 composited frames, `StashDrawPatch`). The
  chest is a plain BC 130 underneath; `JunimoStashService` loads the texture via a loader hook.
- **Planning shrine furniture** = the real Stone Junimo recolored **green body + gold star**.
  Sprites come from `tools/extract_sprites.py` (an XNB/MonoGame-LZ4 decoder) + `tools/gen-sprites.py`
  (books only). Shrine + chest are both **fixed in place** (`canBeRemoved` → false).

### Spec A — Keep System v2 (the focus)
- **Reach gating**: the shrine only offers a keep for a tier/level/item the player actually
  reached **this run** (lowest un-owned tier per chain). `RunReachRequirement` (Core parse +
  threshold) + `KeepShopFilter` (pure predicate, with the watering-can worked example as a
  test) + `RunReachEvaluator` (glue, live `Game1` reads). Both the purchase shrine
  (`JunimoShrineMenu`) and the read-only preview share the filter.
- **Reach metrics**: tools (`UpgradeLevel`), fishing rod (incl. new **Keep Bamboo Pole** root;
  rod UpgradeLevel 0=bamboo/2=fiberglass/3=iridium — bamboo is 0, NOT 1), backpack
  (`MaxItems`), skills, mine (`deepestMineLevel`), mastery (`getCurrentMasteryLevel`), golden
  scythe (mail `gotGoldenScythe`), **buildings** (coop/barn chains via farm buildings),
  **house** (`HouseUpgradeLevel`: kitchen=1/basement=3), **pet** (`hasPet`), **bus**
  (`RunState.VaultBundlesPaid`, threaded via `RunReachEvaluator.AttachRunState`), **shortcuts**
  (mail `communityUpgradeShortcuts`).
- **New keeps**: Keep Bamboo Pole, Keep Mastery 1–5, Keep Golden Scythe.
- **Permanent-floor keeps**: the old per-run "cap at what you reached" in `RunBaselineBuilder`
  was **removed** — reach-gating the purchase already guarantees you earned it, so a keep now
  always restores its full tier. `FarmerReset` grants the Golden Scythe instead of the basic
  scythe when kept, and restores mastery (`Game1.stats.Set("MasteryExp", …)`).
- **Planning shrine reworked**: scrollable buyable-only list (no owned-tier clutter — "Cookbook
  II" implies you own I), per-row cost, hover tooltip with effect + "Currently owned: …", title
  inside the box, no "…more" truncation.
- **Descriptions** standardized on **loop** terminology ("Start each loop with …", "Bank up to N
  … between loops"); dropped redundant "(Permanent…)" parentheticals and "instead of …" back-refs.

## STILL TO VERIFY (the reason for this handoff — ask the user first)

The user was about to test a **loop reset**. Confirm in-game (pull the log after they report):
- **Permanent floors**: after a reset, kept tools/skills/rod/mine/mastery restore in FULL
  regardless of what was reached in the run that just ended (no more per-run cap).
- **Golden Scythe**: with the keep owned, a reset grants `(W)53` and NO basic scythe.
- **Mastery**: with a Keep Mastery N owned, the reset reports mastery level N.
- **Reach gating in the shop/preview**: only earned keeps show — buildings (coop/barn) gone
  until built, kitchen/basement gated on house level, pet gated on having a pet, bus gated on
  paying the vault this run, shortcuts gated on `communityUpgradeShortcuts`.
- **Planning shrine**: scroll works, tooltips read right ("Currently owned: …"), costs shown.
- **Chest** purple-and-black + animates; **books** closed; **shrine** green junimo + gold star.

The `FarmerReset` Trace log line now includes `mastery=…, goldenScythe=…` — use it to confirm
the grants fired. SMAPI log: `%APPDATA%\StardewValley\ErrorLogs\SMAPI-latest.txt`.

## Known follow-ups / deferred
- **Vestigial params**: `RunBaselineBuilder.Build(meta, run, peaks, …)` no longer reads `run`
  or `peaks` (cap removed). `WorldResetService.CapturePeaks` + `PlayerSnapshot` are now dead
  weight. Optional cleanup: drop them (ripples to the builder signature + several tests).
- **Final-review I1 skipped**: `RunReachEvaluator.Meets(string)` (not `string?`) — intentional,
  the mod project compiles `<Nullable>disable</Nullable>` and `KeepShopFilter` never passes null.
- **Consistency pass** done for the obvious cases; if the user flags more wording, fold into one pass.
- **From the prior handoff, still unconfirmed**: the day-1 intro chain end-to-end, and the
  5-basic-tools-on-reset fix (should be fine now but never explicitly re-confirmed).

## Workflow / rules
- Local commits only; **never push**. Co-Authored-By footer on every commit.
- SMAPI running → compile-only: `dotnet build src\TheLongestYear\TheLongestYear.csproj -c Release -p:EnableModDeploy=false -p:EnableModZip=false`.
  To deploy: kill SMAPI (`Get-Process StardewModdingAPI | Stop-Process -Force`), build without those flags, relaunch
  `StardewModdingAPI.exe` from the Steam Stardew dir.
- Tests: `dotnet test TheLongestYear.sln -c Release` (test project references ONLY `TheLongestYear.Core`).
- Decompiled Android source: `C:\Users\Jeff\Documents\Projects\decompiler\stardew-valley-android` (PC differs — verify).
- User is not a coder: deploy + pull logs yourself; reserve playtests for meaningful feedback.
