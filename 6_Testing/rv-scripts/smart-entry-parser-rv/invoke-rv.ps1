# RV Smart Entry parser - local driver (Windows).
# Builds harness (temp csproj - no committed csproj), scp to VPS, run inside
# shoperp container, fetch report back.
#
#   pwsh invoke-rv.ps1          # build + deploy + run on vanan-shop-a
#   pwsh invoke-rv.ps1 -SkipBuild
#   pwsh invoke-rv.ps1 -Local   # run vs locally-built 3_CoreHub dll
#   pwsh invoke-rv.ps1 -Vps <name> -Zone <z> -GcpProject <p>
#
# Requires: .NET 8 SDK + gcloud SDK authed to project vanan-prod.
param(
    [string]$Vps         = "vanan-shop-a",
    [string]$Zone        = "asia-southeast1-b",
    [string]$GcpProject  = "vanan-prod",
    [switch]$SkipBuild,
    [switch]$Local
)

$ErrorActionPreference = "Stop"
$dir   = $PSScriptRoot
$dist  = Join-Path $dir "dist"

if (-not $SkipBuild) {
    Write-Host "== build harness (temp csproj) =="
    $tmp = Join-Path $env:TEMP "rv-smart-entry-build"
    New-Item -ItemType Directory -Force $tmp | Out-Null
    $src = (Join-Path $dir "RvSmartEntryParser.cs") -replace '\\', '/'
    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AssemblyName>RvSmartEntryParser</AssemblyName>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$src" />
  </ItemGroup>
</Project>
"@ | Set-Content -Encoding UTF8 "$tmp\rv.csproj"
    dotnet publish "$tmp\rv.csproj" -c Release -o "$tmp\out" --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
    New-Item -ItemType Directory -Force $dist | Out-Null
    Copy-Item "$tmp\out\RvSmartEntryParser.dll" "$dist\"
    Copy-Item "$tmp\out\RvSmartEntryParser.runtimeconfig.json" "$dist\"
    Write-Host "built -> $dist"
}

if ($Local) {
    $dll = Join-Path $dir "..\..\..\3_CoreHub\bin\Release\net8.0\VanAn.CoreHub.dll"
    if (-not (Test-Path $dll)) { $dll = Join-Path $dir "..\..\..\3_CoreHub\bin\Debug\net8.0\VanAn.CoreHub.dll" }
    $dll = (Resolve-Path $dll).Path
    Write-Host "== LOCAL run vs $dll =="
    dotnet "$dist\RvSmartEntryParser.dll" $dll (Join-Path $dir "cases.json") (Join-Path $dir "rv-smart-entry-report.json")
    exit $LASTEXITCODE
}

Write-Host "== deploy to $Vps ($Zone) =="
$stage = Join-Path $env:TEMP "rv-se-stage"
New-Item -ItemType Directory -Force $stage | Out-Null
Copy-Item "$dist\*" $stage
Copy-Item (Join-Path $dir "cases.json") $stage
$sh = (Get-Content (Join-Path $dir "run-on-vps.sh") -Raw) -replace "`r`n", "`n"
[System.IO.File]::WriteAllText((Join-Path $stage "run-on-vps.sh"), $sh)

gcloud compute ssh $Vps --zone $Zone --project $GcpProject --command "mkdir -p /tmp/rv-se-src && rm -f /tmp/rv-se-src/*"
if ($LASTEXITCODE -ne 0) { throw "ssh mkdir failed" }
gcloud compute scp "$stage\*" "${Vps}:/tmp/rv-se-src/" --zone $Zone --project $GcpProject
if ($LASTEXITCODE -ne 0) { throw "scp failed" }

Write-Host "== run on VPS =="
gcloud compute ssh $Vps --zone $Zone --project $GcpProject --command "bash /tmp/rv-se-src/run-on-vps.sh"
$rc = $LASTEXITCODE

gcloud compute scp "${Vps}:/tmp/rv-se-src/rv-smart-entry-report.json" (Join-Path $dir "rv-smart-entry-report.json") --zone $Zone --project $GcpProject 2>$null
Write-Host "== done (exit $rc) - report: $dir\rv-smart-entry-report.json =="
exit $rc
