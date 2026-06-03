#Requires -Version 5.1
<#
.SYNOPSIS
    Check VanAn Pipeline activation status
.DESCRIPTION
    Validates all components of the AI-assisted development pipeline
.NOTES
    Run anytime to verify pipeline health
#>

[CmdletBinding()]
param()

# Colors
$Colors = @{
    Success = 'Green'
    Error = 'Red'
    Warning = 'Yellow'
    Info = 'Cyan'
    Header = 'Magenta'
}

function Write-Check {
    param(
        [string]$Name,
        [bool]$Passed,
        [string]$Message = "",
        [string]$Fix = ""
    )
    $icon = $Passed ? "✓" : "✗"
    $color = $Passed ? $Colors.Success : $Colors.Error
    Write-Host "  $icon $Name" -ForegroundColor $color -NoNewline
    if ($Message) {
        Write-Host " - $Message" -ForegroundColor Gray
    } else {
        Write-Host ""
    }
    if (-not $Passed -and $Fix) {
        Write-Host "    💡 Fix: $Fix" -ForegroundColor $Colors.Warning
    }
}

function Write-Section {
    param([string]$Title)
    Write-Host "`n$([char]0x2500)" -ForegroundColor $Colors.Header -NoNewline
    Write-Host " $Title " -ForegroundColor $Colors.Header -NoNewline
    Write-Host "$([char]0x2500)" -ForegroundColor $Colors.Header
}

Write-Host @"
╔══════════════════════════════════════════════════════════════╗
║           VanAn Pipeline Status Check                         ║
╚══════════════════════════════════════════════════════════════╝
"@ -ForegroundColor $Colors.Header

$ProjectPath = "c:\VibeCoding\Gemini_Windsurf"
Set-Location $ProjectPath

# ==================== GIT STATUS ====================
Write-Section "GIT CONFIGURATION"

try {
    $gitVersion = git --version 2>$null
    Write-Check -Name "Git installed" -Passed $true -Message $gitVersion
} catch {
    Write-Check -Name "Git installed" -Passed $false -Message "Not found" -Fix "Install from https://git-scm.com/download/win"
}

$remotes = git remote -v 2>$null
if ($remotes) {
    $originUrl = ($remotes | Select-String "origin.*fetch" | ForEach-Object { $_ -split '\s+' })[1]
    Write-Check -Name "Remote configured" -Passed $true -Message $originUrl
    
    # Test connection
    try {
        git ls-remote origin HEAD 2>$null | Out-Null
        Write-Check -Name "Remote accessible" -Passed $true
    } catch {
        Write-Check -Name "Remote accessible" -Passed $false -Message "Connection failed" -Fix "Check network or token"
    }
} else {
    Write-Check -Name "Remote configured" -Passed $false -Message "No origin remote" -Fix "Run: git remote add origin https://github.com/USER/REPO.git"
}

# ==================== GITHUB TOKEN ====================
Write-Section "GITHUB AUTHENTICATION"

if ($env:GITHUB_TOKEN) {
    Write-Check -Name "GITHUB_TOKEN set" -Passed $true -Message "Environment variable present"
    
    # Test token
    try {
        $response = Invoke-RestMethod -Uri "https://api.github.com/user" `
            -Headers @{ Authorization = "token $env:GITHUB_TOKEN" } `
            -ErrorAction Stop
        Write-Check -Name "Token valid" -Passed $true -Message "Authenticated as: $($response.login)"
    } catch {
        Write-Check -Name "Token valid" -Passed $false -Message "Authentication failed" -Fix "Generate new token at https://github.com/settings/tokens"
    }
} else {
    Write-Check -Name "GITHUB_TOKEN set" -Passed $false -Message "Not found" -Fix "[Environment]::SetEnvironmentVariable('GITHUB_TOKEN', 'ghp_...', 'User')"
}

# ==================== MCP SERVERS ====================
Write-Section "MCP SERVERS"

try {
    $nodeVersion = node --version 2>$null
    Write-Check -Name "Node.js installed" -Passed $true -Message $nodeVersion
} catch {
    Write-Check -Name "Node.js installed" -Passed $false -Message "Not found" -Fix "Install from https://nodejs.org/"
}

$githubMcp = npm list -g @modelcontextprotocol/server-github 2>&1
if ($githubMcp -notmatch "empty") {
    Write-Check -Name "GitHub MCP server" -Passed $true
} else {
    Write-Check -Name "GitHub MCP server" -Passed $false -Message "Not installed" -Fix "npm install -g @modelcontextprotocol/server-github"
}

