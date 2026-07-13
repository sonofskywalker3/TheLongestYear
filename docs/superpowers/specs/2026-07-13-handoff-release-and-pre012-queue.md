# Handoff — 2026-07-13 session (v0.11.44 RELEASED; pre-0.12 queue underway)

**Repo state:** master at `v0.11.45`, clean tree, all pushed. Deployed PC Mods folder has
**0.11.44** (0.11.45 builds clean but is NOT deployed — game was running; deploy via
`tools/deploy.ps1`, which archives the log first). PC game may still be running Run 31 on the
user's test save.

## Shipped this session
- **v0.11.42–44** (all live-verified): kept buildings rebuild at the player's own tiles
  (`MetaState.KeptBuildingSpots` snapshot pre-reset, footprint bulldozed; fixed tiles = legacy
  fallback — the 1.6 FARMHOUSE is a Building at (59,12) that hid the old silo tile); reset
  re-resolves weather via vanilla's `UpdateWeatherForNewDay → ApplyWeatherForNewDay →
  updateWeatherIcon` (storms had serialized onto Spring 1); v0.11.41 clothes-keep confirmed.
- **v0.11.44 RELEASED** to GitHub + Nexus (file live as MAIN, version + description synced;
  README ≡ bbcode "What's New in 0.11.44"; CHANGELOG.md entry; notes in
  `release-notes/0.11.44-nexus-changelog.txt`). Release needed TWO fixes: `release.ps1
  -SkipBuild` (game locks the DLL), and the **upload-action pin** (see memory
  `nexus-upload-action-v3-pin.md` — all 3 repos pinned to `ee1af4be…`, deprecated flow dies
  **2026-09-09**).
- **v0.11.45**: one-time NetWorldState keep/wipe audit — full ruling table in
  `2026-07-13-networldstate-audit-design.md`; the loadForNewGame-survival leak class is closed.
- **Community update posted + verified** (user-approved; Nexus posts tab deliberately skipped):
  Reddit r/StardewValley + r/StardewValleyMods + r/SMAPI comments, forums.stardewvalley.net
  reply, VeggieGirl43 Better-Chests retest DM. Tracking in `marketing/announce-tracking.md`.
  Forums login was re-armed via `tly-forum-wait-login.mjs` (profile sessions expire!).

## Remaining pre-0.12 queue (user directive: finish these BEFORE 0.12.0 balance work)

### 3. Advanced Options screenshot for the Nexus page (promised to khauser13 2026-06-10)
A screenshot of the new-game **Advanced Options** panel highlighting the recommended
**Community Center Bundles → Remixed** setting, uploaded to the mod page media.
- Needs the USER to navigate: title screen → New → Advanced Options (can't drive character
  creation unattended). Capture via PrintWindow **by game HWND** — the window finder matches
  the SMAPI console first (both titles contain "Stardew Valley"; see memory
  `printwindow-screenshot-sdv.md` + script `test-output/printwindow.ps1`).
- Upload via the `tly-media-upload.mjs` pattern (AC/release-notes, NEXUS_PW_PROFILE
  `C:\Users\Jeff\.nexus-automation-profile`). Media upload = publish → needs the user's OK.

### 4. i18n string extraction (Fluxwb, Nexus mod 47926 — user APPROVED the pass 2026-07-13)
Move user-facing strings to SMAPI `i18n/` JSON so translations stop requiring DLL edits
(Fluxwb's Chinese copy is frozen at 0.11.0 for exactly this reason; user publicly offered to
make future translations easier). LARGE pass — **brainstorm → design spec first** (Superpowers
flow): i18n key layout, which strings are translatable vs mechanical ids, GMCM strings,
Day28 dialogue scripts, how Fluxwb migrates. Also remember the parked rename: "Selected" not
"championed" wording is done, but ChampionService/CurrentChampion CODE rename is pending —
don't fold it in unless asked.

### 5. Nexus upload v3 migration (DEADLINE 2026-09-09 — pinned flow dies)
All 3 repos (TLY / AC / CartCatalog, branch `main` for CC) pin
`Nexus-Mods/upload-action@ee1af4be8e1be6773ab5aeb02bacc6d638b20deb`. Legacy fileId 170754
404'd on `POST /v3/mod-files/{id}/versions` ("Mod file not found") — legacy mods appear to
have no v3 mod-file identity. Next step: **probe from a workflow debug step** (v3 needs the
API key = GH secret `NEXUS_API_KEY`; unauthenticated → 401): `GET /v3/games/stardewvalley/
mods/47192` → global id → `GET /v3/mods/{id}/files`; if empty, a v3 file may need creating
once via `POST /v3/mod-files` from a finalised upload (the schema's "create NEW mod file"
path) — after which versions attach normally. Group ids for the pinned flow: TLY 7502657,
AC 7118491. Current live main file: legacy fileId 174860 ("The Longest Year 0.11.44").

## Open questions / loose ends
- VeggieGirl43 retest answer (DM sent today) and any 0.11.44 reactions on the threads — next
  forum sweep will catch them (`sweep-forums.mjs`).
- `EnableThemeReroll: true` still set in the DEPLOYED config.json (playtest convenience).
- Balance reports → 0.12.0/0.13.0 brainstorm is the NEXT milestone after this queue.
