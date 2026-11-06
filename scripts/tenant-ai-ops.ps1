# tenant-ai-ops.ps1 — P1 operator tooling for vanan-ai (SRS FR-8).
# Usage:
#   . .\scripts\tenant-ai-ops.ps1
#   Set-TenantAiEnv -BaseUrl "http://localhost:5060" -CallerId "shoperp-a" -CallerKey "…" -Secret "…" -TenantId "guid"
#   Get-AiPending | Approve-AiDoc -Id <guid> | Reject-AiDoc -Id <guid> -Reason "…" | Get-AiStatus | Sync-AiProducts
#
# Secrets: pass via -Secret param or $env:TENANTAI_CTX_SECRET — never persist values in this file.

$script:cfg = @{}

function Set-TenantAiEnv([string]$BaseUrl, [string]$CallerId, [string]$CallerKey, [string]$Secret, [string]$TenantId) {
    $script:cfg = @{
        BaseUrl  = $BaseUrl.TrimEnd('/')
        CallerId = $CallerId
        CallerKey = $CallerKey
        Secret   = if ($Secret) { $Secret } else { $env:TENANTAI_CTX_SECRET }
        TenantId = $TenantId
    }
    Write-Host "TenantAi env set → $($script:cfg.BaseUrl) caller=$CallerId tenant=$TenantId"
}

