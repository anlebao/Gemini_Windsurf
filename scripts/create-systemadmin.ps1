#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Create SystemAdmin user for ShopERP development
.DESCRIPTION
    Creates a SystemAdmin user in SQLite database for testing tenant onboarding
.EXAMPLE
    .\scripts\create-systemadmin.ps1
#>

param(
    [Parameter(Mandatory = $false)]
    [string]$DatabasePath = "5_WebApps/ShopERP/bin/Debug/net8.0/vanan_shoperp.db",

    [Parameter(Mandatory = $false)]
    [string]$Username = "systemadmin@vanan.vn",

    [Parameter(Mandatory = $true)]
    [string]$Password
)

# Load BCrypt
try {
    Add-Type -Path "3_CoreHub/bin/Release/net8.0/BCrypt.Net-Next.dll" -ErrorAction Stop
} catch {
    Write-Error "BCrypt.Net-Next not found. Please build the project first: dotnet build -c Release"
    exit 1
}

# Load SQLite
try {
    Add-Type -Path "3_CoreHub/bin/Release/net8.0/Microsoft.Data.Sqlite.dll" -ErrorAction Stop
} catch {
    Write-Error "Microsoft.Data.Sqlite not found. Please build the project first: dotnet build -c Release"
    exit 1
}

$connectionString = "Data Source=$DatabasePath"

Write-Host "=== Creating SystemAdmin User ===" -ForegroundColor Cyan
Write-Host "Database: $DatabasePath"
Write-Host "Username: $Username"
Write-Host "Password: $Password"
Write-Host ""

try {
    $connection = New-Object Microsoft.Data.Sqlite.SqliteConnection($connectionString)
    $connection.Open()

    # Check if user exists
    $checkUserCmd = $connection.CreateCommand()
    $checkUserCmd.CommandText = "SELECT COUNT(*) FROM Users WHERE Username = @Username"
    $checkUserCmd.Parameters.AddWithValue("@Username", $Username) | Out-Null
    $userExists = [int]$checkUserCmd.ExecuteScalar()

    if ($userExists -gt 0) {
        Write-Host "User $Username already exists, skipping..." -ForegroundColor Yellow
        $connection.Close()
        exit 0
    }

    # Generate user ID and password hash
    $userId = [Guid]::NewGuid()
    $passwordHash = [BCrypt.Net.BCrypt]::HashPassword($Password, 12)

    # Insert SystemAdmin user (no tenant - platform-level)
    $insertUserCmd = $connection.CreateCommand()
    $insertUserCmd.CommandText = "INSERT INTO Users (Id, Username, PasswordHash, DisplayName, Email, Role, IsActive, TenantId, CreatedAt, UpdatedAt) VALUES (@Id, @Username, @PasswordHash, @DisplayName, @Email, @Role, @IsActive, @TenantId, @CreatedAt, @UpdatedAt)"
    $insertUserCmd.Parameters.AddWithValue("@Id", $userId) | Out-Null
    $insertUserCmd.Parameters.AddWithValue("@Username", $Username) | Out-Null
    $insertUserCmd.Parameters.AddWithValue("@PasswordHash", $passwordHash) | Out-Null
    $insertUserCmd.Parameters.AddWithValue("@DisplayName", "System Administrator") | Out-Null
    $insertUserCmd.Parameters.AddWithValue("@Email", $Username) | Out-Null
    $insertUserCmd.Parameters.AddWithValue("@Role", "SystemAdmin") | Out-Null
    $insertUserCmd.Parameters.AddWithValue("@IsActive", $true) | Out-Null
    $insertUserCmd.Parameters.AddWithValue("@TenantId", [Guid]::Empty) | Out-Null
    $insertUserCmd.Parameters.AddWithValue("@CreatedAt", [DateTime]::UtcNow) | Out-Null
    $insertUserCmd.Parameters.AddWithValue("@UpdatedAt", [DateTime]::UtcNow) | Out-Null
    $insertUserCmd.ExecuteNonQuery() | Out-Null

    $connection.Close()

    Write-Host "✓ SystemAdmin user created successfully" -ForegroundColor Green
    Write-Host ""
    Write-Host "Login credentials:" -ForegroundColor Cyan
    Write-Host "  Username: $Username" -ForegroundColor White
    Write-Host "  Password: $Password" -ForegroundColor White
    Write-Host ""
    Write-Host "You can now login at: http://localhost:5003/Login" -ForegroundColor Yellow

} catch {
    Write-Error "Error: $_"
    if ($connection.State -eq "Open") {
        $connection.Close()
    }
    exit 1
}
