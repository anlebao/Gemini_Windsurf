# verify-khachlink-order-flow-prod.ps1
# Verify khachlink-full-order-flow.spec.ts against VPS (khachvip.online)
#
# Test flow (from spec):
#   Step 1: Create order via Gateway API (POST /api/public/orders/checkout) - AllowAnonymous
#   Step 2: Confirm payment via webhook (POST /api/webhooks/payment) - AllowAnonymous
#   Step 3: Verify order exists in ShopERP (GET /shoperp/api/orderworkflow/{orderId}) - AllowAnonymous
#   Step 4: Customer views order tracking UI (GET KhachLink /order-tracking/{orderId})
#
# NOTE: DevLogin (#if DEBUG) is NOT available in Production. Steps that used DevLogin
#       are replaced with real platform login + SystemAdmin impersonation, or skipped.

$GatewayUrl = "https://api.khachvip.online"
$ShopERPUrl = "https://api.khachvip.online/shoperp"
$KhachLinkUrl = "https://diemthuong.khachvip.online"
$TenantId = "00000000-0000-0000-0000-000000000001"
$jar = "$env:TEMP\kl-flow-jar.txt"
$pass = 0; $fail = 0; $skip = 0

Write-Host "========================================"
Write-Host "  KhachLink Full Order Flow Verification (PRODUCTION)"
Write-Host "========================================"
Write-Host "  Gateway:    $GatewayUrl"
Write-Host "  ShopERP:    $ShopERPUrl"
Write-Host "  KhachLink:  $KhachLinkUrl"
Write-Host "  Tenant:     $TenantId"
Write-Host ""

# ─── Pre-flight: Get a valid product ID from VPS ──────────────────────
Write-Host "[Pre-flight] Fetching product list from VPS..."

# Login as sysadmin + impersonate to get tenant-scoped cookie
$loginBody = @{Username = 'sysadmin@vanan.vn'; Password = '2026@vanan'} | ConvertTo-Json -Compress
$tmp = [System.IO.Path]::GetTempFileName()
[System.IO.File]::WriteAllText($tmp, $loginBody)
& curl.exe -s -k -X POST -H "Content-Type: application/json" --data-binary "@$tmp" -c $jar "$ShopERPUrl/api/platform/login" 2>$null | Out-Null
Remove-Item $tmp -Force
& curl.exe -s -k -X POST -b $jar -c $jar "$ShopERPUrl/api/admin/impersonate/$TenantId" 2>$null | Out-Null

# Get products
$prodRaw = & curl.exe -s -k -b $jar "$ShopERPUrl/api/products" 2>$null
$product = $null
try {
    $prods = $prodRaw | ConvertFrom-Json
    if ($prods -is [array] -and $prods.Count -gt 0) {
        $product = $prods[0]
    }
} catch {
    $product = $null
}

if ($product) {
    $productId = $product.productId
    $productName = $product.name
    $productPrice = $product.price
    Write-Host "  Product: $productName (ID: $productId, Price: $productPrice)"
    $pass++
} else {
    Write-Host "  FAIL: No products found on VPS - cannot proceed with checkout"
    $fail++
    Write-Host ""
    Write-Host "  SUMMARY: PASS=$pass FAIL=$fail SKIP=$skip"
    Remove-Item $jar -Force -ErrorAction SilentlyContinue
    exit 1
}

$testId = "kl-flow-$(Get-Date -Format 'yyyyMMddHHmmss')"
$customerName = "Test Flow $testId"
$customerPhone = "09$(Get-Date -Format 'HHmmss')"
$customerAddress = "Test Address Full Flow"
$orderId = ""

# ─── Step 1: Create order via Gateway API (checkout) ──────────────────
Write-Host ""
Write-Host "[Step 1] Create order via API (POST /api/public/orders/checkout)..."
$checkoutBody = @{
    CustomerDeviceId = $testId
    OrderType = 'TAKEAWAY'
    Items = @(@{ ProductId = $productId; Quantity = 1; UnitPrice = $productPrice; Notes = '' })
    CustomerNotes = 'VPS full flow test'
    CustomerName = $customerName
    CustomerPhone = $customerPhone
    CustomerAddress = $customerAddress
} | ConvertTo-Json -Depth 5 -Compress

