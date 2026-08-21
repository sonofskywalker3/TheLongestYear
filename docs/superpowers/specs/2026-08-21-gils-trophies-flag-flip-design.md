# `EnableNonObjectDonations` mid-loop flip — design (2026-08-21)

## Problem

`EnableNonObjectDonations` (config.json only, read once at `Entry`) does two things today:

1. **Generation** — `BundleEngine` composes Gil's Trophies from the full 11-id trophy list
   (4 hats + Rusty Sword + 6 rings) when true, rings-only when false
   (`AuthoredBundleComposer.TrophyCandidates`).
2. **Donation UI** — `BundleDonationPatches.Enabled` gates the two Harmony patches that let a
   (W)/(H) item be highlighted/picked and get an ingredient icon in `JunimoNoteMenu`.

Flipping it **off between launches, mid-loop**, on a board already composed with weapon/hat
slots, therefore:

- leaves those slots **un-donatable** (patches off) — with 0–1 ring slots among the 4 shown the
  bundle (need 2) is uncompletable, and its Percentage ramp (derived `[0,1,1,2]`) blocks the
  Winter checkpoint / win; and
- makes `ResolveRequirements` regenerate the manifest with the NEW flag → `EngineManifestCheck.
  Matches` fails on the Gil's Trophies value → WARN "engine manifest mismatch" → legacy
  read-and-classify fallback for the rest of the loop.

This is the documented beta caveat (README / Nexus "Beta caveat: flipping … mid-loop can leave
an in-flight Gil's Trophies bundle un-donatable until the next reset").

## Options considered

| | Option | Verdict |
|---|---|---|
| A | Rewrite the live Gil's Trophies to the rings-only composition at load | Mutates live BundleData + slot state mid-loop; must re-map already-completed slots; most code, most risk |
| B | Auto-complete the (W)/(H) slots at load | Free bundle reward; still needs the manifest dual-match; feels like a cheat |
| **C** | **Flag governs the NEXT board only; an in-flight board keeps working** | No board mutation, no free reward; smallest change; matches the user's "make it donatable" ask |

**Chosen: C.**

## Design

1. **Core (pure, tested):** `BoardInspection.HasNonObjectIngredients(bundleData)` — true when
   any parsed bundle has a concrete (non-category) ingredient whose normalized id is not
   `(O)`-prefixed. Uses `BundleParsing` so it reads exactly what the game sees.
2. **Patches:** `BundleDonationPatches.LiveBoardHasNonObjectSlots` (static, set from the live
   `BundleData` right after requirements are resolved at save-load; cleared on return-to-title).
   `Enabled` becomes `RunActivation.IsActive && (config.EnableNonObjectDonations ||
   LiveBoardHasNonObjectSlots)`. Net effect: the patches stay live for the rest of a loop whose
   board needs them, and go dormant on the first board generated rings-only.
3. **`ResolveRequirements` (engine-manifest branch):** generate with the current flag; on
   mismatch, generate once more with the **opposite** flag. If that matches, use it (INFO log:
   board was composed with the other setting; honouring it this loop; the new setting applies
   from the next reset). Only when neither matches fall through to legacy as today.
4. **Docs:** delete the beta caveat from README + Nexus description; reword the
   `GameplayConfig.EnableNonObjectDonations` summary to "takes effect from the next reset".

Not changed: reset-time generation (`WorldResetService`) and fresh-run generation keep using the
flag as-is, so a flip is honoured at the next board. No GMCM exposure is added (the flag stays
config.json-only, as today).

## Tests

- `BoardInspectionTests`: Object-only board → false; board with `(W)13` slot → true; board with
  `(H)8` → true; category-ref-only ingredients ignored; empty board → false.
- Existing 670 tests stay green.

## Smoke (deployed build)

Config flag off, load the ZZZSMOKE6 clone whose board is legacy (no trophies) → log shows no
change; then `tly_genbundles` both ways is unaffected. The engine-board case is exercised by the
unit tests + the manifest dual-match log line on a reset smoke later in this session (C-phase
reset smoke runs with the flag on; the flip path is covered by unit tests and code review).
