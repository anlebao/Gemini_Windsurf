#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Test matrix for deployment-impact.yml + scripts/impact-analysis.ps1 (P0 Safety Foundation).
    Simulated scenarios prove dependency detection does NOT miss anything.
    Includes the ADVERSARIAL tests required for production sign-off:
      - Shared/CoreHub change must NEVER be scoped to a single app (BLOCK).
    Run:  pwsh scripts/tests/impact-analysis.tests.ps1
    Exit 0 = all pass.  Exit 1 = failures.
#>

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptDir "..\..")).Path
$impactScript = Join-Path $repoRoot "scripts\impact-analysis.ps1"
$configPath = Join-Path $repoRoot "deployment-impact.yml"

# Dot-source: exposes Invoke-ImpactAnalysis + ConvertTo-GlobRegex + ConvertFrom-ImpactYaml
. $impactScript

$script:PassCount = 0
$script:FailCount = 0
$script:FailDetails = [System.Collections.Generic.List[string]]::new()

function Write-Result($name, $ok, $detail) {
    if ($ok) {
        $script:PassCount++
        Write-Host "  [PASS] $name" -ForegroundColor Green
    } else {
        $script:FailCount++
        $script:FailDetails.Add("$name : $detail")
        Write-Host "  [FAIL] $name : $detail" -ForegroundColor Red
    }
}

function Assert-Equal($name, $actual, $expected) {
    $a = @($actual) -join ','
    $e = @($expected) -join ','
    Write-Result $name ($a -ceq $e) "expected [$e] got [$a]"
}

function Assert-Set($name, $actual, $expected) {
    $a = @($actual | Sort-Object) -join ','
    $e = @($expected | Sort-Object) -join ','
    Write-Result $name ($a -ceq $e) "expected set [$e] got [$a]"
}

function Assert-True($name, $cond, $detail) {
    Write-Result $name ([bool]$cond) $detail
}

