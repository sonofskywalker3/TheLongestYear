# Release Tooling

How a release of **The Longest Year** reaches GitHub and Nexus (mod **47192**).

## One command, then the Nexus page

After you've bumped `manifest.json`, updated `docs/nexus-description.bbcode` (+ the
README "What's New") and `CHANGELOG.md`, and **committed**, run:

```powershell
pwsh -NoProfile -File release.ps1
```

It builds the Release zip, pushes, and creates the GitHub release `vX.Y.Z` with the zip;
the workflow then uploads the file to Nexus. The Nexus **page** (version field, description,
changelog entry) is edited afterwards through Claude-in-Chrome on the regular signed-in
browser. Steps and gotchas live in the user memory `nexus-use-regular-chrome.md`.

## What's automated, and what isn't

| Nexus field | How | Touch |
|---|---|---|
| **File (zip)** | `.github/workflows/publish-nexus.yml` runs on `release.published` and uploads via `Nexus-Mods/upload-action` (file_group_id **7502657**, `archive_existing_file`). | **Zero-touch**: fires from the GitHub release. |
| **Mod version field + Description** | `/games/stardewvalley/mods/47192/edit/general` in the regular browser via Claude-in-Chrome: `ta._sceditor.val(bbcode)` from `docs/nexus-description.bbcode`, native-setter on `#mod-version`, one real keystroke in the editor, header Save. Verify on the public page. | Manual (agent-driven). **No Nexus API exists** for these. |
| **Changelog entry** | `/edit/documents` → "Add changelog" (file auto-matched to the current upload) → paste `release-notes/X.Y.Z-nexus-changelog.txt` → Save. | Manual (agent-driven). Only the current file can be given an entry. |

### History
Until 2026-08-25 the version/description step ran as a Playwright script
(`AndroidConsolizer/release-notes/tly-publish-general.mjs`) against a dedicated Chrome profile
(`C:\Users\Jeff\.nexus-automation-profile`). That profile's Nexus login kept expiring, so the
step was retired; `release.ps1 -LegacyNexusDesc` still runs it if ever needed.

### Why description/version can't be CI'd
The Nexus **V2 GraphQL** has mutations for files and (collection) changelogs only, and
**no mutation for a mod's description, summary, or version field** (introspected
2026-06-05). The **V1 REST** API is read-only for mod metadata. So the only way to
set them is the website, which needs a logged-in browser session.

## Manual prep before `release.ps1`
1. Bump `Version` in `src/TheLongestYear/manifest.json`.
2. Update `docs/nexus-description.bbcode` "What's New in X.Y.Z" **and** the README to match (keep them content-identical, house style).
3. Add the version to `CHANGELOG.md` and write `release-notes/X.Y.Z-nexus-changelog.txt` (used as the GitHub release notes and as the Nexus changelog paste).
4. Commit everything. Then run `release.ps1`, then do the Nexus page.
