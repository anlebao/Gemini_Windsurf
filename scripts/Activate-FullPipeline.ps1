#Requires -Version 5.1
<#
.SYNOPSIS
    Activate Full AI-Assisted Development Pipeline (Option A)
.DESCRIPTION
    Automates the activation of Phases 1-4: GitHub repo, token setup, Actions, MCP

.EXAMPLE
    # Interactive (secure - token hidden input):
    .\scripts\Activate-FullPipeline.ps1

    # With parameters (token visible in console history):
    .\scripts\Activate-FullPipeline.ps1 -GitHubUsername "anlebao" -GitHubToken "ghp_xxx"

.NOTES
    Run with: .\scripts\Activate-FullPipeline.ps1
    Requires: Git, Node.js, GitHub account
    File: scripts/Activate-FullPipeline.ps1
#>

[CmdletBinding()]
param(
    [string]$GitHubUsername = "anlebao",
    [string]$GitHubToken = "",
    [string]$RepoName = "Gemini_Windsurf",
    [switch]$SkipConfirmation
)

# Colors
$Colors = @{
    Success = 'Green'
    Error = 'Red'
    Warning = 'Yellow'
    Info = 'Cyan'
    Header = 'Magenta'
}

function Write-Step {
    param([string]$Message)
    Write-Host "`n[$(Get-Date -Format 'HH:mm:ss')] === $Message ===" -ForegroundColor $Colors.Header
}

function Write-Status {
    param([string]$Message, [string]$Status = "Info")
    $color = $Colors[$Status]
    $icon = switch ($Status) {
        'Success' { '✓' }
        'Error' { '✗' }
        'Warning' { '⚠' }
        default { '→' }
    }
    Write-Host "  $icon $Message" -ForegroundColor $color
}

# ==================== PHASE 0: VALIDATION ====================
Write-Step "PHASE 0: Pre-Flight Validation"

$Checks = @(
    @{ Name = "Git"; Command = "git --version"; InstallUrl = "https://git-scm.com/download/win" }
    @{ Name = "Node.js"; Command = "node --version"; InstallUrl = "https://nodejs.org/" }
    @{ Name = "npm"; Command = "npm --version"; InstallUrl = "https://nodejs.org/" }
)

$AllPassed = $true
foreach ($check in $Checks) {
    try {
        $result = Invoke-Expression $check.Command 2>$null
        Write-Status "$($check.Name): $result" "Success"
    } catch {
        Write-Status "$($check.Name) not found. Install from: $($check.InstallUrl)" "Error"
        $AllPassed = $false
    }
}

if (-not $AllPassed) {
    Write-Status "Please install missing prerequisites and rerun." "Error"
    exit 1
}

# Project directory
$ProjectPath = "c:\VibeCoding\Gemini_Windsurf"
if (-not (Test-Path $ProjectPath)) {
    Write-Status "Project not found at $ProjectPath" "Error"
    exit 1
}
Set-Location $ProjectPath
Write-Status "Working directory: $ProjectPath" "Success"

