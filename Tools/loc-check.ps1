$repo = 'C:\Users\emred\encore'
# Collect files to search
$extensions = @('*.xaml','*.cs','*.xml','*.yml','*.yaml','*.fs','*.fsx','*.ts','*.tsx','*.js','*.jsx','*.json')
$files = Get-ChildItem -Path $repo -Recurse -Include $extensions -File -ErrorAction SilentlyContinue

$p1 = @()
$p2 = @()
foreach ($f in $files) {
    $text = $null
    try { $text = Get-Content $f.FullName -Raw -ErrorAction SilentlyContinue } catch {}
    if ($null -eq $text) { continue }
    $m = [regex]::Matches($text, "\{Loc\s'([^']+)'\}")
    foreach ($match in $m) { $p1 += $match.Groups[1].Value }
    $m2 = [regex]::Matches($text, 'Loc\.GetString\(\s*"([^"\)]+)"')
    foreach ($match in $m2) { $p2 += $match.Groups[1].Value }
}

$keys = ($p1 + $p2) | Where-Object { $_ } | Sort-Object -Unique

# Collect en-US keys from .ftl files
$ftlFiles = Get-ChildItem -Path (Join-Path $repo 'Resources\Locale\en-US') -Recurse -Include *.ftl -File -ErrorAction SilentlyContinue
$enkeys = @()
foreach ($f in $ftlFiles) {
    $text = $null
    try { $text = Get-Content $f.FullName -Raw -ErrorAction SilentlyContinue } catch {}
    if ($null -eq $text) { continue }
    $m = [regex]::Matches($text, '^[\s\t]*([^ \t=]+)[ \t]*=', 'Multiline')
    foreach ($match in $m) { $enkeys += $match.Groups[1].Value }
}
$enkeys = $enkeys | Sort-Object -Unique

$missing = $keys | Where-Object { $_ -and -not ($enkeys -contains $_) }

# Output results to file
$out = Join-Path $repo 'loc-check-report.txt'
$report = @()
$report += "Total used keys found: $($keys.Count)"
$report += "Total en-US keys found: $($enkeys.Count)"
$report += "Missing keys count: $($missing.Count)"
$report += ""
$report += "---Missing keys---"
$report += $missing
$report += ""
$report += "---Sample en-US files with few non-blank lines---"

foreach ($f in $ftlFiles) {
    $nonblank = (Get-Content $f.FullName | Where-Object { $_ -match '\\S' }).Count
    if ($nonblank -lt 3) {
        $report += "$($f.FullName) (non-blank lines: $nonblank)"
    }
}

$report | Out-File $out -Encoding UTF8
Write-Output "Report written to $out"
Write-Output "MissingCount:$($missing.Count)"
Write-Output "Done"
