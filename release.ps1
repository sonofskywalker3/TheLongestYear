# One-command release for The Longest Year.
#
# Releases the version currently in manifest.json to BOTH GitHub and Nexus:
#   1. Builds the Release zip.
#   2. Pushes commits, creates the GitHub release (tag vX.Y.Z) with the zip.
#      -> .github/workflows/publish-nexus.yml then auto-uploads the zip to Nexus
#         (mod 47192) and archives the previous file. Zero-touch.
#   3. The Nexus PAGE (version field, description, changelog entry) is then driven
#      by the agent through Claude-in-Chrome on Jeff's regular signed-in browser.
#      There is no Nexus API for those fields, so a logged-in browser is the only
#      route -- but nothing about it is hand-pasted. See docs/RELEASE_TOOLING.md
#      for the exact steps and the user memory nexus-use-regular-chrome.
#
# The Playwright path (dedicated .nexus-automation-profile) was REMOVED 2026-08-26:
# its Nexus session expired constantly and it forced a second login. Do not bring
# it back -- Claude-in-Chrome on the regular browser is the only supported route.
#
# PREREQS: bump the version in manifest.json, update docs/nexus-description.bbcode
# (+ README "What's New") and CHANGELOG.md, write release-notes/<version>-nexus-changelog.txt,
# and commit -- all BEFORE running this.
#
# Usage:  pwsh -NoProfile -File release.ps1            (build + GitHub release + Nexus file)
#         pwsh -NoProfile -File release.ps1 -SkipBuild (reuse existing zip)

param([switch]$SkipBuild)
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

    Write-Host "=== Done. $tag is on GitHub; the workflow uploads the file to Nexus. ===" -ForegroundColor Cyan
    Write-Host "Next: the agent drives the Nexus page (version + description + changelog) via Claude-in-Chrome -- see docs/RELEASE_TOOLING.md." -ForegroundColor Cyan
}
finally { Pop-Location }
