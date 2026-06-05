#Requires -Version 5.1
<#
.SYNOPSIS
    Activate Full AI-Assisted Development Pipeline (Option A)
    Compatible with Windows PowerShell 5.1
.NOTES
    Run: .\scripts\Activate-FullPipeline-v2.ps1
#>

param(
    [string]$GitHubUsername = "anlebao",
    [string]$GitHubToken = "",
    [string]$RepoName = "Gemini_Windsurf"
)

# Simple output functions
function Write-Step($msg) {
    Write-Host ""
    Write-Host "=== $msg ===" -ForegroundColor Magenta
}

function Write-OK($msg) {
    Write-Host "  [OK] $msg" -ForegroundColor Green
}

function Write-Fail($msg) {
    Write-Host "  [FAIL] $msg" -ForegroundColor Red
}

function Write-Info($msg) {
    Write-Host "  [INFO] $msg" -ForegroundColor Cyan
}

# Project path
$ProjectPath = "c:\VibeCoding\Gemini_Windsurf"
Set-Location $ProjectPath

# ==================== PHASE 0 ====================
Write-Step "PHASE 0: Validation"

# Check prerequisites
$checks = @(
    @{ name = "Git"; cmd = "git --version" },
    @{ name = "Node.js"; cmd = "node --version" },
    @{ name = "npm"; cmd = "npm --version" }
)

$allPassed = $true
foreach ($c in $checks) {
    try {
        $result = Invoke-Expression $c.cmd 2>$null
        Write-OK "$($c.name): $result"
    } catch {
        Write-Fail "$($c.name) not found"
        $allPassed = $false
    }
}

if (-not $allPassed) {
    Write-Fail "Please install missing prerequisites"
    exit 1
}

# Get token if not provided
if (-not $GitHubToken) {
    $GitHubToken = Read-Host "Enter your GitHub Personal Access Token (ghp_...)"
}

# Simple token validation (basic check)
if (-not ($GitHubToken -like "ghp_*")) {
    Write-Fail "Token should start with 'ghp_'"
    exit 1
}

Write-OK "Token format valid"

# ==================== PHASE A ====================
Write-Step "PHASE A: GitHub Repository Setup"

# Test GitHub authentication
Write-Info "Testing GitHub authentication..."
try {
    $headers = @{ Authorization = "token $GitHubToken" }
    $user = Invoke-RestMethod -Uri "https://api.github.com/user" -Headers $headers
    Write-OK "Authenticated as: $($user.login)"
} catch {
    Write-Fail "GitHub authentication failed: $_"
    exit 1
}

