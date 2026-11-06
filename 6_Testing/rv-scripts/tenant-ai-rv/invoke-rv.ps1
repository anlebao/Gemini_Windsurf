# RV Tenant-AI - local driver (Windows).
# scp run-on-vps.sh -> VPS vanan-ai -> chay -> keo report ve.
#
#   pwsh invoke-rv.ps1 -TenantId <guid>                        # tenant trong ingest scope
#   pwsh invoke-rv.ps1 -TenantId <guid> -TenantB <guid> -ZeroWalletTenant <guid>
#   pwsh invoke-rv.ps1 -TenantId <guid> -Vps <name> -Zone <z> -GcpProject <p>
#
# Requires: gcloud SDK authed to project vanan-prod.
param(
    [Parameter(Mandatory=$true)][string]$TenantId,
    [string]$TenantB           = "99999999-0000-0000-0000-000000000099",
    [string]$ZeroWalletTenant  = "",
    [string]$Vps               = "vanan-ai",
    [string]$Zone              = "asia-southeast1-b",
    [string]$GcpProject        = "vanan-prod"
)

$ErrorActionPreference = "Stop"
$dir = $PSScriptRoot

Write-Host "== deploy script to $Vps ($Zone) =="
$stage = Join-Path $env:TEMP "rv-tenantai-stage"
New-Item -ItemType Directory -Force $stage | Out-Null
$sh = (Get-Content (Join-Path $dir "run-on-vps.sh") -Raw) -replace "`r`n", "`n"
[System.IO.File]::WriteAllText((Join-Path $stage "run-on-vps.sh"), $sh)

gcloud compute ssh $Vps --zone $Zone --project $GcpProject --command "mkdir -p /tmp/rv-tenantai && rm -f /tmp/rv-tenantai/*"
if ($LASTEXITCODE -ne 0) { throw "ssh mkdir failed" }
gcloud compute scp "$stage\run-on-vps.sh" "${Vps}:/tmp/rv-tenantai/" --zone $Zone --project $GcpProject
if ($LASTEXITCODE -ne 0) { throw "scp failed" }

Write-Host "== run on VPS =="
$remote = "bash /tmp/rv-tenantai/run-on-vps.sh 2>&1 | tee /tmp/rv-tenantai/report.txt"
$envVars = "RV_TENANT_ID=$TenantId RV_TENANT_ID_B=$TenantB"
if ($ZeroWalletTenant -ne "") { $envVars += " RV_ZERO_WALLET_TENANT=$ZeroWalletTenant" }
gcloud compute ssh $Vps --zone $Zone --project $GcpProject --command "$envVars $remote"
$rc = $LASTEXITCODE

gcloud compute scp "${Vps}:/tmp/rv-tenantai/report.txt" (Join-Path $dir "tenant-ai-rv-report.txt") --zone $Zone --project $GcpProject 2>$null
Write-Host "== done (exit $rc) - report: $dir\tenant-ai-rv-report.txt =="
exit $rc
