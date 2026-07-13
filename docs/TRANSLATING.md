# Translating The Longest Year

All player-visible text lives in [`src/TheLongestYear/i18n/default.json`](../src/TheLongestYear/i18n/default.json)
(shipped in the mod folder as `i18n/default.json`). To add a language:

1. Copy `default.json` to `<locale>.json` in the same `i18n/` folder — e.g. `zh.json`
   (Chinese), `de.json`, `es.json`, `pt.json`, `fr.json`, `ja.json`, `ko.json`, `ru.json`.
2. Translate the **values only**. Never change the keys.
3. Preserve these EXACTLY as they appear:
   - `{{token}}` placeholders (e.g. `{{count}}`, `{{jp}}`, `{{cap}}`, `{{loopline}}`) — the
     game substitutes numbers/names at runtime. Token names are always lowercase; translated
     text can reorder them freely, but the `{{...}}` spelling itself must not change.
   - `@` — replaced with the player's name.
   - `#$b#` — dialogue page break.
   - `$h`, `$s`, `$a` — portrait pose codes.
   - `^` — line break inside the onboarding mail letter body (`mail.intro.body`).
4. Some keys come in `.one` / `.other` pairs (e.g. `hud.stash-full.one` /
   `hud.stash-full.other`) — these are English singular/plural variants, picked automatically
   based on a count. If your language doesn't inflect for plural, it's fine to put the same
   translated text in both the `.one` and `.other` values.
5. **`furniture.*` keys are display names embedded in a slash-delimited game data row**
   (`Data/Furniture`) — your translated value must NOT contain a literal `/` character, or
   it will split into extra fields and corrupt the row.
   **`event.intro.*` keys have the same restriction, for a different reason:** each value is
   substituted into a `speak <npc> "..."` command inside a slash-delimited, quote-wrapped
   vanilla event script (see `IntroEventInjector.BuildIntroEvent`). Your translated value must
   NOT contain a literal `/` or `"` character — either one will corrupt the event script (a
   `/` splits it into extra commands; a `"` breaks out of the quoted speech line early).
6. Drop the file into the installed mod folder (`Mods/TheLongestYear/i18n/`) and restart.
   SMAPI picks the file matching the game language; any key missing from your file
   falls back to English automatically, so partial translations work fine.

No DLL edits, no rebuilds — a JSON file is the whole translation. If you publish one,
tell us (Nexus DM or GitHub issue) and we'll link it from the mod page.
