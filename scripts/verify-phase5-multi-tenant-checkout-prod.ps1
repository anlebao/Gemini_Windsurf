# verify-phase5-multi-tenant-checkout-prod.ps1
# Phase 5 Runtime Verification - Multi-tenant checkout + QR with prices + Price validation
#
# Tests:
#   RV1: Gateway health
#   RV2: Multi-tenant checkout (CheckoutResponse shape with orders[])
#   RV3: New QR code endpoint returns non-empty PNG
#   RV4: ValidateProductPrice endpoint returns match=true for correct price
#   RV5: ValidateProductPrice endpoint returns match=false for stale price
#   RV6: Price_Validation_Enabled toggle readable via API
#   RV7: KhachLink order tracking UI loads

$ErrorActionPreference = "Continue"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$GatewayUrl = "https://api.khachvip.online"
$ShopERPUrl = "https://api.khachvip.online/shoperp"
$KhachLinkUrl = "https://diemthuong.khachvip.online"
$TenantId = "00000000-0000-0000-0000-000000000001"
$pass = 0; $fail = 0; $skip = 0

# VPS (khachvip.online) has valid certs - no cert override needed

function Test-Result($name, $condition, $evidence) {
    if ($condition) {
        Write-Host "[PASS] $name - $evidence" -ForegroundColor Green
        $script:pass++
    } else {
        Write-Host "[FAIL] $name - $evidence" -ForegroundColor Red
        $script:fail++
    }
}

Write-Host "========================================"
Write-Host "  Phase 5 Runtime Verification (PROD)"
Write-Host "  Multi-tenant Checkout + QR with Prices"
Write-Host "========================================"
Write-Host "  Gateway:    $GatewayUrl"
Write-Host "  ShopERP:    $ShopERPUrl"
Write-Host "  KhachLink:  $KhachLinkUrl"
Write-Host "  Tenant:     $TenantId"
Write-Host ""

# ─── Login as sysadmin + impersonate ──────────────────────────────────
Write-Host "[Setup] Logging in as sysadmin + impersonating tenant..."
$session = $null
try {
    $loginBody = @{Username = 'sysadmin@vanan.vn'; Password = '2026@vanan'} | ConvertTo-Json -Compress
    $loginResp = Invoke-WebRequest -Uri "$ShopERPUrl/api/platform/login" -Method POST -Body $loginBody -ContentType "application/json" -SessionVariable session -UseBasicParsing
    $impResp = Invoke-WebRequest -Uri "$ShopERPUrl/api/admin/impersonate/$TenantId" -Method POST -WebSession $session -UseBasicParsing
    Write-Host "  Login OK (status: $($loginResp.StatusCode))"
} catch {
    Write-Host "  WARN: Login failed: $($_.Exception.Message)"
}

# ─── RV1: Gateway health ──────────────────────────────────────────────
Write-Host ""
Write-Host "[RV1] Gateway health check..."
try {
    $healthResp = Invoke-RestMethod -Uri "$GatewayUrl/health" -UseBasicParsing
    Test-Result "RV1: Gateway health" ($healthResp.status -eq "Healthy") "status=$($healthResp.status)"
} catch {
    Test-Result "RV1: Gateway health" $false "error: $($_.Exception.Message)"
}

# ─── Get a product for subsequent tests ───────────────────────────────
Write-Host ""
Write-Host "[Pre-flight] Fetching product from VPS..."
$product = $null
try {
    $prods = Invoke-RestMethod -Uri "$ShopERPUrl/api/products" -WebSession $session -UseBasicParsing
    if ($prods -is [array] -and $prods.Count -gt 0) {
        $product = $prods[0]
    }
} catch {
    Write-Host "  ERROR fetching products: $($_.Exception.Message)"
}

if ($product) {
    $productId = $product.productId
    $productName = $product.name
    $productPrice = $product.price
    $productVatRate = 0.10
    if ($product.vatRate) { $productVatRate = $product.vatRate }
    Write-Host "  Product: $productName (ID: $productId, Price: $productPrice, VAT: $productVatRate)"
} else {
    Write-Host "  FAIL: No products found on VPS - aborting"
    $fail++
    Write-Host ""
    Write-Host "  SUMMARY: PASS=$pass FAIL=$fail SKIP=$skip"
    exit 1
}

# ─── RV2: Multi-tenant checkout (CheckoutResponse shape) ──────────────
Write-Host ""
Write-Host "[RV2] Multi-tenant checkout - verify CheckoutResponse shape with orders[]..."
$testId = "phase5-rv-$(Get-Date -Format 'yyyyMMddHHmmss')"
$checkoutBody = @{
    CustomerDeviceId = $testId
    OrderType = 'TAKEAWAY'
    Items = @(@{
        ProductId = $productId
        TenantId = $TenantId
        ProductName = $productName
        VatRate = $productVatRate
        Quantity = 1
        UnitPrice = $productPrice
        Notes = ''
    })
    CustomerNotes = 'Phase 5 RV test'
    CustomerName = "Phase5 RV $testId"
    CustomerPhone = "09$(Get-Date -Format 'HHmmss')"
    CustomerAddress = 'RV Test Address'
} | ConvertTo-Json -Depth 5 -Compress

