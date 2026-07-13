#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Validate environment variable configuration across environments.

.DESCRIPTION
    This script validates environment variables to ensure consistency across environments.
    It checks for:
    - Required environment variables
    - Variable naming conventions
    - Consistency across .env files
    - Missing or invalid values

.PARAMETER EnvFile
    Path to .env file to validate. Default: .env

.PARAMETER Strict
    Enable strict validation (fail on warnings)

.EXAMPLE
    .\scripts\validate-env-vars.ps1
    Validates .env file

.EXAMPLE
    .\scripts\validate-env-vars.ps1 -EnvFile .env.production -Strict
    Validates .env.production with strict mode
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$EnvFile = ".env",

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

function Test-EnvFileExists {
    param(
        [string]$FilePath
    )

    Write-ValidationResult "Checking if .env file exists..." -Type "Info"

    if (-not (Test-Path $FilePath)) {
        Write-ValidationResult ".env file not found: $FilePath" -Type "Error"
        $script:Errors += ".env file not found: $FilePath"
        return $false
    }

    Write-ValidationResult ".env file exists" -Type "Success"
    return $true
}

function Test-RequiredVariables {
    param(
        [string]$FilePath
    )

    Write-ValidationResult "Validating required environment variables..." -Type "Info"

    $lines = Get-Content $FilePath
    $requiredVars = @(
        "POSTGRES_PASSWORD",
        "JWT_SECRET_KEY",
        "SEQ_ADMIN_PASSWORD",
        "GOOGLE_CLIENT_ID",
        "GOOGLE_CLIENT_SECRET"
    )

    $missingVars = @()
    $placeholderPatterns = @("CHANGE_THIS", "YOUR_", "test-", "placeholder")
    $foundVars = @{}

    # Check each line for required variables
    foreach ($line in $lines) {
        foreach ($var in $requiredVars) {
            if ($line -match "^\s*$var\s*=(.*)$") {
                $foundVars[$var] = $matches[1].Trim()
            }
        }
    }

    # Check for missing variables
    foreach ($var in $requiredVars) {
        if (-not $foundVars.ContainsKey($var)) {
            $missingVars += $var
        }
    }

    if ($missingVars.Count -gt 0) {
        Write-ValidationResult "Missing required variables: $($missingVars -join ', ')" -Type "Error"
        $script:Errors += "Missing required variables: $($missingVars -join ', ')"
        return $false
    }

    # Check if using placeholder values (warning, not error)
    foreach ($var in $requiredVars) {
        if ($foundVars[$var] -match ($placeholderPatterns -join '|')) {
            Write-ValidationResult "$var is using placeholder value - should be replaced in production" -Type "Warning"
            $script:Warnings += "$var is using placeholder value"
        }
    }

    Write-ValidationResult "Required variables validation passed" -Type "Success"
    return $true
}

function Test-VariableNaming {
    param(
        [string]$FilePath
    )

    Write-ValidationResult "Validating environment variable naming..." -Type "Info"

    $lines = Get-Content $FilePath
    $invalidNames = @()

    # Check each line for variable names
    foreach ($line in $lines) {
        if ($line -match "^\s*([A-Z_][A-Z0-9_]*)\s*=") {
            $varName = $matches[1]

            # Check for invalid patterns (only check the variable name itself)
            if ($varName -cmatch "[a-z]") {  # Use case-sensitive match
                $invalidNames += "$varName (contains lowercase)"
            }
            if ($varName -match "__") {
                $invalidNames += "$varName (double underscore - use single)"
            }
        }
    }

    if ($invalidNames.Count -gt 0) {
        Write-ValidationResult "Invalid variable names: $($invalidNames -join ', ')" -Type "Warning"
        $script:Warnings += "Invalid variable names: $($invalidNames -join ', ')"
    }
    else {
        Write-ValidationResult "Variable naming validation passed" -Type "Success"
    }

    return $true
}

function Test-EmptyValues {
    param(
        [string]$FilePath
    )

    Write-ValidationResult "Validating empty environment variable values..." -Type "Info"

    $content = Get-Content $FilePath -Raw
    $emptyVars = @()

    # Find variables with empty values
    $matches = [regex]::Matches($content, "^([A-Z_][A-Z0-9_]*)=$", [System.Text.RegularExpressions.RegexOptions]::Multiline)

    foreach ($match in $matches) {
        $varName = $match.Groups[1].Value
        $emptyVars += $varName
    }

    if ($emptyVars.Count -gt 0) {
        Write-ValidationResult "Variables with empty values: $($emptyVars -join ', ')" -Type "Warning"
        $script:Warnings += "Variables with empty values: $($emptyVars -join ', ')"
    }
    else {
        Write-ValidationResult "Empty values validation passed" -Type "Success"
    }

    return $true
}

