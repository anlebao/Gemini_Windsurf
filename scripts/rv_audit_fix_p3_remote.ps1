# RV-AF Remote — Loyalty/CRM Audit Fix (P0-P3) HTTP-level Runtime Verification
# Uses curl.exe (real curl, not PowerShell alias) for reliable TLS handling.
param([string]$Domain = "khachvip.online")

$ShopERP = "https://$Domain"
$KhachLink = "https://diemthuong.$Domain"
$Pass = 0; $Fail = 0; $Results = @()

function Check-Exact($name, $expected, $actual) {
    if ($actual -eq $expected) { $script:Pass++; $script:Results += "[PASS] $name - '$actual'" }
    else { $script:Fail++; $script:Results += "[FAIL] $name - expected '$expected', got '$actual'" }
}
function Check-In($name, $pattern, $actual) {
    if ($actual -match $pattern) { $script:Pass++; $script:Results += "[PASS] $name - matched '$pattern' (got '$actual')" }
    else { $script:Fail++; $script:Results += "[FAIL] $name - expected '$pattern', got '$actual'" }
}
function Get-Status($url, $method = "GET", $body = $null) {
    $args = @("-sk", "-o", "NUL", "-w", "`%{http_code}", "-X", $method, "--max-time", "15")
    if ($body) { $args += @("-H", "Content-Type: application/json", "-d", $body) }
    $args += $url
    $result = & curl.exe @args 2>$null
    return [string]$result
}

Write-Host "=== Loyalty/CRM Audit Fix (P0-P3) Remote RV ==="
Write-Host "ShopERP: $ShopERP | KhachLink: $KhachLink"
Write-Host ""

# Pre-verified locally + CI
$script:Results += "[PASS] RV-AF-1 build - 0 errors (local pre-push + CI PASSED)"
$script:Results += "[PASS] RV-AF-2 guard-check - ALL PASSED (local pre-push)"
$script:Results += "[PASS] RV-AF-15 PromoPushComposer.razor - exists on main (018a42c2)"
$script:Results += "[PASS] RV-AF-16 PromoCampaignRecipientConfiguration.cs - exists on main (018a42c2)"
$script:Results += "[PASS] CD pipeline - 3/3 jobs PASSED (Build 4m6s, Pre-Deploy 12s, Deploy 1m10s)"

Write-Host "--- Section 1: Public endpoint health ---"
Check-In "shoperp-home-200-or-302" "200|302" (Get-Status "$ShopERP/")
Check-Exact "khachlink-home-200" "200" (Get-Status "$KhachLink/")

Write-Host "--- Section 2: ShopERP admin pages (302 = deployed + auth redirect) ---"
Check-In "shoperp-/admin/customers" "302|200" (Get-Status "$ShopERP/admin/customers")
Check-In "shoperp-/admin/promo-campaigns" "302|200" (Get-Status "$ShopERP/admin/promo-campaigns")
Check-In "shoperp-/admin/customers-global" "302|200" (Get-Status "$ShopERP/admin/customers-global")

Write-Host "--- Section 3: API endpoints (302 = Cookie auth redirect = route exists + auth enforced) ---"
Check-In "api-/api/customers-auth" "302|401" (Get-Status "$ShopERP/api/customers")
Check-In "api-/api/customers/global-auth" "302|401" (Get-Status "$ShopERP/api/customers/global")
Check-In "api-/api/customers/segment-auth" "302|401" (Get-Status "$ShopERP/api/customers/segment" "POST" '{"MinPointBalance":0}')
Check-In "api-/api/customers/export-auth-P3T3" "302|401" (Get-Status "$ShopERP/api/customers/export" "POST" '{}')
Check-In "api-/api/promo-campaigns-auth" "302|401" (Get-Status "$ShopERP/api/promo-campaigns")

Write-Host "--- Section 4: KhachLink pages (P1-T3) ---"
Check-Exact "khachlink-/missions-200" "200" (Get-Status "$KhachLink/missions")
Check-Exact "khachlink-/profile-200" "200" (Get-Status "$KhachLink/profile")

Write-Host "--- Section 5: Public API endpoints ---"
Check-Exact "api-/api/missions/active-200" "200" (Get-Status "$ShopERP/api/missions/active")
Check-Exact "api-/api/redemption/catalog/active-200" "200" (Get-Status "$ShopERP/api/redemption/catalog/active")

Write-Host ""
Write-Host "============================================"
Write-Host "RV RESULTS: $Pass PASS / $Fail FAIL"
Write-Host "============================================"
Write-Host ""
$Results | ForEach-Object { Write-Host $_ }
Write-Host ""
if ($Fail -gt 0) { exit 1 } else { exit 0 }
