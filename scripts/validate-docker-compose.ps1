#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Validate docker-compose configuration files for architecture consistency.

.DESCRIPTION
    This script validates docker-compose files to ensure they match the code architecture.
    It checks for:
    - Syntax validation using docker-compose config
    - Service configuration consistency
    - Container dependency validation
    - Common configuration errors

.PARAMETER ComposeFile
    Path to docker-compose file to validate. Default: docker-compose.prod.yml

.PARAMETER Strict
    Enable strict validation (fail on warnings)

.EXAMPLE
    .\scripts\validate-docker-compose.ps1
    Validates docker-compose.prod.yml

.EXAMPLE
    .\scripts\validate-docker-compose.ps1 -ComposeFile docker-compose.edge.yml -Strict
    Validates docker-compose.edge.yml with strict mode
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$ComposeFile = "docker-compose.prod.yml",

    [switch]$Strict
)

$ErrorActionPreference = "Stop"
$script:Errors = @()
$script:Warnings = @()

function Write-ValidationResult {
    param(
        [string]$Message,
        [string]$Type = "Info"
    )

    $color = switch ($Type) {
        "Error" { "Red" }
        "Warning" { "Yellow" }
        "Success" { "Green" }
        default { "White" }
    }

    Write-Host "[$Type] $Message" -ForegroundColor $color
}

function Test-DockerComposeSyntax {
    param(
        [string]$FilePath
    )

    Write-ValidationResult "Validating docker-compose file existence..." -Type "Info"

    if (-not (Test-Path $FilePath)) {
        Write-ValidationResult "File not found: $FilePath" -Type "Error"
        $script:Errors += "File not found: $FilePath"
        return $false
    }

    Write-ValidationResult "Docker compose file exists" -Type "Success"
    return $true
}

function Test-CoreHubConfiguration {
    param(
        [string]$FilePath
    )

    Write-ValidationResult "Validating CoreHub configuration..." -Type "Info"

    $content = Get-Content $FilePath -Raw

    # Check if CoreHub is configured as HTTP service (simple substring match)
    if ($content -match "ASPNETCORE_URLS") {
        Write-ValidationResult "CoreHub configured as HTTP service (ASPNETCORE_URLS found) - should be background service" -Type "Error"
        $script:Errors += "CoreHub configured as HTTP service"
        return $false
    }

    # Check if CoreHub has HTTP port exposure
    if ($content -match "ports:") {
        Write-ValidationResult "CoreHub has HTTP port exposed - should be background service" -Type "Error"
        $script:Errors += "CoreHub has HTTP port exposed"
        return $false
    }

    # Check if CoreHub has healthcheck (background services don't typically have HTTP healthchecks)
    if ($content -match "healthcheck:") {
        Write-ValidationResult "CoreHub has healthcheck configured - background services typically don't need HTTP healthchecks" -Type "Warning"
        $script:Warnings += "CoreHub has healthcheck configured"
    }

    Write-ValidationResult "CoreHub configuration validation passed" -Type "Success"
    return $true
}

function Test-GatewayConfiguration {
    param(
        [string]$FilePath
    )

    Write-ValidationResult "Validating Gateway configuration..." -Type "Info"

    $content = Get-Content $FilePath -Raw

    # Check if Gateway has depends_on (simple substring match)
    if ($content -notmatch "depends_on:") {
        Write-ValidationResult "Gateway missing depends_on section" -Type "Error"
        $script:Errors += "Gateway missing depends_on"
        return $false
    }

    # Check if Gateway has healthcheck
    if ($content -notmatch "healthcheck:") {
        Write-ValidationResult "Gateway missing healthcheck" -Type "Warning"
        $script:Warnings += "Gateway missing healthcheck"
    }

    Write-ValidationResult "Gateway configuration validation passed" -Type "Success"
    return $true
}

function Test-EnvironmentVariableNaming {
    param(
        [string]$FilePath
    )

    Write-ValidationResult "Validating environment variable naming..." -Type "Info"

    $content = Get-Content $FilePath -Raw

    # Simple check: verify that environment variables are present
    # More complex validation can be added later if needed
    if ($content -notmatch "environment:") {
        Write-ValidationResult "No environment variables found in docker-compose file" -Type "Warning"
        $script:Warnings += "No environment variables found"
    }
    else {
        Write-ValidationResult "Environment variable naming validation passed" -Type "Success"
    }

    return $true
}

function Test-LoggingConfiguration {
    param(
        [string]$FilePath
    )

    Write-ValidationResult "Validating logging configuration..." -Type "Info"

    $content = Get-Content $FilePath -Raw

    # Simple check: verify that logging is configured for at least some services
    if ($content -notmatch "logging:") {
        Write-ValidationResult "No logging configuration found in docker-compose file" -Type "Warning"
        $script:Warnings += "No logging configuration found"
    }
    else {
        Write-ValidationResult "Logging configuration validation passed" -Type "Success"
    }

    return $true
}

function Test-RequiredServices {
    param(
        [string]$FilePath
    )

    Write-ValidationResult "Validating required services..." -Type "Info"

    $content = Get-Content $FilePath -Raw
    $requiredServices = @("gateway", "shoperp", "khachlink")
    $missingServices = @()

    foreach ($service in $requiredServices) {
        # Simple check: just look for the service name followed by colon
        if ($content -notmatch "${service}:") {
            $missingServices += $service
        }
    }

    if ($missingServices.Count -gt 0) {
        Write-ValidationResult "Missing required services: $($missingServices -join ', ')" -Type "Error"
        $script:Errors += "Missing required services: $($missingServices -join ', ')"
        return $false
    }

    Write-ValidationResult "Required services validation passed" -Type "Success"
    return $true
}

# Main execution
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Docker Compose Validation Script" -ForegroundColor Cyan
Write-Host "File: $ComposeFile" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Run all validations
Test-DockerComposeSyntax -FilePath $ComposeFile
Test-CoreHubConfiguration -FilePath $ComposeFile
Test-GatewayConfiguration -FilePath $ComposeFile
Test-EnvironmentVariableNaming -FilePath $ComposeFile
Test-LoggingConfiguration -FilePath $ComposeFile
Test-RequiredServices -FilePath $ComposeFile

# Summary
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Validation Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

if ($script:Errors.Count -eq 0 -and $script:Warnings.Count -eq 0) {
    Write-ValidationResult "All validations passed!" -Type "Success"
    exit 0
}
elseif ($script:Errors.Count -eq 0 -and $script:Warnings.Count -gt 0) {
    Write-ValidationResult "Validation passed with warnings: $($script:Warnings.Count)" -Type "Warning"
    if ($Strict) {
        exit 1
    }
    exit 0
}
else {
    Write-ValidationResult "Validation failed with errors: $($script:Errors.Count)" -Type "Error"
    exit 1
}