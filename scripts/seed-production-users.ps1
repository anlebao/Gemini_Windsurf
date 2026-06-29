#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Production User Seeding Script for VanAn Ecosystem
.DESCRIPTION
    Creates initial users for each role (Owner, StoreKeeper, Guard, Staff, Masterchef, SystemAdmin)
    with BCrypt password hashing. Run on production VPS after database initialization.
.NOTES
    Wave 5: Added SystemAdmin platform-level role
    Requires: BCrypt.Net-Next package (install via: dotnet add package BCrypt.Net-Next)
.EXAMPLE
    .\scripts\seed-production-users.ps1 -DatabasePath "/app/data/vanan.db"
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$DatabasePath,

    [Parameter(Mandatory = $false)]
    [string]$DefaultPassword = "VanAn@2026"
)

# Load BCrypt
try {
    Add-Type -Path "3_CoreHub/bin/Release/net8.0/BCrypt.Net-Next.dll" -ErrorAction Stop
} catch {
    Write-Error "BCrypt.Net-Next not found. Please build the project first: dotnet build -c Release"
    exit 1
}

# Database connection
$connectionString = "Data Source=$DatabasePath"

# Helper: Hash password with BCrypt
function Get-BCryptHash {
    param([string]$password)
    return [BCrypt.Net.BCrypt]::HashPassword($password, 12)  # Work factor 12
}

# Default tenant ID
$defaultTenantId = [Guid]::Parse("11111111-1111-1111-1111-111111111111")

# Users to create
$users = @(
    @{
        Username = "owner@vanan.vn"
        Email = "owner@vanan.vn"
        DisplayName = "Shop Owner"
        Role = "Owner"
        TenantId = $defaultTenantId
    },
    @{
        Username = "storekeeper@vanan.vn"
        Email = "storekeeper@vanan.vn"
        DisplayName = "Store Keeper"
        Role = "StoreKeeper"
        TenantId = $defaultTenantId
    },
    @{
        Username = "guard@vanan.vn"
        Email = "guard@vanan.vn"
        DisplayName = "Security Guard"
        Role = "Guard"
        TenantId = $defaultTenantId
    },
    @{
        Username = "staff@vanan.vn"
        Email = "staff@vanan.vn"
        DisplayName = "Staff Member"
        Role = "Staff"
        TenantId = $defaultTenantId
    },
    @{
        Username = "masterchef@vanan.vn"
        Email = "masterchef@vanan.vn"
        DisplayName = "Master Chef"
        Role = "Masterchef"
        TenantId = $defaultTenantId
    },
    @{
        Username = "systemadmin@vanan.vn"
        Email = "systemadmin@vanan.vn"
        DisplayName = "System Administrator"
        Role = "SystemAdmin"
        TenantId = [Guid]::Empty  # Platform-level, no tenant
    }
)

Write-Host "=== VanAn Production User Seeding ===" -ForegroundColor Cyan
Write-Host "Database: $DatabasePath"
Write-Host "Default Password: $DefaultPassword"
Write-Host ""

