# Release Tooling

How a release of **The Longest Year** reaches GitHub and Nexus (mod **47192**).

**Nothing in a release is hand-pasted.** The zip upload is CI; every Nexus page field is
driven by the agent through Claude-in-Chrome on Jeff's regular signed-in browser. Jeff's
standing instruction (2026-08-26): "no more manual pasting, you do everything by driving my
chrome."

## The sequence

### 1. Prep, then commit

1. Bump `Version` in `src/TheLongestYear/manifest.json`.
2. Update `docs/nexus-description.bbcode` "What's New in X.Y.Z" **and** the README to match.
   They must stay content-identical (same sections, order, wording) and differ only in markup.
3. Add the version to `CHANGELOG.md`.
4. Write `release-notes/X.Y.Z-nexus-changelog.txt`. This one file is used twice: as the GitHub
   release notes and as the Nexus changelog body.
5. Commit everything. `release.ps1` refuses to run on a dirty tree.

### 2. One command

```powershell
pwsh -NoProfile -File release.ps1
```

Builds the Release zip, pushes `master`, and creates GitHub release `vX.Y.Z` with the zip
attached. That fires `.github/workflows/publish-nexus.yml`, which uploads the file to Nexus
(file_group_id **7502657**, `archive_existing_file`) and archives the previous one. Zero-touch.

### 3. The Nexus page, via Claude-in-Chrome

No Nexus API exists for the version field, description, or changelog (see *Why this can't be
CI'd* below), so these run against the logged-in browser. The agent does all of it; Jeff does
not paste anything.

**Version field + description** - `/games/stardewvalley/mods/47192/edit/general`:

```js
// description: SCEditor will not take a plain .value= assignment
ta._sceditor.val(bbcode)                      // bbcode = docs/nexus-description.bbcode verbatim
```

Then the version field via its native setter (React ignores a bare `.value =`):

```js
const el = document.querySelector('#mod-version');
Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype,'value')
  .set.call(el, '0.15.0');
el.dispatchEvent(new Event('input', {bubbles:true}));
```

Then **one real keystroke inside the editor iframe** (click it, then `space` `Backspace`) -
without a genuine input event the header Save stays disabled. Click Save (header, approx.
906,50) and wait for "Mod saved successfully". Verify on the public page.

**Changelog entry** - `/games/stardewvalley/mods/47192/edit/documents`:

Click "Add changelog". The file and version are auto-matched to the CURRENT upload; only the
current file can be given an entry, so old versions cannot be backfilled. Fill the textarea
with `form_input` rather than `type` - typing a "/" on Nexus opens the site search. Save.

**Gotchas that have each cost a session** (full list in the user memory
`nexus-use-regular-chrome`):

- After a fresh navigation, the "Add changelog" button often needs a **second click by
  coordinates**; the first one does not open the dialog.
- The tools redact anything token-shaped in a JS result (`[BLOCKED: JWT token]`). Read the
  live version back with an `innerText` regex, never by returning a raw field value.
- If the extension goes dead mid-session ("Browser extension is not connected", empty
  `list_connected_browsers`), the native-host bridge has died (extension or Claude Code
  auto-update, or Chrome idling the worker). Have Jeff open `https://clau.de/chrome/reconnect`
  in Chrome, wait 5 s, retry. No need to kill Chrome; do not blame the login.
- Flood control silently drops form submits within ~30s of a previous one.

## What runs where

| Nexus field | How | Who touches it |
|---|---|---|
| **File (zip)** | `publish-nexus.yml` on `release.published`, via `Nexus-Mods/upload-action`. | Nobody. CI. |
| **Version field + Description** | `/edit/general` through Claude-in-Chrome, per the recipe above. | Agent. |
| **Changelog entry** | `/edit/documents` through Claude-in-Chrome, per the recipe above. | Agent. |

## Why this can't be CI'd

The Nexus **V2 GraphQL** API has mutations for files and for *collection* changelogs only, and
**no mutation for a mod's description, summary, or version field** (introspected 2026-06-05).
The **V1 REST** API is read-only for mod metadata. The website is therefore the only surface
that can set them, and it needs a logged-in session. "Agent-driven browser" is not a stopgap
here - it is the only route that exists.

## History

Until 2026-08-25 the version/description step ran as a Playwright script
(`AndroidConsolizer/release-notes/tly-publish-general.mjs`) against a dedicated Chrome profile
(`C:\Users\Jeff\.nexus-automation-profile`). That profile's Nexus session expired constantly and
it forced Jeff to log in twice. It was retired on 2026-08-25 and **removed from `release.ps1`
entirely on 2026-08-26**. Do not reintroduce it.
