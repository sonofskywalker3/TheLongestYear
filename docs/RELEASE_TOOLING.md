# Release Tooling

How a release of **The Longest Year** reaches GitHub and Nexus (mod **47192**).

## One command

After you've bumped `manifest.json`, updated `docs/nexus-description.bbcode` (+ the
README "What's New") and `CHANGELOG.md`, and **committed**, run:

```powershell
pwsh -NoProfile -File release.ps1
```

It builds the Release zip, pushes, creates the GitHub release `vX.Y.Z` with the zip,
and syncs the Nexus version field + description. That's the whole release.

## What's automated, and what can't be

| Nexus field | How | Touch |
|---|---|---|
| **File (zip)** | `.github/workflows/publish-nexus.yml` runs on `release.published` and uploads via `Nexus-Mods/upload-action` (file_group_id **7502657**, `archive_existing_file`). | **Zero-touch** — fires from the GitHub release. |
| **Mod version field + Description** | `AndroidConsolizer/release-notes/tly-publish-general.mjs` (Playwright vs. the logged-in profile `C:\Users\Jeff\.nexus-automation-profile`). Reads the version from `manifest.json`; description from `docs/nexus-description.bbcode`. | Local, run by `release.ps1`. **No Nexus API exists** for these, so it can't run in CI. |
| **Separate changelog tab** | — | **Does not exist** in the current Nexus mod-edit UI (verified 2026-06-05: tabs are General/Media/Files/Requirements/Permissions; "Version management" is only file-group naming). The description's **"What's New in X.Y.Z"** is the de-facto changelog, and it's covered by the row above. |

### Why description/version can't be CI'd
The Nexus **V2 GraphQL** has mutations for files and (collection) changelogs only —
**no mutation for a mod's description, summary, or version field** (introspected
2026-06-05). The **V1 REST** API is read-only for mod metadata. So the only way to
set them is the website, which needs a logged-in browser session — hence the local
Playwright step against the dedicated profile. CI can't hold that login reliably.

## Manual prep before `release.ps1`
1. Bump `Version` in `src/TheLongestYear/manifest.json`.
2. Update `docs/nexus-description.bbcode` "What's New in X.Y.Z" **and** the README to match (keep them content-identical — house style).
3. Add the version to `CHANGELOG.md`; optionally write `release-notes/X.Y.Z-nexus-changelog.txt` (used as the GitHub release notes).
4. Commit everything. Then run `release.ps1`.
