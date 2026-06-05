# architecture-guard.ps1 - Van An Architecture Compliance Guard v1.0
# Enforces Clean Architecture and Domain Entity Location Rules

Write-Host "Running Van An Architecture Guard v1.0..." -ForegroundColor Cyan

$violations = @()
$hasViolations = $false

# 1. Check domain entities in Service layer
Write-Host "Checking domain entities in Service layer..." -ForegroundColor Yellow

$serviceFiles = Get-ChildItem -Path "3_CoreHub\Services" -Filter "*.cs" -Recurse
foreach ($file in $serviceFiles) {
    $content = Get-Content $file.FullName -Raw
    
    # Check for domain entity definitions in Service layer
    $domainEntityPatterns = @(
        'public record.*Entry\(',
        'public record.*Balance\(',
        'public record.*Package\(',
        'public record.*Ledger\(',
        'public class.*Entry.*\{',
        'public class.*Balance.*\{',
        'public class.*Package.*\{',
        'public class.*Ledger.*\{'
    )
    
    foreach ($pattern in $domainEntityPatterns) {
        if ($content -match $pattern) {
            $violations += "Domain entity found in Service layer: $($file.Name) - Pattern: $pattern"
            $hasViolations = $true
        }
    }
}

# 2. Check domain entities in API layer
Write-Host "Checking domain entities in API layer..." -ForegroundColor Yellow

$apiFiles = Get-ChildItem -Path "2_Gateway" -Filter "*.cs" -Recurse
foreach ($file in $apiFiles) {
    $content = Get-Content $file.FullName -Raw
    
    $domainEntityPatterns = @(
        'public record.*Entry\(',
        'public record.*Balance\(',
        'public record.*Package\(',
        'public record.*Ledger\(',
        'public class.*Entry.*\{',
        'public class.*Balance.*\{',
        'public class.*Package.*\{',
        'public class.*Ledger.*\{'
    )
    
    foreach ($pattern in $domainEntityPatterns) {
        if ($content -match $pattern) {
            $violations += "Domain entity found in API layer: $($file.Name) - Pattern: $pattern"
            $hasViolations = $true
        }
    }
}

# 3. Verify domain entities are in 1_Shared/Domain.cs
Write-Host "Verifying domain entities in 1_Shared/Domain.cs..." -ForegroundColor Yellow

$expectedDomainEntities = @(
    'GeneralLedgerEntry',
    'DetailedLedgerEntry',
    'TrialBalance',
    'TrialBalanceAccount',
    'HKDBooksPackage'
)

$domainFile = "1_Shared\Domain.cs"
if (Test-Path $domainFile) {
    $domainContent = Get-Content $domainFile -Raw
    
    foreach ($entity in $expectedDomainEntities) {
        if ($domainContent -notmatch [regex]::Escape($entity)) {
            $violations += "Expected domain entity not found in 1_Shared/Domain.cs: $entity"
            $hasViolations = $true
        }
    }
} else {
    $violations += "1_Shared/Domain.cs not found"
    $hasViolations = $true
}

# 4. Check dependency directions
Write-Host "Checking dependency directions..." -ForegroundColor Yellow

# Check if Service layer references Domain layer correctly
$serviceFiles = Get-ChildItem -Path "3_CoreHub\Services" -Filter "*.cs" -Recurse
foreach ($file in $serviceFiles) {
    $content = Get-Content $file.FullName -Raw
    
    # Should reference 1_Shared.Domain
    if ($content -match 'using VanAn\.Shared\.Domain' -or $content -match 'using VanAn\.Shared') {
        # This is correct
        continue
    }
    
    # Should not define new domain entities inline - only service classes themselves are allowed
    # CoreHub services are permitted to reference VanAn.CoreHub.Domain (their own domain)
    if ($content -match 'using VanAn\.CoreHub\.Domain') {
        # Only flag if the service is defining domain entity types (not just the service class itself)
        $domainEntityInService = @(
            'public record.*Entry\b',
            'public record.*Balance\b',
            'public record.*Package\b',
            'public record.*Ledger\b',
            'public class.*Entry\s*[:{]',
            'public class.*Balance\s*[:{]',
            'public class.*Package\s*[:{]',
            'public class.*Ledger\s*[:{]'
        )
        foreach ($pattern in $domainEntityInService) {
            if ($content -match $pattern) {
                $violations += "Service layer defining domain entities while referencing CoreHub.Domain: $($file.Name) - Pattern: $pattern"
                $hasViolations = $true
                break
            }
        }
    }
}

# 5. Check for EF Core in Domain layer
Write-Host "Checking EF Core in Domain layer..." -ForegroundColor Yellow

$domainFiles = Get-ChildItem -Path "1_Shared" -Filter "*.cs" -Recurse
foreach ($file in $domainFiles) {
    $content = Get-Content $file.FullName -Raw
    
    # Remove commented lines to avoid false positives
    $contentWithoutComments = $content -replace '//.*', '' -replace '/\*.*?\*/', ''
    
    $efCorePatterns = @(
        'using Microsoft\.EntityFrameworkCore',
        'using System\.ComponentModel\.DataAnnotations',
        '\[Table\(',
        '\[Column\(',
        '\[Key\(',
        '\[ForeignKey\(',
        'DbSet<'
    )
    
    foreach ($pattern in $efCorePatterns) {
        if ($contentWithoutComments -match $pattern) {
            $violations += "EF Core found in Domain layer: $($file.Name) - Pattern: $pattern"
            $hasViolations = $true
        }
    }
}

# 6. Report results
Write-Host "ARCHITECTURE VALIDATION RESULTS:" -ForegroundColor Cyan

if ($hasViolations) {
    Write-Host "ARCHITECTURE VIOLATIONS DETECTED:" -ForegroundColor Red
    foreach ($violation in $violations) {
        Write-Host $violation -ForegroundColor Red
    }
    
    Write-Host "Architecture Guard FAILED - Fix violations before proceeding" -ForegroundColor Red
    exit 1
} else {
    Write-Host "Architecture Guard PASSED - All rules compliant" -ForegroundColor Green
    Write-Host "Domain entities in correct location" -ForegroundColor Green
    Write-Host "Clean Architecture respected" -ForegroundColor Green
    Write-Host "Dependency directions correct" -ForegroundColor Green
    Write-Host "Domain layer purity maintained" -ForegroundColor Green
}