# GitHub credentials
if (-not $GitHubUsername) {
    $GitHubUsername = Read-Host "Enter your GitHub username"
}
if (-not $GitHubToken) {
    $GitHubToken = Read-Host "Enter your GitHub Personal Access Token (ghp_...)" -AsSecureString
    $GitHubToken = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto(
        [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($GitHubToken)
    )
}

# Validate token format
if (-not ($GitHubToken -match '^ghp_[a-zA-Z0-9]{36}$')) {
    Write-Status "Token format appears invalid. Should be: ghp_xxxxxxxx..." "Warning"
    $continue = Read-Host "Continue anyway? (y/N)"
    if ($continue -ne 'y') { exit 1 }
}

# ==================== PHASE A: GITHUB REPO ====================
Write-Step "PHASE A: GitHub Repository Setup"

# Check git status
$gitStatus = git status --porcelain 2>$null
if ($gitStatus) {
    Write-Status "Uncommitted changes detected:" "Warning"
    Write-Host $gitStatus
    $commit = Read-Host "Commit all changes before continuing? (y/N)"
    if ($commit -eq 'y') {
        git add .
        git commit -m "WIP: before pipeline activation"
    }
}

# Check if remote exists
$remote = git remote get-url origin 2>$null
if ($remote) {
    Write-Status "Remote already configured: $remote" "Info"
    $update = Read-Host "Update remote to github.com/$GitHubUsername/$RepoName? (y/N)"
    if ($update -eq 'y') {
        git remote remove origin
        git remote add origin "https://github.com/$GitHubUsername/$RepoName.git"
    }
} else {
    git remote add origin "https://github.com/$GitHubUsername/$RepoName.git"
    Write-Status "Remote added: github.com/$GitHubUsername/$RepoName" "Success"
}

# Test connection
Write-Status "Testing GitHub connection..." "Info"
try {
    $response = Invoke-RestMethod -Uri "https://api.github.com/user" `
        -Headers @{ Authorization = "token $GitHubToken" } `
        -ErrorAction Stop
    Write-Status "Authenticated as: $($response.login)" "Success"
} catch {
    Write-Status "GitHub authentication failed: $_" "Error"
    exit 1
}

# Check if repo exists
try {
    $repo = Invoke-RestMethod -Uri "https://api.github.com/repos/$GitHubUsername/$RepoName" `
        -Headers @{ Authorization = "token $GitHubToken" } `
        -ErrorAction Stop
    Write-Status "Repository exists: $($repo.html_url)" "Success"
} catch {
    Write-Status "Repository not found. Creating..." "Info"
    
    $body = @{
        name = $RepoName
        description = "ShopERP - VAT 2026 Compliant POS & Accounting System"
        private = $true
        auto_init = $false
    } | ConvertTo-Json
    
    try {
        $newRepo = Invoke-RestMethod -Uri "https://api.github.com/user/repos" `
            -Method POST `
            -Headers @{ 
                Authorization = "token $GitHubToken"
                Accept = "application/vnd.github.v3+json"
            } `
            -Body $body `
            -ContentType "application/json" `
            -ErrorAction Stop
        Write-Status "Repository created: $($newRepo.html_url)" "Success"
    } catch {
        Write-Status "Failed to create repository: $_" "Error"
        exit 1
    }
}

# Push to GitHub
Write-Status "Pushing code to GitHub..." "Info"
try {
    git push -u origin main 2>&1 | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }
    Write-Status "Code pushed successfully" "Success"
} catch {
    Write-Status "Push failed. Trying force push..." "Warning"
    $force = Read-Host "Force push may overwrite remote. Continue? (y/N)"
    if ($force -eq 'y') {
        git push -u origin main --force
    }
}

# ==================== PHASE B: CODEOWNERS ====================
Write-Step "PHASE B: Update CODEOWNERS"

$codeownersPath = ".github/CODEOWNERS"
if (Test-Path $codeownersPath) {
    $content = Get-Content $codeownersPath -Raw
    
    # Replace placeholders
    $updated = $content -replace '@owner', "@$GitHubUsername"
    $updated = $updated -replace '@domain-guardian', "@$GitHubUsername"
    $updated = $updated -replace '@devops', "@$GitHubUsername"
    $updated = $updated -replace '@architect', "@$GitHubUsername"
    $updated = $updated -replace '@ui-lead', "@$GitHubUsername"
    $updated = $updated -replace '@qa-lead', "@$GitHubUsername"
    $updated = $updated -replace '@mobile-lead', "@$GitHubUsername"
    $updated = $updated -replace '@accounting-domain', "@$GitHubUsername"
    $updated = $updated -replace '@order-domain', "@$GitHubUsername"
    $updated = $updated -replace '@security', "@$GitHubUsername"
    $updated = $updated -replace '@tech-writer', "@$GitHubUsername"
    
    Set-Content $codeownersPath $updated -NoNewline
    Write-Status "CODEOWNERS updated with @$GitHubUsername" "Success"
    
    git add $codeownersPath
    git commit -m "chore: update CODEOWNERS with GitHub username"
    git push origin main
    Write-Status "CODEOWNERS pushed" "Success"
}

# ==================== PHASE C: ENVIRONMENT VARIABLE ====================
Write-Step "PHASE C: Environment Configuration"

# Set user-level env var
[Environment]::SetEnvironmentVariable("GITHUB_TOKEN", $GitHubToken, "User")
$env:GITHUB_TOKEN = $GitHubToken
Write-Status "GITHUB_TOKEN set in user environment" "Success"

# Create .env for reference (not for actual use - env var is better)
$envContent = @"
# VanAn Pipeline Configuration
# Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
# GitHub Token is stored in environment variable (not this file)

GITHUB_USERNAME=$GitHubUsername
REPO_NAME=$RepoName
PROJECT_PATH=$ProjectPath
"@

$envContent | Out-File -FilePath ".env.pipeline" -Encoding utf8
Write-Status "Reference file created: .env.pipeline" "Success"

# ==================== PHASE D: MCP INSTALLATION ====================
Write-Step "PHASE D: MCP Server Installation"

# Check if already installed
$githubMcp = npm list -g @modelcontextprotocol/server-github 2>$null
$filesystemMcp = npm list -g @modelcontextprotocol/server-filesystem 2>$null

if ($githubMcp -match "empty") {
    Write-Status "Installing GitHub MCP server..." "Info"
    npm install -g @modelcontextprotocol/server-github 2>&1 | ForEach-Object { 
        if ($_ -match "error|ERR") { Write-Host "    $_" -ForegroundColor Red }
        else { Write-Host "    $_" -ForegroundColor Gray }
    }
    Write-Status "GitHub MCP server installed" "Success"
} else {
    Write-Status "GitHub MCP server already installed" "Success"
}

if ($filesystemMcp -match "empty") {
    Write-Status "Installing Filesystem MCP server..." "Info"
    npm install -g @modelcontextprotocol/server-filesystem 2>&1 | ForEach-Object { 
        if ($_ -match "error|ERR") { Write-Host "    $_" -ForegroundColor Red }
        else { Write-Host "    $_" -ForegroundColor Gray }
    }
    Write-Status "Filesystem MCP server installed" "Success"
} else {
    Write-Status "Filesystem MCP server already installed" "Success"
}

# Test servers
Write-Status "Testing MCP servers..." "Info"

$testTimeout = 5
$job = Start-Job {
    param($token)
    $env:GITHUB_PERSONAL_ACCESS_TOKEN = $token
    npx @modelcontextprotocol/server-github --help
} -ArgumentList $GitHubToken

$job | Wait-Job -Timeout $testTimeout | Out-Null
if ($job.State -eq 'Completed') {
    Write-Status "GitHub MCP server responds" "Success"
} else {
    Stop-Job $job
    Write-Status "GitHub MCP test timed out (may still work)" "Warning"
}
Remove-Job $job

# ==================== PHASE E: WINDSURF CONFIG ====================
Write-Step "PHASE E: Windsurf Configuration"

$windsurfSettingsDir = "$env:APPDATA\Windsurf\User"
$windsurfSettingsPath = "$windsurfSettingsDir\settings.json"

if (-not (Test-Path $windsurfSettingsDir)) {
    New-Item -ItemType Directory -Path $windsurfSettingsDir -Force | Out-Null
}

# Load existing or create new
if (Test-Path $windsurfSettingsPath) {
    $settings = Get-Content $windsurfSettingsPath | ConvertFrom-Json
    Write-Status "Existing Windsurf settings found" "Info"
} else {
    $settings = @{}
    Write-Status "Creating new Windsurf settings" "Info"
}

# Add MCP configuration
$settings.mcp = @{
    servers = @(
        @{
            name = "github"
            command = "npx"
            args = @("-y", "@modelcontextprotocol/server-github")
            env = @{
                GITHUB_PERSONAL_ACCESS_TOKEN = "`${env:GITHUB_TOKEN}"
            }
        },
        @{
            name = "filesystem"
            command = "npx"
            args = @("-y", "@modelcontextprotocol/server-filesystem", $ProjectPath)
        }
    )
}

