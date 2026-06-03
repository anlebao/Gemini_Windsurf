# Phase 1-4 Full Activation Plan

> **Option A: Complete Automation Setup**  
> Biến configuration thành active pipeline

---

## Executive Summary

| Item | Value |
|------|-------|
| **Goal** | Kích hoạt full AI-assisted development pipeline |
| **Approach** | Option A - Full Automation |
| **Estimated Time** | 2-3 hours |
| **Risk Level** | Low (configurations đã tested locally) |
| **Prerequisites** | GitHub account, npm, git, GitHub token |

---

## Pre-Flight Checklist

### ✅ Prerequisites (Must Have Before Starting)

| # | Item | Verify Command | Status |
|---|------|--------------|--------|
| 1 | Git installed | `git --version` | ⬜ |
| 2 | Node.js 20+ | `node --version` | ⬜ |
| 3 | npm installed | `npm --version` | ⬜ |
| 4 | GitHub account | https://github.com/login | ⬜ |
| 5 | GitHub CLI (optional) | `gh --version` | ⬜ |
| 6 | PowerShell 7+ | `$PSVersionTable.PSVersion` | ⬜ |

### 📋 Information Needed

| # | Item | Where to Get | Value |
|---|------|--------------|-------|
| 1 | GitHub username | Profile page | `___________` |
| 2 | Repository name | Quyết định | `Gemini_Windsurf` |
| 3 | GitHub token | Settings → Developer settings | `ghp_***` (store securely) |
| 4 | Default branch | `main` or `develop` | `___________` |

---

## Phase A: GitHub Repository Setup (30 min)

### Step A1: Create Remote Repository

**Option A1a: GitHub Web (Recommended for first time)**
1. Go to https://github.com/new
2. Repository name: `Gemini_Windsurf`
3. Description: `ShopERP - VAT 2026 Compliant POS & Accounting System`
4. Visibility: Private (recommend) or Public
5. **DO NOT** initialize with README (we have local files)
6. **DO NOT** add .gitignore (we have existing)
7. **DO NOT** add license (optional)
8. Click "Create repository"
9. Copy the "push an existing repository" commands

**Option A1b: GitHub CLI**
```powershell
gh repo create Gemini_Windsurf --private --source=. --remote=origin --push
```

### Step A2: Configure Local Git

```powershell
# Navigate to project
cd c:\VibeCoding\Gemini_Windsurf

# Check current git status
git status

# Add remote (replace YOUR_USERNAME)
git remote add origin https://github.com/YOUR_USERNAME/Gemini_Windsurf.git

# Verify remote
git remote -v
```

### Step A3: First Push

```powershell
# Check what's committed
git log --oneline -5

# If nothing committed yet:
git add .
git commit -m "Initial commit: Phases 1-4 complete (Knowledge Base, CI/CD, MCP)"

# Push to main
git push -u origin main

# Verify on GitHub
git remote get-url origin
# Open URL in browser, verify files visible
```

### Step A4: Protect Main Branch

**GitHub → Repository → Settings → Branches → Add rule**

| Setting | Value |
|---------|-------|
| Branch name pattern | `main` |
| Require pull request reviews | ✅ (1 reviewer) |
| Dismiss stale reviews | ✅ |
| Require status checks | ✅ (see Step C4) |
| Include administrators | ✅ |

### ✅ Phase A Exit Criteria

- [ ] Repository visible on GitHub
- [ ] All files pushed (check file count matches local)
- [ ] Default branch is `main`
- [ ] Branch protection rules configured

---

## Phase B: GitHub Token & Security (20 min)

### Step B1: Generate Personal Access Token

1. GitHub → Settings → Developer settings → Personal access tokens → Tokens (classic)
2. Click "Generate new token (classic)"
3. **Note**: `VanAn-MCP-Integration`
4. **Expiration**: 90 days (recommend) or No expiration
5. **Scopes** (select carefully):
   - ✅ `repo` (Full control of private repositories)
   - ✅ `workflow` (Update GitHub Action workflows)
   - ✅ `write:packages` (if using GitHub Packages)
   - ❌ NOT `delete_repo` (security)
   - ❌ NOT `admin:org` (not needed)