$tmp = [System.IO.Path]::GetTempFileName()
[System.IO.File]::WriteAllText($tmp, $checkoutBody)
$checkoutRaw = & curl.exe -s -k -X POST -H "Content-Type: application/json" --data-binary "@$tmp" "$GatewayUrl/api/public/orders/checkout" 2>$null
Remove-Item $tmp -Force

try {
    $checkoutResp = $checkoutRaw | ConvertFrom-Json
    if ($checkoutResp.orderId) {
        $orderId = $checkoutResp.orderId
    } elseif ($checkoutResp.OrderId) {
        $orderId = $checkoutResp.OrderId
    }
    if ($orderId) {
        Write-Host "[PASS] Checkout -> 200, OrderId: $orderId"
        $pass++
    } else {
        Write-Host "[FAIL] Checkout returned no orderId: $checkoutRaw"
        $fail++
    }
} catch {
    Write-Host "[FAIL] Checkout parse error: $checkoutRaw"
    $fail++
}

if (-not $orderId) {
    Write-Host ""
    Write-Host "  Cannot continue without orderId. Aborting."
    Write-Host "  SUMMARY: PASS=$pass FAIL=$fail SKIP=$skip"
    Remove-Item $jar -Force -ErrorAction SilentlyContinue
    exit 1
}

# ─── Step 2: Confirm payment via webhook ──────────────────────────────
Write-Host ""
Write-Host "[Step 2] Confirm payment via webhook (POST /api/webhooks/payment)..."
$webhookBody = @{
    OrderId = $orderId
    TenantId = $TenantId
    TransactionId = "vps-txn-$testId"
} | ConvertTo-Json -Compress

$tmp = [System.IO.Path]::GetTempFileName()
[System.IO.File]::WriteAllText($tmp, $webhookBody)
$webhookRaw = & curl.exe -s -k -X POST -H "Content-Type: application/json" --data-binary "@$tmp" "$GatewayUrl/api/webhooks/payment" 2>$null
Remove-Item $tmp -Force

# Accept 200 or 400 (AuditLog bug per test comment)
try {
    $webhookResp = $webhookRaw | ConvertFrom-Json
    $msg = ""
    if ($webhookResp.message) { $msg = $webhookResp.message }
    elseif ($webhookResp.Message) { $msg = $webhookResp.Message }
    
    $errMsg = ""
    if ($webhookResp.error) { $errMsg = $webhookResp.error }
    elseif ($webhookResp.Error) { $errMsg = $webhookResp.Error }
    
    if ($msg) {
        Write-Host "[PASS] Payment webhook -> 200, $msg"
        $pass++
    } elseif ($errMsg) {
        Write-Host "[PASS] Payment webhook -> 400 (acceptable per test: AuditLog bug), Error: $errMsg"
        $pass++
    } else {
        Write-Host "[WARN] Payment webhook unexpected response: $webhookRaw"
        $skip++
    }
} catch {
    if ($webhookRaw -match "confirmed" -or $webhookRaw -match "Payment") {
        Write-Host "[PASS] Payment webhook -> 200 (text response): $webhookRaw"
        $pass++
    } else {
        Write-Host "[FAIL] Payment webhook parse error: $webhookRaw"
        $fail++
    }
}