try {
    # Load SQLite assembly
    Add-Type -Path "3_CoreHub/bin/Release/net8.0/Microsoft.Data.Sqlite.dll" -ErrorAction Stop

    $connection = New-Object Microsoft.Data.Sqlite.SqliteConnection($connectionString)
    $connection.Open()

    # Check if tenant exists, create if not
    $checkTenantCmd = $connection.CreateCommand()
    $checkTenantCmd.CommandText = "SELECT COUNT(*) FROM Tenants WHERE Id = @Id"
    $checkTenantCmd.Parameters.AddWithValue("@Id", $defaultTenantId) | Out-Null
    $tenantExists = [int]$checkTenantCmd.ExecuteScalar()

    if ($tenantExists -eq 0) {
        Write-Host "Creating default tenant..." -ForegroundColor Yellow
        $insertTenantCmd = $connection.CreateCommand()
        $insertTenantCmd.CommandText = @"
            INSERT INTO Tenants (Id, Name, BusinessType, Status, CreatedAt, UpdatedAt, TenantId)
            VALUES (@Id, @Name, @BusinessType, @Status, @CreatedAt, @UpdatedAt, @TenantId)
        "@
        $insertTenantCmd.Parameters.AddWithValue("@Id", $defaultTenantId) | Out-Null
        $insertTenantCmd.Parameters.AddWithValue("@Name", "Default Shop") | Out-Null
        $insertTenantCmd.Parameters.AddWithValue("@BusinessType", 0) | Out-Null  # Company
        $insertTenantCmd.Parameters.AddWithValue("@Status", 0) | Out-Null  # Active
        $insertTenantCmd.Parameters.AddWithValue("@CreatedAt", [DateTime]::UtcNow) | Out-Null
        $insertTenantCmd.Parameters.AddWithValue("@UpdatedAt", [DateTime]::UtcNow) | Out-Null
        $insertTenantCmd.Parameters.AddWithValue("@TenantId", $defaultTenantId) | Out-Null
        $insertTenantCmd.ExecuteNonQuery() | Out-Null
        Write-Host "✓ Default tenant created" -ForegroundColor Green
    } else {
        Write-Host "✓ Default tenant already exists" -ForegroundColor Green
    }

    Write-Host ""
    Write-Host "Creating users..." -ForegroundColor Yellow

    foreach ($user in $users) {
        # Check if user exists
        $checkUserCmd = $connection.CreateCommand()
        $checkUserCmd.CommandText = "SELECT COUNT(*) FROM Users WHERE Username = @Username"
        $checkUserCmd.Parameters.AddWithValue("@Username", $user.Username) | Out-Null
        $userExists = [int]$checkUserCmd.ExecuteScalar()

        if ($userExists -gt 0) {
            Write-Host "  → User $($user.Username) already exists, skipping..." -ForegroundColor Gray
            continue
        }

        # Generate user ID
        $userId = [Guid]::NewGuid()
        $passwordHash = Get-BCryptHash -password $DefaultPassword

        # Insert user
        $insertUserCmd = $connection.CreateCommand()
        $insertUserCmd.CommandText = @"
            INSERT INTO Users (Id, Username, PasswordHash, DisplayName, Email, Role, IsActive, TenantId, CreatedAt, UpdatedAt)
            VALUES (@Id, @Username, @PasswordHash, @DisplayName, @Email, @Role, @IsActive, @TenantId, @CreatedAt, @UpdatedAt)
        "@
        $insertUserCmd.Parameters.AddWithValue("@Id", $userId) | Out-Null
        $insertUserCmd.Parameters.AddWithValue("@Username", $user.Username) | Out-Null
        $insertUserCmd.Parameters.AddWithValue("@PasswordHash", $passwordHash) | Out-Null
        $insertUserCmd.Parameters.AddWithValue("@DisplayName", $user.DisplayName) | Out-Null
        $insertUserCmd.Parameters.AddWithValue("@Email", $user.Email) | Out-Null
        $insertUserCmd.Parameters.AddWithValue("@Role", $user.Role) | Out-Null
        $insertUserCmd.Parameters.AddWithValue("@IsActive", $true) | Out-Null
        $insertUserCmd.Parameters.AddWithValue("@TenantId", $user.TenantId) | Out-Null
        $insertUserCmd.Parameters.AddWithValue("@CreatedAt", [DateTime]::UtcNow) | Out-Null
        $insertUserCmd.Parameters.AddWithValue("@UpdatedAt", [DateTime]::UtcNow) | Out-Null
        $insertUserCmd.ExecuteNonQuery() | Out-Null

        # Create UserTenant mapping for tenant-level users
        if ($user.TenantId -ne [Guid]::Empty) {
            $insertUserTenantCmd = $connection.CreateCommand()
            $insertUserTenantCmd.CommandText = @"
                INSERT INTO UserTenants (Id, UserId, TenantId, Role, AssignedAt, IsActive, TenantIdValue)
                VALUES (@Id, @UserId, @TenantId, @Role, @AssignedAt, @IsActive, @TenantIdValue)
            "@
            $userTenantId = [Guid]::NewGuid()
            $insertUserTenantCmd.Parameters.AddWithValue("@Id", $userTenantId) | Out-Null
            $insertUserTenantCmd.Parameters.AddWithValue("@UserId", $userId) | Out-Null
            $insertUserTenantCmd.Parameters.AddWithValue("@TenantId", $user.TenantId) | Out-Null
            $insertUserTenantCmd.Parameters.AddWithValue("@Role", $user.Role) | Out-Null
            $insertUserTenantCmd.Parameters.AddWithValue("@AssignedAt", [DateTime]::UtcNow) | Out-Null
            $insertUserTenantCmd.Parameters.AddWithValue("@IsActive", $true) | Out-Null
            $insertUserTenantCmd.Parameters.AddWithValue("@TenantIdValue", $user.TenantId) | Out-Null
            $insertUserTenantCmd.ExecuteNonQuery() | Out-Null
        }

        Write-Host "  ✓ Created user: $($user.Username) ($($user.Role))" -ForegroundColor Green
    }

    $connection.Close()

    Write-Host ""
    Write-Host "=== Seeding Complete ===" -ForegroundColor Green
    Write-Host "Total users created: $($users.Count)"
    Write-Host ""
    Write-Host "Login credentials:" -ForegroundColor Cyan
    foreach ($user in $users) {
        Write-Host "  $($user.Username) / $DefaultPassword" -ForegroundColor White
    }
    Write-Host ""
    Write-Host "⚠️  IMPORTANT: Change default passwords immediately after first login!" -ForegroundColor Yellow

} catch {
    Write-Error "Error: $_"
    if ($connection.State -eq "Open") {
        $connection.Close()
    }
    exit 1
}