#Requires -Version 5.1
<#
.SYNOPSIS
    CI Build Debug Runbook — VanAn Holding ERP
    Tự động phát hiện và báo cáo các lỗi build phổ biến.

.DESCRIPTION
    Script này thực hiện TRIAGE tự động các lỗi CI build phổ biến:
    - Missing AnalyzerReleases.Unshipped.md
    - Missing/phantom base classes
    - Null-forgiving operator trên Reflection
    - Assembly version conflicts
    - Expression.Constant untyped patterns

.PARAMETER SolutionPath
    Đường dẫn tới solution file. Default: VanAn.sln

.PARAMETER Configuration
    Build configuration. Default: Release

.PARAMETER Fix
    Nếu set, tự động fix các lỗi an toàn (P2 trở xuống). Default: $false

.EXAMPLE
    # Chỉ scan, không sửa
    .\runbook.ps1

    # Scan và auto-fix lỗi an toàn
    .\runbook.ps1 -Fix

    # Chỉ build Release và report
    .\runbook.ps1 -SolutionPath "VanAn.sln" -Configuration Release
#>
param(
    [string]$SolutionPath = "VanAn.sln",
    [string]$Configuration = "Release",
    [switch]$Fix
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Continue"

# ============================================================
# HELPERS
# ============================================================
function Write-Header([string]$text) {
    Write-Host "`n===[ $text ]===" -ForegroundColor Cyan
}

function Write-Pass([string]$msg) {
    Write-Host "  [PASS] $msg" -ForegroundColor Green
}

function Write-Fail([string]$msg) {
    Write-Host "  [FAIL] $msg" -ForegroundColor Red
}

function Write-Warn([string]$msg) {
    Write-Host "  [WARN] $msg" -ForegroundColor Yellow
}

function Write-Info([string]$msg) {
    Write-Host "  [INFO] $msg" -ForegroundColor Gray
}

$findings = [System.Collections.Generic.List[PSCustomObject]]::new()

function Add-Finding([string]$priority, [string]$type, [string]$file, [string]$detail, [string]$fix = "") {
    $findings.Add([PSCustomObject]@{
        Priority = $priority
        Type     = $type
        File     = $file
        Detail   = $detail
        Fix      = $fix
    })
}

# ============================================================
# PHASE 1 — CHECK: AnalyzerReleases.Unshipped.md
# ============================================================
Write-Header "PHASE 1: Analyzer Release Files"

$shippedFiles = Get-ChildItem -Recurse -Filter "AnalyzerReleases.Shipped.md" -ErrorAction SilentlyContinue
foreach ($shipped in $shippedFiles) {
    $dir = $shipped.DirectoryName
    $unshipped = Join-Path $dir "AnalyzerReleases.Unshipped.md"
    if (-not (Test-Path $unshipped)) {
        Write-Fail "Missing: $unshipped"
        Add-Finding "P2" "MISSING_ANALYZER_FILE" $unshipped "AnalyzerReleases.Unshipped.md not found alongside Shipped.md" "New-Item -Path '$unshipped' -ItemType File -Value ''"
        if ($Fix) {
            New-Item -Path $unshipped -ItemType File -Value "" | Out-Null
            Write-Pass "AUTO-FIXED: Created $unshipped"
        }
    } else {
        Write-Pass "Found: $unshipped"
    }
}

if ($shippedFiles.Count -eq 0) {
    Write-Info "No AnalyzerReleases.Shipped.md found — skipping check."
}

# ============================================================
# PHASE 2 — CHECK: Phantom base classes
# ============================================================
Write-Header "PHASE 2: Phantom Base Classes"

$csFiles = Get-ChildItem -Recurse -Filter "*.cs" -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch "\\(bin|obj)\\" }

$baseClassPattern = [regex]":\s*(Base[A-Z][A-Za-z]+)\s*[\(,]"

foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    if (-not $content) { continue }

    $matches = $baseClassPattern.Matches($content)
    foreach ($m in $matches) {
        $baseName = $m.Groups[1].Value
        # Search if class is defined anywhere
        $defined = $csFiles | Where-Object {
            $c = Get-Content $_.FullName -Raw -ErrorAction SilentlyContinue
            $c -match "class\s+$baseName[\s<:(]"
        }
        if (-not $defined) {
            Write-Fail "$($file.Name): '$baseName' used as base class but NOT FOUND in codebase"
            Add-Finding "P0" "PHANTOM_BASE_CLASS" $file.FullName "'$baseName' not defined anywhere. Check EF DbContext, ControllerBase, etc." ""
        } else {
            Write-Pass "$($file.Name): '$baseName' found OK"
        }
    }
}