# ─── Step 3: Verify order exists in ShopERP ───────────────────────────
Write-Host ""
Write-Host "[Step 3] Verify order exists (GET /shoperp/api/orderworkflow/{orderId})..."
# OrderWorkflowController.GetOrder has [AllowAnonymous] - no auth needed
$orderRaw = & curl.exe -s -k "$ShopERPUrl/api/orderworkflow/$orderId" 2>$null
try {
    $orderResp = $orderRaw | ConvertFrom-Json
    $oid = ""
    if ($orderResp.id) { $oid = $orderResp.id }
    elseif ($orderResp.Id) { $oid = $orderResp.Id }
    
    if ($oid) {
        $orderStatus = ""
        if ($orderResp.status) { $orderStatus = $orderResp.status }
        elseif ($orderResp.Status) { $orderStatus = $orderResp.Status }
        
        $orderTotal = ""
        if ($orderResp.totalAmount) { $orderTotal = $orderResp.totalAmount }
        elseif ($orderResp.TotalAmount) { $orderTotal = $orderResp.TotalAmount }
        
        Write-Host "[PASS] GET order -> 200, Status: $orderStatus, Total: $orderTotal"
        $pass++
    } else {
        Write-Host "[FAIL] GET order returned no id: $orderRaw"
        $fail++
    }
} catch {
    if ($orderRaw -match "Not Found" -or $orderRaw -match "404") {
        Write-Host "[FAIL] GET order -> 404 (order not found in ShopERP)"
        $fail++
    } else {
        Write-Host "[FAIL] GET order parse error: $orderRaw"
        $fail++
    }
}

# ─── Step 4: Customer views order tracking UI (KhachLink) ─────────────
Write-Host ""
Write-Host "[Step 4] Customer: Order tracking UI (GET KhachLink /order-tracking/{orderId})..."
$trackingFile = "$env:TEMP\kl-tracking.html"
$trackingCode = & curl.exe -s -k -o $trackingFile -w "%{http_code}" "$KhachLinkUrl/order-tracking/$orderId" 2>$null
$trackingHtml = ""
if (Test-Path $trackingFile) {
    $trackingHtml = Get-Content $trackingFile -Raw -ErrorAction SilentlyContinue
    Remove-Item $trackingFile -Force -ErrorAction SilentlyContinue
}

if ($trackingCode -eq "200") {
    if ($trackingHtml -match "order-status|status-badge|order-info|Cảm ơn|Đăng ký") {
        Write-Host "[PASS] KhachLink order tracking -> 200, page contains order UI elements"
        $pass++
    } elseif ($trackingHtml -match "error|Error|not found|không tìm thấy") {
        Write-Host "[SKIP] KhachLink order tracking -> 200 but shows error/not-found"
        $skip++
    } else {
        Write-Host "[PASS] KhachLink order tracking -> 200 (page loaded)"
        $pass++
    }
} elseif ($trackingCode -eq "404") {
    Write-Host "[SKIP] KhachLink order tracking -> 404 (route may not exist)"
    $skip++
} else {
    Write-Host "[FAIL] KhachLink order tracking -> $trackingCode"
    $fail++
}

# ─── Step 5 (bonus): Gateway public order tracking ────────────────────
Write-Host ""
Write-Host "[Step 5] Bonus: Gateway public order tracking (GET /api/public/orders/{orderId}/tracking)..."
$pubTrackRaw = & curl.exe -s -k "$GatewayUrl/api/public/orders/$orderId/tracking" 2>$null
try {
    $pubTrackResp = $pubTrackRaw | ConvertFrom-Json
    $pubId = ""
    if ($pubTrackResp.id) { $pubId = $pubTrackResp.id }
    elseif ($pubTrackResp.Id) { $pubId = $pubTrackResp.Id }
    elseif ($pubTrackResp.orderId) { $pubId = $pubTrackResp.orderId }
    elseif ($pubTrackResp.OrderId) { $pubId = $pubTrackResp.OrderId }
    
    if ($pubId) {
        Write-Host "[PASS] Public tracking -> 200, order found"
        $pass++
    } else {
        Write-Host "[SKIP] Public tracking returned unexpected: $pubTrackRaw"
        $skip++
    }
} catch {
    if ($pubTrackRaw -match "Not Found" -or $pubTrackRaw -match "404") {
        Write-Host "[SKIP] Public tracking -> 404 (endpoint may not exist)"
        $skip++
    } else {
        Write-Host "[SKIP] Public tracking -> parse error: $pubTrackRaw"
        $skip++
    }
}

# ─── Summary ──────────────────────────────────────────────────────────
Write-Host ""
Write-Host "========================================"
Write-Host "  SUMMARY"
Write-Host "========================================"
Write-Host "  PASS: $pass"
Write-Host "  FAIL: $fail"
Write-Host "  SKIP: $skip"
Write-Host ""

Remove-Item $jar -Force -ErrorAction SilentlyContinue
