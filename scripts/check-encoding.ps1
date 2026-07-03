# check-encoding.ps1 - Wave 8 SC7: Mojibake encoding lint for .cs/.razor files
# Scans source files for UTF-8 mojibake patterns (Issue 7 regression prevention).
#
# Mojibake occurs when UTF-8 Vietnamese text is mis-decoded as Latin-1/Windows-1252.
# The signature is a SEQUENCE of two Latin-1 chars: a lead byte (U+00C2-U+00DF)
# followed by a continuation byte (U+0080-U+00BF). A single Latin-1 char like
# Ó (U+00D3) or Ã (U+00C3) is a LEGITIMATE Vietnamese character, NOT mojibake.
#
# Usage: powershell -ExecutionPolicy Bypass -File scripts/check-encoding.ps1
# Exit 0 = PASS (no mojibake), Exit 1 = FAIL (mojibake found)

param(
    [string[]]$Extensions = @('.cs', '.razor'),
    [string[]]$ExcludePaths = @('\bin\', '\obj\', '\.vs\', '\node_modules\')
)

$repoRoot = Split-Path -Parent $PSScriptRoot
Write-Host "Wave 8 Encoding Lint: scanning for mojibake in $($Extensions -join ', ') files..." -ForegroundColor Cyan

# True mojibake signature: a 2-char sequence where
#   char1 is a UTF-8 lead byte mis-decoded as Latin-1 (U+00C2-U+00DF), AND
#   char2 is a UTF-8 continuation byte mis-decoded as Latin-1 (U+0080-U+00BF).
# Also flag the Unicode replacement character U+FFFD (decoding failure marker).
# A single Latin-1 char (e.g. Ó U+00D3, Ã U+00C3, Â U+00C2) is NOT mojibake —
# it is a legitimate precomposed Vietnamese character.
$leadRange = [char]0x00C2..[char]0x00DF
$contRange = [char]0x0080..[char]0x00BF
$replacementChar = [char]0xFFFD

$files = Get-ChildItem -Path $repoRoot -Recurse -File |
    Where-Object {
        $Extensions -contains $_.Extension -and
        -not ($ExcludePaths | Where-Object { $_.FullName -like "*$_*" })
    }

$violations = @()
foreach ($file in $files) {
    $content = Get-Content -Path $file.FullName -Raw -Encoding UTF8
    if ($null -eq $content) { continue }

    $chars = $content.ToCharArray()
    $relPath = $file.FullName.Replace($repoRoot, '').TrimStart('\', '/')

    # Scan for 2-char mojibake sequences (lead + continuation).
    for ($i = 0; $i -lt $chars.Length - 1; $i++) {
        $c1 = [int]$chars[$i]
        $c2 = [int]$chars[$i + 1]
        if ($leadRange -contains $c1 -and $contRange -contains $c2) {
            $violations += [PSCustomObject]@{
                File = $relPath
                Line = ($content.Substring(0, $i) -split "`n").Length
                Pattern = ('U+{0:X4} U+{1:X4}' -f $c1, $c2)
            }
            $i++  # skip continuation char to avoid overlapping matches
        }
    }

    # Also flag standalone Unicode replacement characters.
    for ($i = 0; $i -lt $chars.Length; $i++) {
        if ([int]$chars[$i] -eq 0xFFFD) {
            $violations += [PSCustomObject]@{
                File = $relPath
                Line = ($content.Substring(0, $i) -split "`n").Length
                Pattern = 'U+FFFD (replacement char)'
            }
        }
    }
}

if ($violations.Count -gt 0) {
    Write-Host ""
    Write-Host "[FAIL] Mojibake detected in $($violations.Count) location(s):" -ForegroundColor Red
    $violations |
        Group-Object File |
        ForEach-Object {
            Write-Host "  $($_.Name)" -ForegroundColor Yellow
            $_.Group | Select-Object -First 5 | ForEach-Object {
                Write-Host "    line $($_.Line): $($_.Pattern)" -ForegroundColor DarkYellow
            }
            if ($_.Group.Count -gt 5) {
                Write-Host "    ... and $($_.Group.Count - 5) more" -ForegroundColor DarkGray
            }
        }
    Write-Host ""
    Write-Host "Fix: Re-save the affected files as UTF-8 (without mojibake)." -ForegroundColor Cyan
    exit 1
}

Write-Host "[PASS] No mojibake detected in $($files.Count) files." -ForegroundColor Green
exit 0