6. Click "Generate token"
7. **COPY TOKEN NOW** (won't show again)
8. Store in password manager: `ghp_xxxxxxxxxxxx`

### Step B2: Configure CODEOWNERS

```powershell
# Edit CODEOWNERS
notepad .github/CODEOWNERS
```

**Replace placeholders:**
```diff
- * @owner
+ * @YOUR_USERNAME

- 1_Shared/Domain.cs @domain-guardian
+ 1_Shared/Domain.cs @YOUR_USERNAME

- .github/workflows/ @devops
+ .github/workflows/ @YOUR_USERNAME
```

**Full example** (replace all @placeholders):
```
# Default owner for everything
* @johndoe

# Domain Layer
1_Shared/Domain.cs @johndoe
1_Shared/BusinessRules.cs @johndoe

# CI/CD
.github/workflows/ @johndoe
.github/actions/ @johndoe

# Documentation
docs/decisions/ @johndoe

# Testing
6_Tests/ @johndoe
6_Testing/ @johndoe
```

### Step B3: Commit CODEOWNERS

```powershell
git add .github/CODEOWNERS
git commit -m "chore: update CODEOWNERS with actual usernames"
git push origin main
```

### Step B4: Secure Token Storage

**Windows - Environment Variable:**
```powershell
# User-level (recommended)
[Environment]::SetEnvironmentVariable("GITHUB_TOKEN", "ghp_YOUR_TOKEN", "User")

# Verify
$env:GITHUB_TOKEN
```

**Windows - .env file (Alternative):**
```powershell
# Create .env file (add to .gitignore!)
"GITHUB_TOKEN=ghp_YOUR_TOKEN" | Out-File -FilePath .env -Encoding utf8

# Verify .gitignore has .env
cat .gitignore | Select-String "\.env"
```

### ✅ Phase B Exit Criteria

- [ ] GitHub token generated with correct scopes
- [ ] Token stored securely (env var or .env)
- [ ] CODEOWNERS updated with real usernames
- [ ] CODEOWNERS pushed to GitHub

---

## Phase C: GitHub Actions Activation (20 min)

### Step C1: Enable GitHub Actions

1. GitHub → Repository → Settings → Actions → General
2. **Actions permissions**: 
   - ✅ "Allow all actions and reusable workflows"
3. **Workflow permissions**:
   - ✅ "Read and write permissions"
   - ✅ "Allow GitHub Actions to create and approve pull requests"
4. Click "Save"

### Step C2: Verify Workflow Files

```powershell
# Check workflows exist
ls .github/workflows/
# Expected: ci.yml, e2e.yml, pr-check.yml

# Validate YAML syntax (optional)
npx yaml-lint .github/workflows/ci.yml
```

### Step C3: Trigger First Workflow

**Option 1: Push trigger**
```powershell
# Make trivial change
echo "# Pipeline activated" >> README.md
git add README.md
git commit -m "chore: trigger CI pipeline"
git push origin main
```

**Option 2: Manual trigger (if workflow_dispatch enabled)**
- GitHub → Actions → CI → Run workflow

### Step C4: Verify CI Status

1. GitHub → Actions → CI workflow
2. Wait for workflow to complete
3. Check all jobs:
   - [ ] build
   - [ ] architecture-tests
   - [ ] guard-check
   - [ ] code-quality

### Step C5: Configure Status Checks (Branch Protection)

GitHub → Settings → Branches → Edit `main` rule:

Add status checks:
- `build`
- `architecture-tests`
- `pr-metadata` (from pr-check.yml)

### ✅ Phase C Exit Criteria

- [ ] GitHub Actions enabled in settings
- [ ] CI workflow triggered and visible
- [ ] At least one successful run
- [ ] Status checks configured in branch protection

---

## Phase D: MCP Server Installation (30 min)

### Step D1: Verify Node.js Installation

```powershell
node --version  # Should be v20.x or higher
npm --version   # Should be 10.x or higher
```

If not installed: https://nodejs.org/ (LTS version)

### Step D2: Install MCP Servers

```powershell
# Install GitHub MCP server globally
npm install -g @modelcontextprotocol/server-github

# Install Filesystem MCP server globally  
npm install -g @modelcontextprotocol/server-filesystem

# Verify installation
npx @modelcontextprotocol/server-github --version
npx @modelcontextprotocol/server-filesystem --version
```

### Step D3: Test MCP Servers Manually

```powershell
# Test with env var
$env:GITHUB_TOKEN = "ghp_YOUR_TOKEN"
npx @modelcontextprotocol/server-github
# Should start without errors (Ctrl+C to exit)

# Test filesystem
npx @modelcontextprotocol/server-filesystem c:\VibeCoding\Gemini_Windsurf
# Should start without errors (Ctrl+C to exit)
```

### Step D4: Configure Windsurf Settings

**Locate Windsurf settings:**
- Windows: `%APPDATA%\Windsurf\User\settings.json`
- Or: Windsurf → File → Preferences → Settings → Open Settings (JSON)

**Add MCP configuration:**
```json
{
  "mcp": {
    "servers": [
      {
        "name": "github",
        "command": "npx",
        "args": ["-y", "@modelcontextprotocol/server-github"],
        "env": {
          "GITHUB_PERSONAL_ACCESS_TOKEN": "${env:GITHUB_TOKEN}"
        }
      },
      {
        "name": "filesystem",
        "command": "npx",
        "args": ["-y", "@modelcontextprotocol/server-filesystem", "c:\\VibeCoding\\Gemini_Windsurf"]
      }
    ]
  }
}
```

**Alternative (if env var substitution doesn't work):**
```json
{
  "mcp": {
    "servers": [
      {
        "name": "github",
        "command": "npx",
        "args": ["-y", "@modelcontextprotocol/server-github"],
        "env": {
          "GITHUB_PERSONAL_ACCESS_TOKEN": "ghp_YOUR_ACTUAL_TOKEN_HERE"
        }
      }
    ]
  }
}
```

### Step D5: Restart Windsurf

1. Close all Windsurf windows
2. Reopen Windsurf
3. MCP servers should auto-start

### ✅ Phase D Exit Criteria

- [ ] MCP servers installed globally
- [ ] Manual test passes (servers start)
- [ ] Windsurf settings configured
- [ ] Windsurf restarted

---

## Phase E: End-to-End Validation (30 min)

### Step E1: Create Test Issue

GitHub → Issues → New issue:
- Title: `[TEST] Verify MCP Integration`
- Body: `
This is a test issue to verify AI can read GitHub issues via MCP.

## Expected Behavior
AI should be able to:
1. Read this issue
2. Create a branch
3. Make a trivial change
4. Create a PR

## Acceptance Criteria
- [ ] Branch created
- [ ] Commit pushed
- [ ] PR created with description
`
- Label: `test`
- Assign: yourself

### Step E2: AI Test - Read Issue

**In Windsurf chat:**
```
Read GitHub issue #[NUMBER] and summarize the requirements.
Use MCP if available.
```

**Expected:** AI returns issue content

### Step E3: AI Test - Create Branch

```
Create a branch named "test/mcp-integration" for issue #[NUMBER].
```

**Expected:** Branch appears in GitHub

### Step E4: AI Test - Commit & PR

```
Make a trivial change (e.g., add a comment to README) 
and commit to branch test/mcp-integration with message "test: verify mcp".
Then create a PR linking to issue #[NUMBER].
```

**Expected:**
- Commit visible in branch
- PR created with:
  - Title referencing issue
  - Description with "Closes #[NUMBER]"
  - CI checks running

### Step E5: Verify CI on PR

1. Open PR on GitHub
2. Check "Checks" tab
3. Verify running:
   - PR check (title, files)
   - CI (build, tests)
4. Wait for completion

### Step E6: Merge & Cleanup

```powershell
# After PR merged
git checkout main
git pull origin main

# Delete local branch
git branch -d test/mcp-integration

# Close test issue
# Delete remote branch (GitHub UI)
```

### ✅ Phase E Exit Criteria

- [ ] AI successfully read issue
- [ ] Branch created via MCP
- [ ] Commit pushed via MCP
- [ ] PR created via MCP
- [ ] CI passed on PR
- [ ] PR merged successfully

---

## Post-Activation: Operating Procedures

### Daily Workflow (Now Active)

```
New chat session:
    ↓
Mention @PROJECT_CONTEXT.md
    ↓
"Implement feature for issue #42"
    ↓
├─ AI reads issue (MCP GitHub)
├─ AI loads domain docs
├─ AI implements (mode: IMPLEMENT)
├─ AI commits (MCP GitHub)
├─ AI creates PR (MCP GitHub)
    ↓
CI auto-runs
    ↓
You review & merge
```

### MCP Status Check Command

Add to PowerShell profile (`$PROFILE`):

```powershell
function Test-VanAnPipeline {
    Write-Host "=== VanAn Pipeline Status ===" -ForegroundColor Cyan
    
    # Check GitHub
    Write-Host "`nGitHub Remote:" -ForegroundColor Yellow
    git remote -v
    
    # Check MCP servers
    Write-Host "`nMCP Servers:" -ForegroundColor Yellow
    $github = Get-Command npx -ErrorAction SilentlyContinue
    if ($github) { Write-Host "  ✓ npx available" -ForegroundColor Green }
    
    # Check env var
    Write-Host "`nEnvironment:" -ForegroundColor Yellow
    if ($env:GITHUB_TOKEN) { 
        Write-Host "  ✓ GITHUB_TOKEN set" -ForegroundColor Green 
    } else { 
        Write-Host "  ✗ GITHUB_TOKEN missing" -ForegroundColor Red 
    }
    
    # Check Windsurf config
    Write-Host "`nWindsurf Config:" -ForegroundColor Yellow
    $wsSettings = "$env:APPDATA\Windsurf\User\settings.json"
    if (Test-Path $wsSettings) {
        $config = Get-Content $wsSettings | ConvertFrom-Json
        if ($config.mcp) {
            Write-Host "  ✓ MCP configured" -ForegroundColor Green
            $config.mcp.servers | ForEach-Object { 
                Write-Host "    - $($_.name)" -ForegroundColor Gray
            }
        } else {
            Write-Host "  ✗ MCP not configured" -ForegroundColor Red
        }
    }
}
```

---

## Rollback Plan

### If Something Breaks

| Problem | Rollback Action |
|---------|-----------------|
| GitHub Actions fail | Disable in Settings → Actions |
| MCP not working | Remove from Windsurf settings |
| Token exposed | Revoke in GitHub → Regenerate |
| Bad commit pushed | `git revert HEAD` |
| Wrong branch protection | Edit in Settings → Branches |

### Emergency Stop

```powershell
# Disable all automation
git remote remove origin
Remove-Item Env:\GITHUB_TOKEN
# Remove MCP config from Windsurf settings
```

---

## Summary Timeline

| Phase | Duration | Cumulative |
|-------|----------|------------|
| A: GitHub Repo | 30 min | 30 min |
| B: Token & Security | 20 min | 50 min |
| C: Actions | 20 min | 70 min |
| D: MCP Install | 30 min | 100 min |
| E: E2E Test | 30 min | 130 min |
| **Total** | **~2.5 hours** | |

---

## Approval Checklist

Before starting, confirm:

- [ ] All prerequisites met
- [ ] GitHub token scopes understood
- [ ] 2-3 hours uninterrupted time available
- [ ] Rollback plan reviewed
- [ ] Ready to proceed with Phase A

---

**Next Action:** Review this plan, then run `Phase-A-Setup.ps1` (script to be created).

*Plan Version: 1.0*  
*Created: June 1, 2026*
