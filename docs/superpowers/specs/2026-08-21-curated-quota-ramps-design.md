# Curated per-name quota ramps for the remix pool — proposal (2026-08-21, awaiting ruling)

## Problem

`BundleClassifier` gives every pick-X-of-Y bundle whose name isn't in
`GameplayConfig.DefaultBundleQuotas` (7 names) a derived cumulative ramp
`floor(X × [0.25, 0.5, 0.75, 1.0])`. The engine's pools (vanilla `Data/Bundles` + every
`Data/RandomBundles` variant + the 11 authored defs) can produce **18** such bundles. The derived
ramp is blind to *what* the bundle asks for; the only protection is the generation-time clamp
(`GeneratedBundleSet.ClampRampForObtainability`), and that clamp only knows season pins for
crops / fish / crab-pot / forage — artisan goods, cooked dishes, minerals, artifacts, trophies,
bush berries and any-season fish are all treated as Spring-obtainable.

The checkpoint gate (`BundleRequirement.IsSatisfiedAtSeasonEnd`) never consults obtainability,
so a quota above what is obtainable by that season is a real, run-ending day-28 fail.

Survey source: `scratchpad/a4-ramp-survey.md` (pool enumeration + per-ingredient earliest
seasons, citing `VanillaBundlePool.cs`, `BundleSlotFiller.cs`, `AuthoredBundles.cs`,
`GameplayConfig.DefaultItemSeasonPins`, vanilla `Data/RandomBundles`).

## Findings (18 uncurated X<Y bundles)

| Bundle | X/Y | What it asks | Derived | Verdict | Proposed |
|---|---|---|---|---|---|
| **Winter Star** | 2/4 | Holly (Wi), Plum Pudding (Wi recipe), Stuffing (Fa/Wi), Powdermelon (Wi crop) | [0,1,1,2] | **Impossible Summer + Fall** — nothing exists before late Fall | **[0,0,0,2]** |
| **Forager's** | 2/3 | Salmonberry ×50 (Sp 15–18 only), Blackberry ×50 (Fa 8–11), Wild Plum ×15 (Fa) | [0,1,1,2] | **Trap** — Summer asks 1 but the only pre-Fall item expired on Spring 18 | **[0,0,2,2]** |
| **Gil's Trophies** | 2/4 | 4 of 11 trophies; 4 of the 11 are not year-1 (Slime Charmer, Napalm, Knight's Helmet, Arcane Hat) | [0,1,1,2] | ~9 % of loops roll ≥3 infeasible trophies → uncompletable; ~21 % have no Sp/Su-feasible trophy | **[0,0,1,2]** + trim the 4 late-game ids from `_gilTrophies` |
| **Brewer's** | 4/5 | re-rolled to 5 random Artisan Goods | [1,2,3,4] | Wrong Spring — ~5 of ~30 goods are Spring-feasible, and the clamp can't see it | **[0,1,2,4]** |
| **Preserver's** (authored) | 4/6 | 6 random Artisan Goods | [1,2,3,4] | same as Brewer's | **[0,1,2,4]** |
| **Mineral** (authored) | 4/6 | 6 geode minerals (⅓ regular, ⅓ Frozen 40+, ⅓ Magma/Omni 80+) | [1,2,3,4] | ~9 %/loop no Spring-obtainable mineral among 6 | **[0,1,3,4]** |
| **Home Cook's Feast** (authored) | 4/6 | 6 cooked dishes | [1,2,3,4] | Harsh Spring — no kitchen by Spring 28 on 500 g; curated Chef's is [0,1,2,3] | **[0,1,2,4]** |
| **Fish Farmer's** | 2/3 | Roe ×15, Aged Roe ×15, Squid Ink | [0,1,1,2] | Harsh Summer — 15 Roe needs a 5 000 g Fish Pond | **[0,0,1,2]** |
| **Artifact** (authored) | 4/6 | 6 random artifacts | [1,2,3,4] | Harsh Spring — 1 specific artifact of 6 by Spring 28 is a coin flip; cf. Adventurer's [0,1,2,2] | **[0,1,2,4]** |
| **Four Seasons Sampler** (authored) | 5/6 | 6 forage spanning ≥3 seasons | [1,2,3,5] | Lax vs expiry — you may skip only one item all year yet Spring asks 1 | **[1,3,4,5]** |
| **Rare Crops** | 1/2 | re-rolled to 2 random crops | [0,0,0,1] | Trap — first ask is Winter 28, when nothing grows | **[0,0,1,1]** |
| **Garden** | 4/5 | re-rolled to 5 random crops | [1,2,3,4] | Borderline — Winter 4th when only Powdermelon grows | **[1,2,4,4]** |
| Wild Medicine | 3/4 | Purple Mushroom, Fiddlehead, White Algae, Hops | [0,1,2,3] | OK | keep |
| Master Fisher's | 2/4 | 4 fish sharing a habitat | [0,1,1,2] | OK | keep |
| Treasure Hunter's | 5/6 | 6 gems | [1,2,3,5] | OK (obtainable 2/4/6/6) | keep |
| Children's | 3/4 | Ancient Doll, Ice Cream, Cookie, Salmonberry | [0,1,2,3] | OK | keep |
| Book (authored) | 3/5 | 5 books | [0,1,2,3] | borderline | keep |
| Tapper's / Weatherman's / Recycler's (authored) | 4/5, 4/5, 4/6 | tapper goods / fish / trash | [1,2,3,4] | OK | keep |

Side findings: curated **Chef's** never fires in the engine era (RandomBundles Chef's is 6/6 →
PerItem); **The Missing** (Abandoned Joja Mart) is never classified (room has no theme) so it
gates nothing; `DerivePins` could be extended to artisan goods / dishes / geode tiers so the
clamp catches these structurally instead of per-name (0.13.0 material).

## Proposal (for ruling)

**Must-fix (impossible or trap checkpoints):** Winter Star `[0,0,0,2]`, Forager's `[0,0,2,2]`,
Gil's Trophies `[0,0,1,2]` + trim the 4 late-game trophies, Brewer's + Preserver's `[0,1,2,4]`,
Mineral `[0,1,3,4]`.

**Should-fix (harsh/lax vs curated neighbours):** Home Cook's Feast `[0,1,2,4]`, Fish Farmer's
`[0,0,1,2]`, Artifact `[0,1,2,4]`, Four Seasons Sampler `[1,3,4,5]`, Rare Crops `[0,0,1,1]`,
Garden `[1,2,4,4]`.

Implementation is a table edit in `GameplayConfig.DefaultBundleQuotas` (+ the trophy-list trim
in `AuthoredBundles._gilTrophies`) with a test per new entry asserting the ramp is monotone and
ends at X; curated names already win over the derived ramp by name, and the obtainability clamp
still applies on top. User `BundleQuotas` overrides keep working.

## Ruling needed

1. Apply the must-fix set? (recommended: yes)
2. Apply the should-fix set too, or only some of it?
3. Trim Gil's Trophies to the 7 year-1-feasible ids, or keep the full 11 and rely on the ramp?