$firstOrderId = ""
try {
    $checkoutResp = Invoke-RestMethod -Uri "$GatewayUrl/api/public/orders/checkout" -Method POST -Body $checkoutBody -ContentType "application/json" -UseBasicParsing
    $hasOrdersArray = $checkoutResp.orders -ne $null
    $ordersCount = if ($checkoutResp.orders) { @($checkoutResp.orders).Count } else { 0 }
    $successCount = $checkoutResp.successCount
    if ($ordersCount -gt 0) { $firstOrderId = $checkoutResp.orders[0].orderId }

    Test-Result "RV2a: CheckoutResponse has orders[] array" $hasOrdersArray "orders.Count=$ordersCount"
    Test-Result "RV2b: successCount=1" ($successCount -eq 1) "successCount=$successCount"
    Test-Result "RV2c: First order has orderId" (-not [string]::IsNullOrEmpty($firstOrderId)) "orderId=$firstOrderId"
} catch {
    Test-Result "RV2: Multi-tenant checkout" $false "error: $($_.Exception.Message)"
}

# ─── RV3: New QR code endpoint returns non-empty PNG ──────────────────
Write-Host ""
Write-Host "[RV3] New QR code endpoint returns non-empty PNG..."
try {
    $qrResp = Invoke-WebRequest -Uri "$ShopERPUrl/api/products/$productId/qr?tenantId=$TenantId" -WebSession $session -UseBasicParsing
    $qrSize = $qrResp.RawContentLength
    Test-Result "RV3: QR endpoint returns non-empty PNG" ($qrSize -gt 1000) "size=$qrSize bytes, status=$($qrResp.StatusCode)"
} catch {
    Test-Result "RV3: QR endpoint" $false "error: $($_.Exception.Message)"
}

# ─── RV4: ValidateProductPrice - correct price returns match=true ─────
Write-Host ""
Write-Host "[RV4] ValidateProductPrice - correct price returns match=true..."
try {
    $amp = [char]38
    $validateUrl = "$ShopERPUrl/api/products/$productId/validate-price?unitPrice=$productPrice${amp}vatRate=$productVatRate${amp}tenantId=$TenantId"
    $validateResp = Invoke-RestMethod -Uri $validateUrl -UseBasicParsing
    Test-Result "RV4: ValidateProductPrice match=true" ($validateResp.match -eq $true) "match=$($validateResp.match), reason=$($validateResp.reason)"
} catch {
    Test-Result "RV4: ValidateProductPrice" $false "error: $($_.Exception.Message)"
}

# ─── RV5: ValidateProductPrice - stale price returns match=false ──────
Write-Host ""
Write-Host "[RV5] ValidateProductPrice - stale price returns match=false..."
$stalePrice = [decimal]$productPrice + 99999
try {
    $amp = [char]38
    $staleUrl = "$ShopERPUrl/api/products/$productId/validate-price?unitPrice=$stalePrice${amp}vatRate=$productVatRate${amp}tenantId=$TenantId"
    $staleResp = Invoke-RestMethod -Uri $staleUrl -UseBasicParsing
    Test-Result "RV5: ValidateProductPrice match=false for stale price" ($staleResp.match -eq $false) "match=$($staleResp.match), reason=$($staleResp.reason), currentPrice=$($staleResp.currentUnitPrice)"
} catch {
    Test-Result "RV5: ValidateProductPrice stale" $false "error: $($_.Exception.Message)"
}

# ─── RV6: Price_Validation_Enabled toggle readable via API ────────────
Write-Host ""
Write-Host "[RV6] Price_Validation_Enabled toggle readable via ShopFeatureSettings API..."
try {
    $settingsResp = Invoke-RestMethod -Uri "$ShopERPUrl/api/shopsettings/features" -WebSession $session -UseBasicParsing
    $pvValue = $settingsResp.price_Validation_Enabled
    if ($null -eq $pvValue) { $pvValue = $settingsResp.Price_Validation_Enabled }
    $hasPV = ($null -ne $pvValue)
    Test-Result "RV6: Price_Validation_Enabled field present" $hasPV "value=$pvValue"
} catch {
    # Try alternate endpoint
    try {
        $settingsResp2 = Invoke-RestMethod -Uri "$ShopERPUrl/api/shopsettings" -WebSession $session -UseBasicParsing
        $pvValue2 = $settingsResp2.price_Validation_Enabled
        if ($null -eq $pvValue2) { $pvValue2 = $settingsResp2.Price_Validation_Enabled }
        $hasPV2 = ($null -ne $pvValue2)
        Test-Result "RV6: Price_Validation_Enabled (alt endpoint)" $hasPV2 "value=$pvValue2"
    } catch {
        Test-Result "RV6: Price_Validation_Enabled toggle" $false "error: $($_.Exception.Message)"
    }
}

# ─── RV7: KhachLink order tracking UI loads ───────────────────────────
Write-Host ""
Write-Host "[RV7] KhachLink order tracking UI loads..."
if ($firstOrderId) {
    try {
        $trackingResp = Invoke-WebRequest -Uri "$KhachLinkUrl/order-tracking/$firstOrderId" -UseBasicParsing
        Test-Result "RV7: KhachLink tracking page returns 200" ($trackingResp.StatusCode -eq 200) "HTTP $($trackingResp.StatusCode), contentLength=$($trackingResp.RawContentLength)"
    } catch {
        Test-Result "RV7: KhachLink tracking page" $false "error: $($_.Exception.Message)"
    }
} else {
    Test-Result "RV7: KhachLink tracking page" $false "no orderId from RV2"
}

# ─── Summary ──────────────────────────────────────────────────────────
Write-Host ""
Write-Host "========================================"
Write-Host "  SUMMARY: PASS=$pass FAIL=$fail SKIP=$skip"
Write-Host "========================================"

if ($fail -gt 0) { exit 1 } else { exit 0 }
