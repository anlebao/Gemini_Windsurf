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

# 6. Check ADR-001 Compliance (Two-Version Strategy)
Write-Host "Checking ADR-001 compliance..." -ForegroundColor Yellow

$prodComposeFile = "docker-compose.prod.yml"
$edgeComposeFile = "docker-compose.edge.yml"

if (Test-Path $prodComposeFile) {
    $prodContent = Get-Content $prodComposeFile -Raw

    # Rule H: v1 SaaS (prod) MUST use PostgreSQL for CoreHub (cloud accounting, always online)
    $hasPostgresForCoreHub = $prodContent -match "Host=postgres" -or $prodContent -match "postgres:5432"
    if (-not $hasPostgresForCoreHub) {
        $violations += "ADR-001 v1 SaaS violation: docker-compose.prod.yml CoreHub must use PostgreSQL for cloud accounting"
        $hasViolations = $true
    }
} else {
    Write-Host "  Skipped: docker-compose.prod.yml not found (dev environment)" -ForegroundColor Yellow
}

if (Test-Path $edgeComposeFile) {
    $edgeContent = Get-Content $edgeComposeFile -Raw

    # Rule I: v2 Edge MUST use SQLite + NATS sync worker for offline station capability
    $hasSQLiteVolume = $edgeContent -match "shoperp_sqlite_data"
    $hasNatsSyncWorker = $edgeContent -match "shoperp-nats-sync" -or $edgeContent -match "nats-sync"
    $hasNatsBroker = $edgeContent -match "image:\s*nats:" -or $edgeContent -match "nats:2\.10"

    if (-not $hasSQLiteVolume) {
        $violations += "ADR-001 v2 Edge violation: docker-compose.edge.yml must declare 'shoperp_sqlite_data' volume for SQLite persistence"
        $hasViolations = $true
    }

    if (-not $hasNatsSyncWorker) {
        $violations += "ADR-001 v2 Edge violation: docker-compose.edge.yml must include 'shoperp-nats-sync' worker service"
        $hasViolations = $true
    }

    if (-not $hasNatsBroker) {
        $violations += "ADR-001 v2 Edge violation: docker-compose.edge.yml must include NATS broker for event-driven sync"
        $hasViolations = $true
    }
} else {
    # Edge compose is expected in ADR-001 v2 deployment; flag as violation if missing
    $violations += "ADR-001 v2 Edge violation: docker-compose.edge.yml not found"
    $hasViolations = $true
}

# 7. Report results
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
    Write-Host "ADR-001 compliance verified" -ForegroundColor Green
}