function ConvertTo-Base64Url([byte[]]$bytes) {
    [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

function Get-SignedCtx {
    $now = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $payload = @{
        tenantId  = $script:cfg.TenantId
        callerId  = $script:cfg.CallerId
        iat       = $now
        exp       = $now + 120
        requestId = [Guid]::NewGuid().ToString('N')
    } | ConvertTo-Json -Compress
    $ctx = ConvertTo-Base64Url ([Text.Encoding]::UTF8.GetBytes($payload))
    $hmac = [System.Security.Cryptography.HMACSHA256]::new([Text.Encoding]::UTF8.GetBytes($script:cfg.Secret))
    $sig = ConvertTo-Base64Url ($hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes($ctx)))
    return @{ Ctx = $ctx; Sig = $sig }
}

function Invoke-AiApi([string]$Method, [string]$Path, $Body = $null, [string]$IdemKey = $null, [hashtable]$ExtraHeaders = $null) {
    $s = Get-SignedCtx
    $headers = @{
        'X-Caller-Id'       = $script:cfg.CallerId
        'X-Ai-Key'          = $script:cfg.CallerKey
        'X-Tenant-Ctx'      = $s.Ctx
        'X-Tenant-Ctx-Sig'  = $s.Sig
    }
    if ($IdemKey) { $headers['Idempotency-Key'] = $IdemKey }
    if ($ExtraHeaders) { foreach ($k in $ExtraHeaders.Keys) { $headers[$k] = $ExtraHeaders[$k] } }
    $uri = "$($script:cfg.BaseUrl)$Path"
    $params = @{ Method = $Method; Uri = $uri; Headers = $headers; ContentType = 'application/json' }
    if ($null -ne $Body) { $params.Body = ($Body | ConvertTo-Json -Depth 10 -Compress) }
    Invoke-RestMethod @params
}

# ---------- operations ----------

function Get-AiPending      { Invoke-AiApi GET "/v1/admin/knowledge/pending" }
function Get-AiStatus       { Invoke-AiApi GET "/v1/knowledge/status" }
function Approve-AiDoc([string]$Id) { Invoke-AiApi POST "/v1/admin/knowledge/$Id/approve" }
function Reject-AiDoc([string]$Id, [string]$Reason) { Invoke-AiApi POST "/v1/admin/knowledge/$Id/reject" @{ reason = $Reason } }

function Send-AiDocument([string]$Type, [string]$Title, [string]$Content, [string]$Classification = 'public') {
    Invoke-AiApi POST "/v1/knowledge/documents" @{ type = $Type; title = $Title; content = $Content; classification = $Classification }
}

function Send-AiProduct([string]$ProductId, [string]$Name, [decimal]$Price, [bool]$Available = $true, [string]$Category = $null) {
    $item = @{ productId = $ProductId; name = $Name; price = $Price; available = $Available; categoryName = $Category }
    Invoke-AiApi POST "/v1/knowledge/products:batch" @{ fullSync = $false; sourceVersion = "ops:$(Get-Date -Format 'yyyyMMddHHmm')"; items = @($item) } -IdemKey "ops:$(Get-Date -Format 'yyyyMMddHHmmss')"
}

function Remove-AiProduct([string]$ProductId) { Invoke-AiApi DELETE "/v1/knowledge/products/$ProductId" }

# ---------- task_ai_p6b: admin API (SRS §6.3) ----------

function Get-AiProfile { Invoke-AiApi GET "/v1/admin/tenants/$($script:cfg.TenantId)/profile" }

function Put-AiProfile([string]$AiName, [hashtable]$Persona = @{}, [hashtable]$BusinessProfile = @{}) {
    Invoke-AiApi PUT "/v1/admin/tenants/$($script:cfg.TenantId)/profile" @{
        aiName             = $AiName
        personaJson        = ($Persona | ConvertTo-Json -Depth 5 -Compress)
        businessProfileJson = ($BusinessProfile | ConvertTo-Json -Depth 5 -Compress)
    }
}

function Publish-AiConfig { Invoke-AiApi POST "/v1/admin/tenants/$($script:cfg.TenantId)/publish" }

function Rollback-AiConfig([int]$Version) {
    Invoke-AiApi POST "/v1/admin/tenants/$($script:cfg.TenantId)/rollback" @{ version = $Version }
}

function Get-AiPolicy { Invoke-AiApi GET "/v1/admin/tenants/$($script:cfg.TenantId)/policy" }

function Put-AiPolicy([hashtable]$IntentScopes = @{}, [string[]]$RefusedTopics = @()) {
    Invoke-AiApi PUT "/v1/admin/tenants/$($script:cfg.TenantId)/policy" @{
        intentScopesJson   = ($IntentScopes | ConvertTo-Json -Compress)
        externalAllowJson  = '[]'   # P3 chưa build — v1 chỉ chấp nhận mảng rỗng
        refusedTopicsJson  = ($RefusedTopics | ConvertTo-Json -Compress)
    }
}

function Get-AiConversations([string]$SessionId = $null, [int]$Page = 1, [int]$Size = 20) {
    $base = "/v1/admin/tenants/$($script:cfg.TenantId)/conversations"
    if ($SessionId) { Invoke-AiApi GET "$base`?sessionId=$SessionId" }
    else { Invoke-AiApi GET "$base`?page=$Page&size=$Size" }
}

function Get-AiUsage([int]$Days = 30) {
    Invoke-AiApi GET "/v1/admin/tenants/$($script:cfg.TenantId)/usage`?days=$Days"
}

# [NFR-7] tenant purge — requires -Confirm; wipes ALL tenant data in vanan-ai.
function Remove-AiTenant([switch]$Confirm) {
    if (-not $Confirm) { Write-Error "Remove-AiTenant needs -Confirm — this deletes ALL tenant data."; return }
    Invoke-AiApi DELETE "/v1/admin/tenants/$($script:cfg.TenantId)" -ExtraHeaders @{
        'X-Confirm-Purge' = $script:cfg.TenantId
    }
}

Write-Host "tenant-ai-ops loaded — Set-TenantAiEnv first, then: Get-AiPending / Get-AiStatus / Approve-AiDoc / Reject-AiDoc / Send-AiDocument / Send-AiProduct / Remove-AiProduct / Get-AiProfile / Put-AiProfile / Publish-AiConfig / Rollback-AiConfig / Get-AiPolicy / Put-AiPolicy / Get-AiConversations / Get-AiUsage / Remove-AiTenant"
