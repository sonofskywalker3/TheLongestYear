# One-command release for The Longest Year.
#
# Releases the version currently in manifest.json to BOTH GitHub and Nexus:
#   1. Builds the Release zip.
#   2. Pushes commits, creates the GitHub release (tag vX.Y.Z) with the zip.
#      -> .github/workflows/publish-nexus.yml then auto-uploads the zip to Nexus
#         (mod 47192) and archives the previous file. Zero-touch.
#   3. (RETIRED 2026-08-25) The Nexus version field + description used to be synced
#      by a Playwright script against a dedicated Chrome profile. That profile's
#      logins kept expiring, so the Nexus page (version, description, changelog)
#      is now edited through Claude-in-Chrome on the regular signed-in browser.
#      Pass -LegacyNexusDesc to run the old Playwright step anyway.
#
# PREREQS: bump the version in manifest.json, update docs/nexus-description.bbcode
# (+ README "What's New") and CHANGELOG.md, and commit -- all BEFORE running this.
#
# Usage:  pwsh -NoProfile -File release.ps1            (build + GitHub release + Nexus file)
#         pwsh -NoProfile -File release.ps1 -SkipBuild (reuse existing zip)
#         pwsh -NoProfile -File release.ps1 -LegacyNexusDesc  (also run the retired Playwright sync)
#         -SkipNexusDesc is accepted for compatibility and is now the default behaviour.

param([switch]$SkipBuild, [switch]$SkipNexusDesc, [switch]$LegacyNexusDesc)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $root
try {
    $manifest = Join-Path $root 'src\TheLongestYear\manifest.json'
    $version  = (Get-Content $manifest -Raw | ConvertFrom-Json).Version
    $tag      = "v$version"
    Write-Host "=== Releasing The Longest Year $tag ===" -ForegroundColor Cyan

    # Guard: clean working tree (don't release uncommitted code).
    if (git status --porcelain) {
        throw "Working tree is dirty. Commit (and bump manifest/docs) before releasing."
    }
    # Guard: tag not already released.
    if (gh release view $tag 2>$null) {
        throw "Release $tag already exists. Bump the version in manifest.json first."
    }

    # 1. Build.
    if (-not $SkipBuild) {
        dotnet build (Join-Path $root 'src\TheLongestYear\TheLongestYear.csproj') -c Release -v quiet
        if ($LASTEXITCODE -ne 0) { throw "Release build failed." }
    }
    $zip = Join-Path $root "src\TheLongestYear\bin\Release\net6.0\TheLongestYear $version.zip"
    if (-not (Test-Path $zip)) { throw "Release zip not found: $zip" }

    # 2. Push + GitHub release (fires publish-nexus.yml -> Nexus file upload).
    git push origin master
    $notesFile = Join-Path $root "release-notes\$version-nexus-changelog.txt"
    $notes = if (Test-Path $notesFile) { Get-Content $notesFile -Raw } else { "The Longest Year $version" }
    gh release create $tag $zip --title $tag --notes $notes
    if ($LASTEXITCODE -ne 0) { throw "gh release create failed." }
    Write-Host "GitHub release $tag created. publish-nexus.yml will upload the file to Nexus." -ForegroundColor Green

    # 3. Retired: Nexus version field + description via Playwright. Opt-in only.
    if ($LegacyNexusDesc) {
        $env:NEXUS_PW_PROFILE = 'C:\Users\Jeff\.nexus-automation-profile'
        $script = 'C:\Users\Jeff\Documents\Projects\Stardee Valoo\AndroidConsolizer\release-notes\tly-publish-general.mjs'
        Push-Location (Split-Path $script)   # so 'playwright' resolves from release-notes/node_modules
        try { node $script } finally { Pop-Location }
        if ($LASTEXITCODE -ne 0) { throw "Nexus description/version sync failed (exit $LASTEXITCODE)." }
        Write-Host "Nexus version field + description synced to $version." -ForegroundColor Green
    }

    Write-Host "=== Done. $tag is on GitHub; file auto-uploads to Nexus. Now sync the Nexus version/description/changelog via Claude-in-Chrome. ===" -ForegroundColor Cyan
}
finally { Pop-Location }
