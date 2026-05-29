# Dump the TheLongestYear upgrade catalog as a styled HTML page and open it in
# the default browser. Reflects over the freshly-built TheLongestYear.Core.dll
# rather than parsing the source — that way we get the programmatic chains
# (tool keeps, skill-level keeps, mine-elevator keeps) too.

param(
    [string]$DllPath = "$PSScriptRoot\..\src\TheLongestYear.Core\bin\Release\net6.0\TheLongestYear.Core.dll",
    [string]$OutPath = "$env:TEMP\tly-upgrades.html"
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $DllPath)) {
    Write-Error "Core DLL not found at $DllPath — run a Release build first."
    exit 1
}

# Load the assembly into a throwaway AppDomain isn't trivial in PowerShell; just
# load it into this session. PS will hold the file lock until exit but we don't
# care for a one-shot view.
$asm = [System.Reflection.Assembly]::LoadFrom((Resolve-Path $DllPath))
$catalog = $asm.GetType('TheLongestYear.Core.UpgradeCatalog', $true)
$all = $catalog.GetProperty('All').GetValue($null)

$entries = @()
foreach ($u in $all) {
    $entries += [PSCustomObject]@{
        Id           = $u.Id
        Category     = $u.Category.ToString()
        DisplayName  = $u.DisplayName
        Description  = $u.Description
        Cost         = [int64]$u.Cost
        Prereq       = if ($u.PrerequisiteId) { $u.PrerequisiteId } else { '' }
        MetaReq      = if ($u.MetaRequirement) { $u.MetaRequirement } else { '' }
    }
}

$totalCount = $entries.Count
$totalCost  = ($entries | Measure-Object -Property Cost -Sum).Sum

# Group by category, preserving the enum declaration order.
$categoryOrder = @('Loadout','Carryover','Efficiency','Obtainability','Foresight','Stash','Buildings')

$sb = [System.Text.StringBuilder]::new()
$header = @'
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>The Longest Year — Upgrade Catalog</title>
<style>
  body { font-family: -apple-system, Segoe UI, Roboto, sans-serif; max-width: 1280px; margin: 24px auto; padding: 0 24px; background: #f7f5ef; color: #2a2a2a; }
  h1 { margin: 0 0 4px; font-size: 28px; }
  .meta { color: #6b6555; font-size: 13px; margin-bottom: 24px; }
  h2 { margin: 32px 0 8px; padding: 6px 12px; border-radius: 4px; font-size: 18px; color: #fff; }
  h2.Loadout       { background: #6a8e4e; }
  h2.Carryover     { background: #4e7b8e; }
  h2.Efficiency    { background: #8e6a4e; }
  h2.Obtainability { background: #8e4e7b; }
  h2.Foresight     { background: #5a4e8e; }
  h2.Stash         { background: #8e8a4e; }
  h2.Buildings     { background: #4e8e6e; }
  table { width: 100%; border-collapse: collapse; background: #fff; box-shadow: 0 1px 2px rgba(0,0,0,0.06); }
  th { text-align: left; padding: 8px 12px; background: #efeadb; border-bottom: 2px solid #d8d2bf; font-size: 13px; }
  td { padding: 8px 12px; border-bottom: 1px solid #eee2d0; vertical-align: top; font-size: 14px; }
  tr:last-child td { border-bottom: none; }
  .id     { font-family: Consolas, Menlo, monospace; font-size: 12px; color: #6b6555; }
  .cost   { text-align: right; font-variant-numeric: tabular-nums; white-space: nowrap; }
  .prereq { font-family: Consolas, Menlo, monospace; font-size: 12px; color: #888; }
  .meta-req { font-family: Consolas, Menlo, monospace; font-size: 12px; color: #b07a3a; }
  .desc { color: #443f33; }
  .summary { background: #fff; padding: 12px 16px; border-radius: 4px; box-shadow: 0 1px 2px rgba(0,0,0,0.06); display: inline-block; }
  .summary strong { font-size: 18px; }
</style>
</head>
<body>
<h1>The Longest Year — Upgrade Catalog</h1>
<div class="meta">Reflected from <code>TheLongestYear.Core.dll</code></div>
'@
[void]$sb.AppendLine($header)
[void]$sb.AppendLine("<div class=`"summary`"><strong>$totalCount</strong> upgrades &middot; <strong>$('{0:N0}' -f $totalCost) JP</strong> for the complete set</div>")

foreach ($cat in $categoryOrder) {
    $catEntries = $entries | Where-Object { $_.Category -eq $cat }
    if ($catEntries.Count -eq 0) { continue }
    $catCost = ($catEntries | Measure-Object -Property Cost -Sum).Sum
    [void]$sb.AppendLine("<h2 class=`"$cat`">$cat &mdash; $($catEntries.Count) entries, $('{0:N0}' -f $catCost) JP total</h2>")
    [void]$sb.AppendLine('<table>')
    [void]$sb.AppendLine('<thead><tr><th>Name</th><th>Id</th><th>Description</th><th class="cost">Cost</th><th>Prereq</th><th>Meta req</th></tr></thead>')
    [void]$sb.AppendLine('<tbody>')
    foreach ($e in $catEntries) {
        $name   = [System.Net.WebUtility]::HtmlEncode($e.DisplayName)
        $idHtml = [System.Net.WebUtility]::HtmlEncode($e.Id)
        $desc   = [System.Net.WebUtility]::HtmlEncode($e.Description)
        $pre    = [System.Net.WebUtility]::HtmlEncode($e.Prereq)
        $mreq   = [System.Net.WebUtility]::HtmlEncode($e.MetaReq)
        $cost   = '{0:N0}' -f $e.Cost
        [void]$sb.AppendLine("<tr><td>$name</td><td class=`"id`">$idHtml</td><td class=`"desc`">$desc</td><td class=`"cost`">$cost</td><td class=`"prereq`">$pre</td><td class=`"meta-req`">$mreq</td></tr>")
    }
    [void]$sb.AppendLine('</tbody></table>')
}

[void]$sb.AppendLine('</body></html>')

Set-Content -LiteralPath $OutPath -Value $sb.ToString() -Encoding UTF8
Write-Host "Wrote $totalCount upgrades to $OutPath ($('{0:N0}' -f $totalCost) JP total)."
Start-Process $OutPath
