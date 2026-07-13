# i18n String Extraction — Design

**Date:** 2026-07-13
**Status:** Approved (brainstorm complete; next step = implementation plan)
**Motivation:** Fluxwb's Chinese translation (Nexus 47926) is a modified DLL frozen at
0.11.0 because every TLY string is a hardcoded C# literal. Move all player-visible text
to SMAPI `i18n/` JSON so translations are a JSON file, never a DLL edit. User approved
the pass 2026-07-13 (pre-0.12 queue item 4).

## Decisions made during brainstorm

1. **Scope: everything player-visible** (~250–350 distinct strings), including the Day-1
   intro event speak-lines and the composed/dynamic strings (quest checklists, plurals).
2. **Fluxwb path: notify + migration guide.** We ship `default.json` (English) and a
   translator guide; Fluxwb translates values into `zh.json`. No pre-seeded zh.json, no
   DLL extraction of their work.
3. **Core bridge: injected translation delegate.** `TheLongestYear.Core` stays SMAPI-free.
4. **No rewording:** English output stays byte-identical in this pass. Pure extraction.
5. **Out of scope:** ChampionService/CurrentChampion code rename (parked separately);
   console/debug-bridge text, `Monitor.Log` lines, and all mechanical ids stay English.

## Architecture

### The `Strings` facade (new, in TheLongestYear.Core)

```csharp
public static class Strings
{
    // provider: (key, tokens) => translated string. tokens may be null.
    public static void Init(Func<string, object?, string> provider);
    public static string Get(string key);
    public static string Get(string key, object tokens);   // anonymous object, SMAPI-style
}
```

- `ModEntry.Entry` calls `Strings.Init((key, tokens) => Helper.Translation.Get(key, tokens))`
  once. `Helper.Translation` re-reads on SMAPI's `LocaleChanged`, so no re-init is needed,
  but we DO handle `LocaleChanged` for asset re-injection (below).
- **Uninitialized behavior:** `Get` returns the key itself. Loud in-game, never a crash.
- **Tests:** a one-time fixture loads the real `src/TheLongestYear/i18n/default.json`
  into a dictionary provider (with `{{token}}` substitution). The 546 existing tests keep
  asserting real English text and become a de-facto missing-key detector.

### i18n folder

`src/TheLongestYear/i18n/default.json` — English source of truth, shipped in the mod
folder (verify the mod-build-package includes `i18n/`; add an ItemGroup if not).
Translators add `zh.json` etc. beside it. SMAPI falls back per-key to `default.json`,
so partial translations degrade gracefully.

`default.json` is organized with `//` section comments (SMAPI strips them) mirroring the
key prefixes below, and a header comment documenting the tokens that must be preserved
(`{{name}}`-style tokens, and dialogue codes `@`, `#$b#`, `$h/$s/$a`).

## Key naming

Dotted, surface-prefixed. Upgrade/modifier keys derive from the existing stable ids.

| Prefix | Content | ~Count |
|---|---|---|
| `upgrade.{id}.name` / `.desc` | hand-authored catalog rows (`upgrade.backpack_1.name`) | ~120 |
| `upgrade-tpl.{template}.name` / `.desc` | generator templates with tokens, e.g. `"Keep {{tier}} {{tool}}"`; plus `tier.{slug}`, tool/skill display-name keys | ~10 templates (replaces ~60 generated rows) + token keys |
| `theme.{slug}` | Theme enum display names (farming, foraging, …) | 5 |
| `upgrade-category.{slug}` | shrine tab labels (loadout, carryover, …) | 7 |
| `modifier.{id}` | ThemeModifiers.DisplayNameFor switch values | ~20 |
| `menu.hub.*`, `menu.shrine.*`, `menu.shrine-preview.*`, `menu.goals.*`, `menu.cookbook.*`, `menu.craftbook.*`, `menu.victory.*` | self-drawn menu labels | ~55 |
| `quest.weekly.*`, `quest.stash.*`, `quest.shrine.*` | quest titles/descriptions/objective fragments | ~10 |
| `hud.*` | HUD / red messages (incl. plural variants) | ~8 |
| `dialog.cave.*`, `dialog.win.*`, `dialog.joja`, `dialog.mines.*` | question/message dialogues | ~12 |
| `gmcm.*` | config menu names/tooltips/paragraph | ~7 |
| `mail.intro.title` / `.body` | onboarding letter | 2 |
| `furniture.*` | book kit + planning shrine display names | ~6 |
| `event.intro.lewis-{n}` / `event.intro.junimo-{n}` | intro event speak payloads ONLY | ~22 |
| `cutscene.day28.fail` / `.continue` (+ `menu.cutscene.continue-hint`) | Day-28 blocks | 3 |
| `quality.silver/.gold/.iridium`, `egg-color.white/.brown` | shared tags | 5 |