# ============================================================
# PHASE 3 — CHECK: Null-forgiving Reflection patterns
# ============================================================
Write-Header "PHASE 3: Null-forgiving Reflection Patterns"

$dangerousPatterns = @(
    @{ Pattern = 'GetMethod\([^)]+\)!'; Label = "GetMethod(...)! — null-forgiving without guard" },
    @{ Pattern = 'GetProperty\([^)]+\)!'; Label = "GetProperty(...)! — null-forgiving without guard" },
    @{ Pattern = 'Expression\.Constant\(\s*\w+\s*\)(?!\s*,)'; Label = "Expression.Constant(value) — missing explicit type argument" }
)

foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    if (-not $content) { continue }

    foreach ($pat in $dangerousPatterns) {
        if ($content -match $pat.Pattern) {
            $lineNum = ($content.Substring(0, $content.IndexOf(($content | Select-String $pat.Pattern | Select-Object -First 1).Line)) -split "`n").Count
            Write-Warn "$($file.Name): $($pat.Label)"
            Add-Finding "P1" "DANGEROUS_REFLECTION" $file.FullName $pat.Label "Replace with null-safe pattern (see SKILL.md Phase 3.3)"
        }
    }
}

# ============================================================
# PHASE 4 — BUILD: Run dotnet build
# ============================================================
Write-Header "PHASE 4: dotnet build --configuration $Configuration"

if (-not (Test-Path $SolutionPath)) {
    Write-Fail "Solution not found: $SolutionPath"
    exit 1
}

$buildOutput = dotnet build $SolutionPath --configuration $Configuration --no-restore 2>&1
$buildErrors = $buildOutput | Where-Object { $_ -match " error " -and $_ -notmatch "warning" }
$errorCount  = ($buildOutput | Select-String "^\s+\d+ Error\(s\)").ToString() -replace '\D',''
$warnCount   = ($buildOutput | Select-String "^\s+\d+ Warning\(s\)").ToString() -replace '\D',''

if ($LASTEXITCODE -eq 0) {
    Write-Pass "Build PASSED — $warnCount warning(s), 0 errors"
} else {
    Write-Fail "Build FAILED — $errorCount error(s), $warnCount warning(s)"
    foreach ($err in $buildErrors | Select-Object -First 20) {
        Write-Host "    $err" -ForegroundColor Red
        Add-Finding "P0" "BUILD_ERROR" "solution" $err ""
    }
}

# ============================================================
# PHASE 5 — REPORT
# ============================================================
Write-Header "SUMMARY REPORT"

if ($findings.Count -eq 0) {
    Write-Pass "No issues found!"
} else {
    Write-Host ""
    Write-Host "  Found $($findings.Count) issue(s):" -ForegroundColor Yellow
    Write-Host ""

    $findings | Sort-Object Priority | Format-Table Priority, Type, @{
        Label = "File/Location"
        Expression = { [System.IO.Path]::GetFileName($_.File) }
        Width = 35
    }, @{
        Label = "Detail"
        Expression = { $_.Detail.Substring(0, [Math]::Min(60, $_.Detail.Length)) }
        Width = 62
    } -AutoSize | Out-Host

    Write-Host ""
    $p0 = ($findings | Where-Object Priority -eq "P0").Count
    $p1 = ($findings | Where-Object Priority -eq "P1").Count
    $p2 = ($findings | Where-Object Priority -eq "P2").Count

    Write-Host "  P0 (Critical): $p0" -ForegroundColor $(if ($p0 -gt 0) { "Red" } else { "Green" })
    Write-Host "  P1 (High):     $p1" -ForegroundColor $(if ($p1 -gt 0) { "Yellow" } else { "Green" })
    Write-Host "  P2 (Medium):   $p2" -ForegroundColor $(if ($p2 -gt 0) { "Yellow" } else { "Green" })
    Write-Host ""

    if (-not $Fix) {
        Write-Host "  Tip: Run with -Fix to auto-fix P2 issues (safe fixes only)" -ForegroundColor Cyan
    }
}

Write-Host ""
Write-Host "Run completed. See .devin/skills/ci-build-debug/SKILL.md for full workflow." -ForegroundColor Cyan
Write-Host ""

exit $(if ($findings | Where-Object Priority -eq "P0") { 1 } else { 0 })