function Analyze($files, $scope = "", $forceFull = $false) {
    return Invoke-ImpactAnalysis -ChangedFiles @($files) -ConfigPath $configPath `
        -RequestedScope $scope -ForceFull:$forceFull
}

function Test-Throws($name, [scriptblock]$block, $detail) {
    try {
        & $block
        Write-Result $name $false "expected exception but none thrown: $detail"
    } catch {
        Write-Result $name $true "throws as expected: $($_.Exception.Message)"
    }
}

# ============================================================
# A. GLOB MATCHER UNIT TESTS
# ============================================================
Write-Host "`n=== A. Glob matcher ===" -ForegroundColor Cyan
Assert-True "glob: 5_WebApps/ShopERP/** matches subdir file" `
    (Test-GlobMatch "5_WebApps/ShopERP/Components/Pages/Home.razor" "5_WebApps/ShopERP/**") ""

Assert-True "glob: 5_WebApps/ShopERP/** does NOT match ShopERPx" `
    (-not (Test-GlobMatch "5_WebApps/ShopERPx/Foo.cs" "5_WebApps/ShopERP/**")) ""

Assert-True "glob: docker-compose*.yml matches root only" `
    (Test-GlobMatch "docker-compose.shoperp.yml" "docker-compose*.yml") ""

Assert-True "glob: docker-compose*.yml does NOT match 6_Testing/docker-compose.test.yml" `
    (-not (Test-GlobMatch "6_Testing/docker-compose.test.yml" "docker-compose*.yml")) ""

Assert-True "glob: nginx/** does NOT match app-local nginx.conf" `
    (-not (Test-GlobMatch "5_WebApps/KhachLink/nginx.conf" "nginx/**")) ""

Assert-True "glob: 1_Shared/** matches direct file + subdir" `
    ((Test-GlobMatch "1_Shared/Domain.cs" "1_Shared/**") -and (Test-GlobMatch "1_Shared/Domain/BaseEntity.cs" "1_Shared/**")) ""

Assert-True "glob: **/Migrations/** matches nested migrations" `
    (Test-GlobMatch "3_CoreHub/Migrations/20260926_AddX.cs" "**/Migrations/**") ""

Assert-True "glob: **/*Jwt*.cs matches any depth" `
    (Test-GlobMatch "2_Gateway/Security/JwtValidator.cs" "**/*Jwt*.cs") ""

# ============================================================
# B. CONFIG VALIDATION
# ============================================================
Write-Host "`n=== B. Config validation (safety) ===" -ForegroundColor Cyan
$tmpConfig = Join-Path $env:TEMP "impact-bad-app.yml"
@"
version: 1
apps:
  gateway:
    image: x
    paths: [2_Gateway/**]
dependencies:
  - path: 1_Shared/**
    affects: [nonexistent-app]
unknown: full
"@ | Set-Content -Path $tmpConfig -Encoding utf8
Test-Throws "config: dependency affects unknown app -> throws" {
    Invoke-ImpactAnalysis -ChangedFiles @("1_Shared/Domain.cs") -ConfigPath $tmpConfig
} "unknown app in affects must be rejected"
Remove-Item $tmpConfig -Force

$tmpConfig2 = Join-Path $env:TEMP "impact-bad-unknown.yml"
@"
version: 1
apps:
  gateway:
    image: x
    paths: [2_Gateway/**]
dependencies: []
ops_paths: []
no_deploy_paths: []
unknown: scoped
"@ | Set-Content -Path $tmpConfig2 -Encoding utf8
Test-Throws "config: unknown != full -> throws" {
    Invoke-ImpactAnalysis -ChangedFiles @("2_Gateway/Foo.cs") -ConfigPath $tmpConfig2
} "unknown must be 'full'"
Remove-Item $tmpConfig2 -Force

$tmpConfig3 = Join-Path $env:TEMP "impact-tab.yml"
"apps:`n`tgateway:" | Set-Content -Path $tmpConfig3 -Encoding utf8
Test-Throws "config: tab indentation -> throws" {
    Invoke-ImpactAnalysis -ChangedFiles @("x") -ConfigPath $tmpConfig3
} "tabs must be rejected"
Remove-Item $tmpConfig3 -Force

# ============================================================
# C. CLASSIFICATION SCENARIOS (simulated diffs)
# ============================================================
Write-Host "`n=== C. Classification scenarios ===" -ForegroundColor Cyan

# C1. docs-only -> NO_DEPLOY
$r = Analyze @("docs/AI/project_state.md")
Assert-Equal "C1 docs-only -> classification NO_DEPLOY" $r.classification "NO_DEPLOY"
Assert-Equal "C1 docs-only -> deploy_action noop" $r.deploy_action "noop"
Assert-Equal "C1 docs-only -> deploy_set empty" $r.deploy_set @()

# C2. 1-line ShopERP .razor -> APP shoperp only
$r = Analyze @("5_WebApps/ShopERP/Components/Pages/Home.razor")
Assert-Equal "C2 ShopERP razor -> APP" $r.classification "APP"
Assert-Set "C2 ShopERP razor -> deploy_set [shoperp]" $r.deploy_set @("shoperp")

# C3. ShopERP backend .cs -> APP shoperp (no gateway/khachlink)
$r = Analyze @("5_WebApps/ShopERP/Services/OrderService.cs")
Assert-Set "C3 ShopERP backend -> deploy_set [shoperp]" $r.deploy_set @("shoperp")

# C4. KhachLink .razor -> APP khachlink
$r = Analyze @("5_WebApps/KhachLink/Pages/Index.razor")
Assert-Set "C4 KhachLink -> deploy_set [khachlink]" $r.deploy_set @("khachlink")

# C5. Gateway controller -> APP gateway + api intent
$r = Analyze @("2_Gateway/Controllers/AuthController.cs")
Assert-Set "C5 Gateway controller -> deploy_set [gateway]" $r.deploy_set @("gateway")
Assert-True "C5 api_contract intent detected" ($r.verification_set -contains "api") "verification=$($r.verification_set -join ',')"

# C6. CoreHub service -> MULTI_APP [gateway, shoperp] - MUST NOT include khachlink
$r = Analyze @("3_CoreHub/Services/OrderWorkflowService.cs")
Assert-Equal "C6 CoreHub -> MULTI_APP" $r.classification "MULTI_APP"
Assert-Set "C6 CoreHub -> deploy_set [gateway, shoperp]" $r.deploy_set @("gateway", "shoperp")

# C7. CRITICAL: 1_Shared/Domain.cs -> ALL 4 apps
$r = Analyze @("1_Shared/Domain.cs")
Assert-Equal "C7 1_Shared/Domain.cs -> MULTI_APP" $r.classification "MULTI_APP"
Assert-Set "C7 1_Shared/Domain.cs -> deploy_set [gateway,khachlink,shoperp,directory]" $r.deploy_set @("gateway", "khachlink", "shoperp", "directory")

# C8. UI.Platform component -> [khachlink, shoperp, directory] - NOT gateway
$r = Analyze @("UI.Platform/Components/VanAnButton.razor")
Assert-Set "C8 UI.Platform -> [khachlink,shoperp,directory]" $r.deploy_set @("khachlink", "shoperp", "directory")

# C9. docker-compose change -> OPS -> full
$r = Analyze @("docker-compose.shoperp.yml")
Assert-Equal "C9 compose -> OPS" $r.classification "OPS"
Assert-Equal "C9 compose -> deploy_action full" $r.deploy_action "full"
Assert-Set "C9 compose -> all 4 apps" $r.deploy_set @("gateway", "khachlink", "shoperp", "directory")

# C10. nginx/nginx.conf -> OPS -> full
$r = Analyze @("nginx/nginx.conf")
Assert-Equal "C10 nginx -> OPS full" $r.deploy_action "full"

# C11. .env template -> OPS -> full
$r = Analyze @(".env.shoperp")
Assert-Equal "C11 .env.shoperp -> OPS" $r.classification "OPS"

# C12. UNKNOWN new module -> FULL (safety valve)
$r = Analyze @("9_NewModule/Engine.cs")
Assert-Equal "C12 unknown path -> UNKNOWN" $r.classification "UNKNOWN"
Assert-Equal "C12 unknown path -> deploy_action full" $r.deploy_action "full"
Assert-Equal "C12 unknown path recorded" $r.unknown_paths @("9_NewModule/Engine.cs")

# C13. VanAn.Accounting change -> MULTI_APP [gateway, shoperp] (via CoreHub analyzer chain)
$r = Analyze @("VanAn.Accounting/Domain/Entry.cs")
Assert-Equal "C13 Accounting -> MULTI_APP" $r.classification "MULTI_APP"
Assert-Set "C13 Accounting -> [gateway, shoperp]" $r.deploy_set @("gateway", "shoperp")

# C14. Directory change -> APP directory
$r = Analyze @("5_WebApps/Directory/Pages/Index.razor")
Assert-Set "C14 Directory -> [directory]" $r.deploy_set @("directory")

# C15. VanAn.sln -> OPS -> full
$r = Analyze @("VanAn.sln")
Assert-Equal "C15 sln -> OPS full" $r.deploy_action "full"

# C16. 6_Tests change -> NO_DEPLOY but needs_ci
$r = Analyze @("6_Tests/VanAn.Core.Tests/FooTests.cs")
Assert-Equal "C16 tests -> NO_DEPLOY" $r.classification "NO_DEPLOY"
Assert-True "C16 tests -> needs_ci" $r.needs_ci "needs_ci should be true"

# C17. App-local Dockerfile -> APP only (NOT ops/full)
$r = Analyze @("2_Gateway/Dockerfile")
Assert-Set "C17 gateway Dockerfile -> [gateway]" $r.deploy_set @("gateway")

# C18. DB migration in CoreHub -> MULTI_APP + migration intent
$r = Analyze @("3_CoreHub/Migrations/20260926_AddX.cs")
Assert-True "C18 migration intent detected" ($r.verification_set -contains "migration") "verification=$($r.verification_set -join ',')"

# C19. KhachLink local nginx.conf -> APP khachlink (NOT ops)
$r = Analyze @("5_WebApps/KhachLink/nginx.conf")
Assert-Set "C19 khachlink nginx.conf -> [khachlink]" $r.deploy_set @("khachlink")

# C20. scripts/ change -> OPS -> full
$r = Analyze @("scripts/deploy-shoperp.sh")
Assert-Equal "C20 scripts -> OPS full" $r.deploy_action "full"

# C21. Multi-file union: ShopERP + KhachLink -> both, scoped OK
$r = Analyze @("5_WebApps/ShopERP/Foo.cs", "5_WebApps/KhachLink/Bar.razor")
Assert-Set "C21 union -> [khachlink, shoperp]" $r.deploy_set @("khachlink", "shoperp")
$r2 = Analyze @("5_WebApps/ShopERP/Foo.cs", "5_WebApps/KhachLink/Bar.razor") "shoperp,khachlink"
Assert-True "C21 union within scope -> not blocked" (-not $r2.blocked) $r2.block_reason

# C22. .github/workflows change -> OPS -> full
$r = Analyze @(".github/workflows/cd-multivps.yml")
Assert-Equal "C22 workflow -> OPS full" $r.deploy_action "full"

# C23. 4_MobileApps -> NO_DEPLOY (not in sln)
$r = Analyze @("4_MobileApps/HRApp/Program.cs")
Assert-Equal "C23 mobile -> NO_DEPLOY" $r.classification "NO_DEPLOY"

# C24. UI intent: wwwroot/js change -> targeted_e2e in verification
$r = Analyze @("5_WebApps/ShopERP/wwwroot/js/realtime.js")
Assert-True "C24 ui intent -> targeted_e2e" ($r.verification_set -contains "targeted_e2e") "verification=$($r.verification_set -join ',')"

# C25. auth intent: gateway Jwt file
$r = Analyze @("2_Gateway/Security/JwtValidator.cs")
Assert-True "C25 auth intent -> auth_e2e" ($r.verification_set -contains "auth_e2e") "verification=$($r.verification_set -join ',')"

# ============================================================
# D. ADVERSARIAL SAFETY TESTS (production sign-off gate)
# ============================================================
Write-Host "`n=== D. Adversarial safety (no-downgrade) ===" -ForegroundColor Cyan

# D1. Shared change + RequestedScope=shoperp -> MUST BLOCK
$r = Analyze @("1_Shared/Domain.cs") "shoperp"
Assert-True "D1 Shared + scope=shoperp -> BLOCKED" $r.blocked $r.block_reason
Assert-Equal "D1 block_reason mentions missing apps" ($r.block_reason -match "gateway|khachlink|directory") $true

# D2. CoreHub change + RequestedScope=khachlink -> MUST BLOCK
$r = Analyze @("3_CoreHub/Services/OrderWorkflowService.cs") "khachlink"
Assert-True "D2 CoreHub + scope=khachlink -> BLOCKED" $r.blocked $r.block_reason

# D3. OPS change + RequestedScope=shoperp -> MUST BLOCK
$r = Analyze @("docker-compose.shoperp.yml") "shoperp"
Assert-True "D3 OPS + scope=shoperp -> BLOCKED" $r.blocked $r.block_reason

# D4. UNKNOWN + RequestedScope=shoperp -> MUST BLOCK
$r = Analyze @("9_NewModule/Engine.cs") "shoperp"
Assert-True "D4 UNKNOWN + scope=shoperp -> BLOCKED" $r.blocked $r.block_reason

# D5. Correct scope (CoreHub -> gateway,shoperp) -> NOT blocked
$r = Analyze @("3_CoreHub/Services/OrderWorkflowService.cs") "gateway,shoperp"
Assert-True "D5 CoreHub + scope=gateway,shoperp -> allowed" (-not $r.blocked) $r.block_reason

# D6. ForceFull escalation always allowed even for tiny change
$r = Analyze @("5_WebApps/ShopERP/Foo.cs") "" $true
Assert-True "D6 ForceFull -> deploy_action full" ($r.deploy_action -eq "full") $r.deploy_action
Assert-Set "D6 ForceFull -> all 4 apps" $r.deploy_set @("gateway", "khachlink", "shoperp", "directory")

# D7. ForceFull + wrong scope still BLOCKED (escalation cannot bypass scope guard)
$r = Analyze @("5_WebApps/ShopERP/Foo.cs") "shoperp" $true
Assert-True "D7 ForceFull + scope=shoperp -> BLOCKED (scope guard wins)" $r.blocked $r.block_reason

# ============================================================
# SUMMARY
# ============================================================
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host " RESULT: $($script:PassCount) PASS / $($script:FailCount) FAIL" -ForegroundColor $(if ($script:FailCount -eq 0) { "Green" } else { "Red" })
if ($script:FailCount -gt 0) {
    Write-Host " Failures:" -ForegroundColor Red
    foreach ($f in $script:FailDetails) { Write-Host "   - $f" -ForegroundColor Red }
    exit 1
}
exit 0