Enum `.ToString()` display sites (Theme, UpgradeCategory) switch to keyed lookups; the
enums themselves are untouched (they're also persisted/parsed — mechanical).

**Never translated** (stay hardcoded): save/modData keys, quest ids (`tly.*`), upgrade
ids and requirement tokens (`tool:hoe:1`), modifier ids, item ids/qualifiers, event ids,
mail flags, `Response` keys, asset paths, sound cues, event command tokens, console
command names/usage text, all `Monitor.Log` output.

## Composed strings

- **Interpolation:** SMAPI's native `{{token}}` replacement everywhere; no `string.Format`.
- **Weekly quest objective:** structure (loop + `string.Join`) stays in code; fragments
  become keys — `quest.weekly.progress` = `"Donated {{done}}/{{total}}:"`,
  `quest.weekly.slot` = `"{{item}}{{color}}{{qty}}{{quality}} - {{bundle}}"`, checkbox
  markers `[X]`/`[ ]` stay code-side (mechanical).
- **Use vanilla's localization where it exists:** item names via `Item.DisplayName`,
  season names via the game's localized season strings, weekday short-names likewise.
  We never re-translate what the game ships.
- **Plurals:** explicit key variants (SMAPI has no plural engine):
  `hud.stash-full.one`/`.other`, `dialog.win.loop-line.first`/`.many`. The inline
  `slot{(cap==1?"":"s")}` pattern is removed.
- **Quality/egg tags:** the 3 duplicated `quality switch` blocks collapse into one shared
  helper reading the `quality.*` keys; egg colors read `egg-color.*`.
- **Templates (UpgradeCatalogGenerators):** name/description templates become token keys;
  the conditional L5/L10 clause (`… Re-triggers the profession picker for Level {{level}}.`)
  becomes its own key appended conditionally, as today.

## Special surfaces

- **Intro event (`IntroEventInjector`):** command array unchanged; only `speak` payloads
  read from keys. Dialogue codes live inside the translated values.
- **Day-28 cutscene:** two multi-page blocks → two keys; `Day28DialogueScript` parsing
  unchanged (it operates on whatever string it receives).
- **GMCM:** register with `Func<string>` lambdas (`() => Strings.Get("gmcm.…")`) so
  language switches update live (GMCM's intended pattern).
- **Mail + furniture (Data/* asset edits):** edit lambdas read keys; on `LocaleChanged`
  invalidate `Data/Mail` + `Data/Furniture` so re-injection happens in the new language.

## Testing & verification

1. Existing 546 tests run against a `default.json`-backed provider (fixture).
2. New guard test, two halves: (a) every **literal** `Strings.Get("…")` key in the
   source resolves in `default.json` (regex scan over the source tree at test time);
   (b) **dynamically composed** key families — `upgrade.{id}.*`, `theme.*`,
   `upgrade-category.*`, `modifier.*` — are verified by iterating the actual catalog /
   enums / modifier table and resolving each generated key. Orphan detection (keys in
   `default.json` nothing references) combines both sets.
3. New guard test: every `{{token}}` in a `default.json` value is supplied at its call
   site(s) (token round-trip on format strings).
4. End verification: PC pass over every surface — shrine, shrine preview, hub, season
   goals, cookbook/craftbook, quest log, GMCM, onboarding mail, Day-1 intro on a fresh
   TLY save, Day-28 via the `tly_commands` bridge — confirming pixel-identical English.

## Rollout (each step builds green; PATCH bump + commit per change, per house rules)

1. Scaffold: `Strings` facade, empty `i18n/default.json`, test fixture, build packaging.
2. Upgrade catalog (hand-authored rows) + generator templates.
3. Themes / modifiers / category labels (enum display sites).
4. Menus, one commit per menu (hub, shrine, shrine-preview, goals, cookbook, craftbook,
   victory).
5. Quests + HUD + question dialogues (incl. shared quality/egg-tag helper).
6. Mail, furniture names, GMCM (+ LocaleChanged invalidation).
7. Intro event + Day-28 cutscene.
8. Guard tests + `docs/TRANSLATING.md`.

## Fluxwb hand-off

- `docs/TRANSLATING.md`: copy `default.json` → `zh.json`, translate values only,
  preserve `{{tokens}}` and dialogue codes, per-key English fallback, no DLL edits.
- On release: DM Fluxwb (Nexus) with the guide link; credit them in the README's
  translations section. Outbound messages wait for the user's explicit OK, per
  workspace rules.
