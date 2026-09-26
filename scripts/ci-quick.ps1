#!/usr/bin/env pwsh
<#
.SYNOPSIS
    VanAn FAST pre-push check for TINY, scoped changes (L0/L1 lane).

.DESCRIPTION
    ci-quick.ps1 answers ONE question:
      "This small change does not break the layer it directly touches."

    It does NOT answer "the whole repository is still consistent" - that is
    ci-full.ps1's job (L2 lane: Shared/CoreHub/ops/architecture changes).

    CONTRACT (non-negotiable):
      - classification APP     -> build + targeted unit tests + guard-check
      - classification NO_DEPLOY-> build + guard-check (docs/tests-only edits)
      - classification MULTI_APP / OPS / UNKNOWN
                               -> REFUSED: exit 2, "RUN FULL CI" (pwsh scripts/ci-full.ps1 -SkipE2E)
      Never treat ci-quick green as license to skip full CI for wide changes.

    Usage: pwsh scripts/ci-quick.ps1 [-SkipTests]

.PARAMETER SkipTests
    Skip the unit-test phase (docs/config-only tiny changes).

.EXAMPLE
    ./scripts/ci-quick.ps1
.EXAMPLE
    ./scripts/ci-quick.ps1 -SkipTests
#>
[CmdletBinding()]
param(
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $repoRoot

# ------------------------------------------------------------
# 1. Changed files (staged + unstaged + untracked)
# ------------------------------------------------------------
$status = git status --porcelain
if ($LASTEXITCODE -ne 0) { throw "git status failed." }
$changed = @()
foreach ($line in $status) {
    $path = $line.Substring(3).Trim()
    if (-not $path) { continue }
    if ($path -match '^"(.+)"$') { $path = $Matches[1] }   # quoted (spaces/unicode)
    $changed += $path
}
if ($changed.Count -eq 0) {
    Write-Host "[ci-quick] No changes detected in working tree." -ForegroundColor Green
    exit 0
}

# ------------------------------------------------------------
# 2. Impact analysis (single source of truth: deployment-impact.yml)
# ------------------------------------------------------------
. (Join-Path $PSScriptRoot "impact-analysis.ps1")
$r = Invoke-ImpactAnalysis -ChangedFiles @($changed) -ConfigPath (Join-Path $repoRoot "deployment-impact.yml")

Write-Host "=== ci-quick impact analysis ===" -ForegroundColor Cyan
Write-Host ("  classification : $($r.classification)")
Write-Host ("  deploy_action  : $($r.deploy_action)")
Write-Host ("  deploy_set     : [$($r.deploy_set -join ', ')]")
Write-Host ("  changed files  : $($changed.Count)")

if ($r.classification -in @("MULTI_APP", "OPS", "UNKNOWN")) {
    Write-Host ""
    Write-Host "== REFUSED: change is wider than the ci-quick lane ==" -ForegroundColor Red
    Write-Host "  ci-quick covers tiny APP/NO_DEPLOY changes only." -ForegroundColor Yellow
    Write-Host "  Run FULL CI instead:  pwsh scripts/ci-full.ps1 -SkipE2E" -ForegroundColor Yellow
    exit 2
}

# ------------------------------------------------------------
# 3. Build (incremental)
# ------------------------------------------------------------
Write-Host "`n=== [1/3] Build VanAn.sln (incremental) ===" -ForegroundColor Cyan
dotnet build VanAn.sln --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ci-quick] BUILD FAILED" -ForegroundColor Red
    exit 1
}
Write-Host "[ci-quick] Build OK" -ForegroundColor Green

# ------------------------------------------------------------
# 4. Targeted unit tests
# ------------------------------------------------------------
if ($SkipTests -or $r.classification -eq "NO_DEPLOY") {
    Write-Host "`n=== [2/3] Unit tests: skipped ($($r.classification)) ===" -ForegroundColor Yellow
} else {
    Write-Host "`n=== [2/3] Targeted unit tests ===" -ForegroundColor Cyan
    $testProjects = @(
        "6_Tests\VanAn.Core.Tests\VanAn.Core.Tests.csproj",
        "6_Tests\VanAn.Unit.Tests\VanAn.Unit.Tests.csproj"
    )
    if ($r.deploy_set -contains "shoperp") {
        $testProjects += "6_Tests\VanAn.ShopERP.Tests\VanAn.ShopERP.Tests.csproj"
    }
    foreach ($proj in $testProjects) {
        Write-Host "   Running $(Split-Path -Leaf $proj)..." -ForegroundColor Gray
        dotnet test $proj --no-build --verbosity quiet `
            --filter "Category!=Performance&Category!=Integration&Category!=E2E&Category!=Flaky" `
            --logger "console;verbosity=minimal"
        if ($LASTEXITCODE -ne 0) {
            Write-Host "[ci-quick] TESTS FAILED ($proj)" -ForegroundColor Red
            exit 1
        }
    }
    Write-Host "[ci-quick] Tests OK" -ForegroundColor Green
}

# ------------------------------------------------------------
# 5. guard-check (mandatory gate)
# ------------------------------------------------------------
Write-Host "`n=== [3/3] guard-check ===" -ForegroundColor Cyan
& (Join-Path $PSScriptRoot "guard-check.ps1")
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ci-quick] GUARD-CHECK FAILED" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "[ci-quick] ALL PASSED - safe to FAST PUSH (git push --no-verify origin main)" -ForegroundColor Green
Write-Host "           Remember: this validated a $($r.classification) change only." -ForegroundColor Yellow
exit 0