function Test-SecretStrength {
    param(
        [string]$FilePath
    )

    Write-ValidationResult "Validating secret strength..." -Type "Info"

    $content = Get-Content $FilePath -Raw
    $weakSecrets = @()

    # Check for weak secrets (common default values).
    # Patterns use end-of-line anchor $ to avoid false positives on legitimate CI values
    # that are prefixed with a weak word (e.g. "test-password-for-ci-only" must NOT match "=test$").
    # The (?m) flag enables multiline mode so $ matches end of each line.
    $weakPatterns = @(
        "(?m)password=password$",
        "(?m)password=123456$",
        "(?m)password=admin$",
        "(?m)password=test$",
        "(?m)password=changeme$",
        "(?m)secret=secret$",
        "(?m)secret=123456$",
        "(?m)secret=test$",
        "(?m)secret=changeme$",
        "(?m)JWT_SECRET_KEY=changeme$",
        "(?m)JWT_SECRET_KEY=secret$",
        "(?m)JWT_SECRET_KEY=test$",
        "(?m)POSTGRES_PASSWORD=changeme$",
        "(?m)POSTGRES_PASSWORD=secret$",
        "(?m)POSTGRES_PASSWORD=test$",
        "(?m)SEQ_ADMIN_PASSWORD=changeme$",
        "(?m)SEQ_ADMIN_PASSWORD=secret$",
        "(?m)SEQ_ADMIN_PASSWORD=test$"
    )

    foreach ($pattern in $weakPatterns) {
        if ($content -imatch $pattern) {
            # Strip regex flags for readable output (e.g. "(?m)POSTGRES_PASSWORD=test$" → "POSTGRES_PASSWORD=test")
            $readable = $pattern -replace '^\(\?[a-z]+\)', '' -replace '\$$', ''
            $weakSecrets += $readable
        }
    }

    if ($weakSecrets.Count -gt 0) {
        Write-ValidationResult "Weak secrets detected: $($weakSecrets -join ', ')" -Type "Error"
        $script:Errors += "Weak secrets detected: $($weakSecrets -join ', ')"
        return $false
    }

    Write-ValidationResult "Secret strength validation passed" -Type "Success"
    return $true
}

function Test-DockerComposeConsistency {
    param(
        [string]$EnvFilePath
    )

    Write-ValidationResult "Validating consistency with docker-compose files..." -Type "Info"

    # Check if docker-compose.prod.yml exists
    $dockerComposePath = "docker-compose.prod.yml"
    if (-not (Test-Path $dockerComposePath)) {
        Write-ValidationResult "docker-compose.prod.yml not found, skipping consistency check" -Type "Warning"
        $script:Warnings += "docker-compose.prod.yml not found"
        return $true
    }

    $dockerComposeContent = Get-Content $dockerComposePath -Raw

    # Extract environment variables referenced in docker-compose
    $dockerComposeVars = [regex]::Matches($dockerComposeContent, "\$\{([A-Z_][A-Z0-9_]*)")
    $referencedVars = $dockerComposeVars | ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique

    # Build dictionary from env file using line-by-line parse (same as Test-RequiredVariables)
    $envVars = @{}
    foreach ($line in (Get-Content $EnvFilePath)) {
        if ($line -match "^\s*([A-Z_][A-Z0-9_]*)\s*=") {
            $envVars[$matches[1]] = $true
        }
    }

    # Check if referenced variables are defined in .env
    $missingInEnv = @()
    foreach ($var in $referencedVars) {
        if (-not $envVars.ContainsKey($var)) {
            $missingInEnv += $var
        }
    }

    if ($missingInEnv.Count -gt 0) {
        Write-ValidationResult "Variables referenced in docker-compose but missing in .env: $($missingInEnv -join ', ')" -Type "Warning"
        $script:Warnings += "Variables referenced in docker-compose but missing in .env: $($missingInEnv -join ', ')"
    }
    else {
        Write-ValidationResult "Docker compose consistency validation passed" -Type "Success"
    }

    return $true
}

# Main execution
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Environment Variable Validation Script" -ForegroundColor Cyan
Write-Host "File: $EnvFile" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Run all validations
Test-EnvFileExists -FilePath $EnvFile
if ($script:Errors.Count -eq 0) {
    Test-RequiredVariables -FilePath $EnvFile
    Test-VariableNaming -FilePath $EnvFile
    Test-EmptyValues -FilePath $EnvFile
    Test-SecretStrength -FilePath $EnvFile
    Test-DockerComposeConsistency -EnvFilePath $EnvFile
}

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