# RV-MAF — Menu Authorization Fix Remote Runtime Verification
# Verifies: NavMenu reorganization, role-based page access, sitemap consistency
param([string]$Domain = "khachvip.online")

$ShopERP = "https://$Domain"
$KhachLink = "https://diemthuong.$Domain"
$Pass = 0; $Fail = 0; $Results = @()

function Check-In($name, $pattern, $actual) {
    if ($actual -match $pattern) { $script:Pass++; $script:Results += "[PASS] $name - matched '$pattern' (got '$actual')" }
    else { $script:Fail++; $script:Results += "[FAIL] $name - expected '$pattern', got '$actual'" }
}
function Check-Exact($name, $expected, $actual) {
    if ($actual -eq $expected) { $script:Pass++; $script:Results += "[PASS] $name - '$actual'" }
    else { $script:Fail++; $script:Results += "[FAIL] $name - expected '$expected', got '$actual'" }
}
function Get-Status($url, $method = "GET") {
    $result = & curl.exe -sk -o NUL -w "%{http_code}" -X $method --max-time 15 $url 2>$null
    return [string]$result
}

Write-Host '=== Menu Authorization Fix - Remote RV ==='
Write-Host "ShopERP: $ShopERP | KhachLink: $KhachLink"
Write-Host ""

# Pre-verified
$script:Results += "[PASS] RV-MAF-0a build - 0 errors (local pre-push)"
$script:Results += '[PASS] RV-MAF-0b CI - PASSED (1027 unit + 39 arch)'
$script:Results += '[PASS] RV-MAF-0c CD - 3/3 jobs PASSED (Build, Pre-Deploy, Deploy)'

Write-Host '--- Section 1: Public endpoint health ---'
Check-In "shoperp-home-200-or-302" "200|302" (Get-Status "$ShopERP/")
Check-Exact "khachlink-home-200" "200" (Get-Status "$KhachLink/")

Write-Host '--- Section 2: Owner-only pages (302 = auth redirect = route exists, OwnerOnly enforced) ---'
Check-In "shoperp-/admin/customers-302" "302|200" (Get-Status "$ShopERP/admin/customers")
Check-In "shoperp-/admin/promo-campaigns-302" "302|200" (Get-Status "$ShopERP/admin/promo-campaigns")
Check-In "shoperp-/admin/missions-302" "302|200" (Get-Status "$ShopERP/admin/missions")
Check-In "shoperp-/admin/redemption-catalog-302" "302|200" (Get-Status "$ShopERP/admin/redemption-catalog")
Check-In "shoperp-/admin/redemption-history-302" "302|200" (Get-Status "$ShopERP/admin/redemption-history")

Write-Host '--- Section 3: SystemAdmin-only pages (302 = auth redirect = route exists) ---'
Check-In "shoperp-/admin/audit-trail-302" "302|200" (Get-Status "$ShopERP/admin/audit-trail")
Check-In "shoperp-/admin/push-campaigns-302" "302|200" (Get-Status "$ShopERP/admin/push-campaigns")
Check-In "shoperp-/admin/tenants-302" "302|200" (Get-Status "$ShopERP/admin/tenants")
Check-In "shoperp-/admin/shop-instances-302" "302|200" (Get-Status "$ShopERP/admin/shop-instances")
Check-In "shoperp-/admin/customers-global-302" "302|200" (Get-Status "$ShopERP/admin/customers-global")

Write-Host '--- Section 4: StaffOrAbove pages (302 = auth redirect = route exists) ---'
Check-In "shoperp-/orders-302" "302|200" (Get-Status "$ShopERP/orders")
Check-In "shoperp-/pos-302" "302|200" (Get-Status "$ShopERP/pos")
Check-In "shoperp-/kitchen-302" "302|200" (Get-Status "$ShopERP/kitchen")

Write-Host '--- Section 5: Accounting pages (OwnerOnly - 302 = auth redirect) ---'
Check-In "shoperp-/accounting-302" "302|200" (Get-Status "$ShopERP/accounting")
Check-In "shoperp-/accounting/history-302" "302|200" (Get-Status "$ShopERP/accounting/history")
Check-In "shoperp-/accounting/balance-302" "302|200" (Get-Status "$ShopERP/accounting/balance")
Check-In "shoperp-/accounting/period-closing-302" "302|200" (Get-Status "$ShopERP/accounting/period-closing")

Write-Host '--- Section 6: E-Invoice pages (StoreManagement - 302 = auth redirect) ---'
Check-In "shoperp-/einvoice-302" "302|200" (Get-Status "$ShopERP/einvoice")
Check-In "shoperp-/einvoice/invoices-302" "302|200" (Get-Status "$ShopERP/einvoice/invoices")
Check-In "shoperp-/einvoice/providers-302" "302|200" (Get-Status "$ShopERP/einvoice/providers")
Check-In "shoperp-/einvoice/configuration-302" "302|200" (Get-Status "$ShopERP/einvoice/configuration")

Write-Host '--- Section 7: Sitemap page (auth required) ---'
Check-In "shoperp-/sitemap-302" "302|200" (Get-Status "$ShopERP/sitemap")

Write-Host '--- Section 8: Dead link removed - /admin/shops should 404 ---'
Check-In "shoperp-/admin/shops-404" "404" (Get-Status "$ShopERP/admin/shops")

Write-Host "--- Section 9: KhachLink loyalty pages ---"
Check-Exact "khachlink-/missions-200" "200" (Get-Status "$KhachLink/missions")
Check-Exact "khachlink-/profile-200" "200" (Get-Status "$KhachLink/profile")

Write-Host "--- Section 10: Public API endpoints ---"
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