$filesystemMcp = npm list -g @modelcontextprotocol/server-filesystem 2>&1
if ($filesystemMcp -notmatch "empty") {
    Write-Check -Name "Filesystem MCP server" -Passed $true
} else {
    Write-Check -Name "Filesystem MCP server" -Passed $false -Message "Not installed" -Fix "npm install -g @modelcontextprotocol/server-filesystem"
}

# ==================== WINDSURF CONFIG ====================
Write-Section "WINDSURF CONFIGURATION"

$windsurfSettingsPath = "$env:APPDATA\Windsurf\User\settings.json"
if (Test-Path $windsurfSettingsPath) {
    Write-Check -Name "Settings file exists" -Passed $true -Message $windsurfSettingsPath
    
    try {
        $settings = Get-Content $windsurfSettingsPath | ConvertFrom-Json
        if ($settings.mcp) {
            Write-Check -Name "MCP configured" -Passed $true -Message "$($settings.mcp.servers.Count) servers"
            
            foreach ($server in $settings.mcp.servers) {
                Write-Host "      → $($server.name)" -ForegroundColor Gray
            }
        } else {
            Write-Check -Name "MCP configured" -Passed $false -Message "No mcp section" -Fix "Run Activate-FullPipeline.ps1"
        }
    } catch {
        Write-Check -Name "Settings valid" -Passed $false -Message "JSON parse error" -Fix "Check $windsurfSettingsPath for syntax errors"
    }
} else {
    Write-Check -Name "Settings file exists" -Passed $false -Message "Not found" -Fix "Run Activate-FullPipeline.ps1"
}

# ==================== CODEOWNERS ====================
Write-Section "CODEOWNERS"

$codeownersPath = ".github/CODEOWNERS"
if (Test-Path $codeownersPath) {
    $content = Get-Content $codeownersPath -Raw
    Write-Check -Name "CODEOWNERS exists" -Passed $true
    
    if ($content -match '@owner|@domain-guardian|@devops') {
        Write-Check -Name "Placeholders replaced" -Passed $false -Message "Still has template placeholders" -Fix "Edit .github/CODEOWNERS, replace @owner with @yourusername"
    } else {
        $owners = [regex]::Matches($content, '@\w+') | ForEach-Object { $_.Value } | Select-Object -Unique
        Write-Check -Name "Placeholders replaced" -Passed $true -Message "Owners: $($owners -join ', ')"
    }
} else {
    Write-Check -Name "CODEOWNERS exists" -Passed $false -Message "Not found" -Fix "Create file at .github/CODEOWNERS"
}

# ==================== WORKFLOWS ====================
Write-Section "GITHUB ACTIONS WORKFLOWS"

$workflowsDir = ".github/workflows"
if (Test-Path $workflowsDir) {
    $workflows = Get-ChildItem $workflowsDir -Filter "*.yml"
    Write-Check -Name "Workflows directory" -Passed $true -Message "$($workflows.Count) workflows found"
    
    foreach ($wf in $workflows) {
        Write-Host "      → $($wf.Name)" -ForegroundColor Gray
    }
} else {
    Write-Check -Name "Workflows directory" -Passed $false -Message "Not found" -Fix "Directory should exist with ci.yml, e2e.yml, pr-check.yml"
}

# ==================== PROJECT STRUCTURE ====================
Write-Section "PROJECT STRUCTURE"

$requiredDirs = @(
    "docs/knowledge-base",
    ".windsurf/rules",
    "mcp"
)

foreach ($dir in $requiredDirs) {
    if (Test-Path $dir) {
        Write-Check -Name "$dir" -Passed $true
    } else {
        Write-Check -Name "$dir" -Passed $false -Message "Missing" -Fix "Directory should exist for full pipeline"
    }
}

$requiredFiles = @(
    "docs/knowledge-base/00-core/PROJECT_CONTEXT.md",
    ".windsurf/rules/.windsurfrules",
    "mcp/github-mcp/config.json",
    "mcp/filesystem-mcp/config.json"
)

foreach ($file in $requiredFiles) {
    if (Test-Path $file) {
        Write-Check -Name (Split-Path $file -Leaf) -Passed $true
    } else {
        Write-Check -Name (Split-Path $file -Leaf) -Passed $false -Message "Missing" -Fix "File required for pipeline"
    }
}

# ==================== SUMMARY ====================
Write-Section "SUMMARY"

Write-Host "`nUse this template in new chat sessions:" -ForegroundColor $Colors.Info
Write-Host @"

  Implement [feature] for issue #[NUMBER]
  Context: @c:\VibeCoding\Gemini_Windsurf\docs\knowledge-base\00-core\PROJECT_CONTEXT.md
  Mode: [IMPLEMENT | FIX_ONLY | ANALYZE]

"@ -ForegroundColor Gray

Write-Host "Pipeline components status check complete at $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor $Colors.Success