# Save settings
$settings | ConvertTo-Json -Depth 10 | Set-Content $windsurfSettingsPath
Write-Status "Windsurf MCP configuration saved" "Success"
Write-Status "Config location: $windsurfSettingsPath" "Info"

# Display the config for verification
Write-Host "`nGenerated configuration:" -ForegroundColor $Colors.Info
Get-Content $windsurfSettingsPath | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }

# ==================== PHASE F: SUMMARY ====================
Write-Step "PHASE F: Activation Summary"

Write-Host @"

╔══════════════════════════════════════════════════════════════════╗
║           PIPELINE ACTIVATION COMPLETE                          ║
╠══════════════════════════════════════════════════════════════════╣
║                                                                  ║
║  Repository:     https://github.com/$GitHubUsername/$RepoName     ║
║  Token Status:  Set in environment variable                     ║
║  MCP Servers:   Installed and configured                        ║
║  Windsurf:      Config saved, RESTART REQUIRED                  ║
║                                                                  ║
╠══════════════════════════════════════════════════════════════════╣
║                        NEXT STEPS                                ║
╠══════════════════════════════════════════════════════════════════╣
║                                                                  ║
║  1. RESTART Windsurf IDE completely                            ║
║                                                                  ║
║  2. Enable GitHub Actions:                                     ║
║     - Go to: https://github.com/$GitHubUsername/$RepoName/settings/actions ║
║     - Enable "Allow all actions and reusable workflows"          ║
║                                                                  ║
║  3. Set branch protection:                                      ║
║     - Settings → Branches → Add rule for 'main'                ║
║     - Enable: Require pull request reviews                      ║
║     - Enable: Require status checks                             ║
║                                                                  ║
║  4. Test the pipeline:                                          ║
║     - Create a test issue on GitHub                             ║
║     - In Windsurf: "Implement feature for issue #1"             ║
║                                                                  ║
╠══════════════════════════════════════════════════════════════════╣
║  Status Check: .\scripts\Test-PipelineStatus.ps1                 ║
╚══════════════════════════════════════════════════════════════════╝

"@ -ForegroundColor $Colors.Success

Write-Status "Activation script completed at $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" "Success"