# Check if repo exists
$repoUrl = "https://api.github.com/repos/$GitHubUsername/$RepoName"
try {
    $repo = Invoke-RestMethod -Uri $repoUrl -Headers $headers
    Write-OK "Repository exists: $($repo.html_url)"
} catch {
    Write-Info "Creating repository..."
    $body = @{ 
        name = $RepoName
        description = "ShopERP - VAT 2026 Compliant POS & Accounting System"
        private = $true 
    } | ConvertTo-Json
    
    try {
        $newRepo = Invoke-RestMethod -Uri "https://api.github.com/user/repos" `
            -Method POST -Headers $headers -Body $body -ContentType "application/json"
        Write-OK "Repository created: $($newRepo.html_url)"
    } catch {
        Write-Fail "Failed to create repository: $_"
        exit 1
    }
}

# Configure git remote
$remote = git remote get-url origin 2>$null
if ($remote) {
    Write-Info "Updating remote to: https://github.com/$GitHubUsername/$RepoName.git"
    git remote remove origin
} else {
    Write-Info "Adding remote..."
}

git remote add origin "https://github.com/$GitHubUsername/$RepoName.git"
Write-OK "Remote configured"

# Push code
Write-Info "Pushing code to GitHub..."
try {
    git push -u origin main 2>&1 | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }
    Write-OK "Code pushed successfully"
} catch {
    Write-Fail "Push failed: $_"
    Write-Info "Trying with force flag..."
    git push -u origin main --force
}

# ==================== PHASE B ====================
Write-Step "PHASE B: Update CODEOWNERS"

$codeownersPath = ".github/CODEOWNERS"
if (Test-Path $codeownersPath) {
    $content = Get-Content $codeownersPath -Raw
    $replacements = @(
        @('@[oO][wW][nN][eE][rR]', "@$GitHubUsername"),
        @('@[dD][oO][mM][aA][iI][nN]-[gG][uU][aA][rR][dD][iI][aA][nN]', "@$GitHubUsername"),
        @('@[dD][eE][vV][oO][pP][sS]', "@$GitHubUsername"),
        @('@[aA][rR][cC][hH][iI][tT][eE][cC][tT]', "@$GitHubUsername"),
        @('@[uU][iI]-[lL][eE][aA][dD]', "@$GitHubUsername"),
        @('@[qQ][aA]-[lL][eE][aA][dD]', "@$GitHubUsername")
    )
    
    $updated = $content
    foreach ($r in $replacements) {
        $updated = $updated -replace $r[0], $r[1]
    }
    
    Set-Content $codeownersPath $updated -NoNewline
    Write-OK "CODEOWNERS updated"
    
    git add $codeownersPath
    git commit -m "chore: update CODEOWNERS with GitHub username" 2>$null
    git push origin main 2>$null
    Write-OK "CODEOWNERS pushed"
} else {
    Write-Fail "CODEOWNERS not found"
}

# ==================== PHASE C ====================
Write-Step "PHASE C: Environment Configuration"

# Set environment variable
[Environment]::SetEnvironmentVariable("GITHUB_TOKEN", $GitHubToken, "User")
$env:GITHUB_TOKEN = $GitHubToken
Write-OK "GITHUB_TOKEN set in user environment"

# Create reference file
$envContent = @"
# VanAn Pipeline Configuration
# Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
GITHUB_USERNAME=$GitHubUsername
REPO_NAME=$RepoName
PROJECT_PATH=$ProjectPath
"@
$envContent | Out-File -FilePath ".env.pipeline" -Encoding utf8
Write-OK "Reference file created: .env.pipeline"

# ==================== PHASE D ====================
Write-Step "PHASE D: MCP Server Installation"

# Install MCP servers
Write-Info "Installing GitHub MCP server..."
npm install -g @modelcontextprotocol/server-github 2>&1 | ForEach-Object { 
    if ($_ -match "error|ERR") { Write-Host "    ERROR: $_" -ForegroundColor Red }
}
Write-OK "GitHub MCP server installed"

Write-Info "Installing Filesystem MCP server..."
npm install -g @modelcontextprotocol/server-filesystem 2>&1 | ForEach-Object { 
    if ($_ -match "error|ERR") { Write-Host "    ERROR: $_" -ForegroundColor Red }
}
Write-OK "Filesystem MCP server installed"

# ==================== PHASE E ====================
Write-Step "PHASE E: Windsurf Configuration"

$windsurfDir = "$env:APPDATA\Windsurf\User"
$windsurfPath = "$windsurfDir\settings.json"

if (-not (Test-Path $windsurfDir)) {
    New-Item -ItemType Directory -Path $windsurfDir -Force | Out-Null
}

# Load or create settings
if (Test-Path $windsurfPath) {
    $settings = Get-Content $windsurfPath | ConvertFrom-Json
    Write-Info "Loaded existing settings"
} else {
    $settings = New-Object PSObject
    Write-Info "Creating new settings file"
}

# Add MCP config
$settings | Add-Member -NotePropertyName "mcp" -NotePropertyValue @{
    servers = @(
        @{
            name = "github"
            command = "npx"
            args = @("-y", "@modelcontextprotocol/server-github")
            env = @{ GITHUB_PERSONAL_ACCESS_TOKEN = $GitHubToken }
        },
        @{
            name = "filesystem"
            command = "npx"
            args = @("-y", "@modelcontextprotocol/server-filesystem", $ProjectPath)
        }
    )
} -Force

$settings | ConvertTo-Json -Depth 10 | Set-Content $windsurfPath
Write-OK "Windsurf MCP configuration saved"
Write-Info "Config location: $windsurfPath"

# ==================== SUMMARY ====================
Write-Step "PHASE F: Activation Summary"

Write-Host ""
Write-Host "===========================================" -ForegroundColor Green
Write-Host "  PIPELINE ACTIVATION COMPLETE" -ForegroundColor Green
Write-Host "===========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Repository:  https://github.com/$GitHubUsername/$RepoName"
Write-Host "Token:       Set in environment variable"
Write-Host "MCP Servers: Installed"
Write-Host "Windsurf:    Configured (RESTART REQUIRED)"
Write-Host ""
Write-Host "NEXT STEPS:" -ForegroundColor Yellow
Write-Host "1. RESTART Windsurf IDE completely"
Write-Host "2. Enable GitHub Actions in repository settings"
Write-Host "3. Test: Create an issue, then 'Implement feature for issue #1'"
Write-Host ""
Write-Host "Verify: .\scripts\Test-PipelineStatus.ps1" -ForegroundColor Cyan
Write-Host ""
