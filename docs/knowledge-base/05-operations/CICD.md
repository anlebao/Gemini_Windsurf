# CI/CD Documentation

> **ShopERP Continuous Integration & Deployment**

## Overview

ShopERP sử dụng GitHub Actions cho CI/CD pipeline với các workflow tách biệt cho build, test, và deployment.

## Pipeline Architecture

```
Pull Request / Push
       ↓
┌─────────────────┐
│  PR Validation  │  ───  pr-check.yml
│  - Title check  │
│  - File analysis│
└────────┬────────┘
         ↓
┌─────────────────┐
│  Build & Unit   │  ───  ci.yml
│  - Restore      │
│  - Build        │
│  - Unit tests   │
└────────┬────────┘
         ↓
┌─────────────────┐
│  Quality Gates  │  ───  ci.yml (parallel)
│  - Architecture │
│  - Guard check  │
│  - Format check │
└────────┬────────┘
         ↓
┌─────────────────┐
│  E2E Tests      │  ───  e2e.yml (conditional)
│  - Playwright   │
│  - UI flows     │
└────────┬────────┘
         ↓
    [Merge]
         ↓
┌─────────────────┐
│  Deploy         │  ───  Future: deploy.yml
│  - Staging      │
│  - Production   │
└─────────────────┘
```

## Workflows

### 1. CI Workflow (.github/workflows/ci.yml)

**Triggers:**
- Push to `main` or `develop`
- Pull request to `main` or `develop`
- Ignores: `**.md`, `docs/**`, `.windsurf/**`

**Jobs:**

| Job | Purpose | Runner | Time |
|-----|---------|--------|------|
| build | Build + Unit tests | ubuntu-latest | 15 min |
| architecture-tests | Architecture rules validation | ubuntu-latest | 10 min |
| guard-check | Run guard-check.ps1 | windows-latest | 10 min |
| code-quality | Format verification | ubuntu-latest | 10 min |

**Artifacts:**
- Test results (TRX format)
- Guard check output

### 2. E2E Workflow (.github/workflows/e2e.yml)

**Triggers:**
- Push to `main` (path-filtered: WebApps, UI.Platform, e2e-tests)
- Pull request to `main` (path-filtered)
- Manual dispatch (workflow_dispatch)

**Jobs:**

| Job | Purpose | Time |
|-----|---------|------|
| build-apps | Build và publish apps | 20 min |
| setup-playwright | Install browsers | 10 min |
| e2e-tests | Run Playwright tests | 30 min |
| quality-gate | Summary report | 5 min |

**Inputs (workflow_dispatch):**
- `spec`: Specific test file (optional)
- `browser`: chromium, firefox, webkit

### 3. PR Check (.github/workflows/pr-check.yml)

**Triggers:**
- PR opened, synchronized, reopened, ready_for_review

**Jobs:**

| Job | Purpose | Details |
|-----|---------|---------|
| pr-metadata | Validate PR title, branch naming | Must follow conventional commits |
| changed-files | Detect critical changes | Domain, workflow, ADR |
| build-verify | Quick build verification | Reuse ci.yml logic |
| docs-check | Verify ADR documentation | Check README.md updated |
| pr-summary | Generate PR summary | Review requirements |

**PR Title Convention:**
- `feat:` New feature
- `fix:` Bug fix
- `docs:` Documentation
- `refactor:` Code refactoring
- `test:` Tests
- `chore:` Maintenance
- `adr:` Architecture decision

## Composite Action

### setup-vanan (.github/actions/setup-vanan/)

Reusable action cho setup môi trường:

```yaml
- uses: ./.github/actions/setup-vanan
  with:
    dotnet-version: '8.0.x'
    node-version: '20.x'
    install-playwright: 'true'
    playwright-browsers: 'chromium'
```

**Features:**
- .NET SDK setup
- Node.js setup
- NuGet package caching
- Playwright browser caching
- Environment verification

## CODEOWNERS

Automatic review assignment based on file paths:

| Path | Owner | Note |
|------|-------|------|
| `1_Shared/Domain.cs` | @domain-guardian | Critical domain layer |
| `.github/workflows/` | @devops | CI/CD changes |
| `UI.Platform/` | @ui-lead | UI components |
| `docs/decisions/` | @architect | ADR changes |
| `6_Tests/` | @qa-lead | Testing |

**To use:** Replace `@owner` với actual GitHub usernames trong `.github/CODEOWNERS`

## Status Checks

Required checks cho PR merge:

1. **build** - Build và unit tests pass
2. **architecture-tests** - Architecture rules pass
3. **pr-metadata** - PR title và branch naming valid

Optional checks:
- **guard-check** - Windows-specific validation
- **e2e-tests** - UI tests (path-filtered)
- **code-quality** - Format check (continue-on-error)

## Troubleshooting

### Build Failures

```bash
# Local verification
dotnet restore VanAn.sln
dotnet build VanAn.sln --configuration Release
dotnet test 6_Tests/VanAn.Core.Tests/
```

### E2E Failures

```bash
cd 6_Testing
npm ci
npx playwright install
npx playwright test --project=chromium
```

### Guard Check Failures

```powershell
# Run locally
./guard-check.ps1
```

## Performance Optimization

### Caching Strategy

| Cache | Path | Key |
|-------|------|-----|
| NuGet | `~/.nuget/packages` | `hashFiles('**/Directory.Packages.props')` |
| Node | `~/.npm` | `hashFiles('6_Testing/package-lock.json')` |
| Playwright | `~/.cache/ms-playwright` | Playwright version + lock file |

### Timeouts

| Job | Timeout | Rationale |
|-----|---------|-----------|
| build | 15 min | Build + unit tests |
| e2e-tests | 30 min | Full browser test suite |
| architecture-tests | 10 min | Static analysis |

## Future Enhancements

### Planned Workflows

1. **deploy-staging.yml** - Auto deploy to staging on merge
2. **deploy-production.yml** - Manual production deployment
3. **security-scan.yml** - Dependency vulnerability scanning
4. **performance.yml** - Load testing với k6

### Infrastructure

- Self-hosted runners for Windows-specific tests
- Testcontainers cho integration tests
- Parallel E2E execution across multiple browsers

## References

- `.github/workflows/` - All workflow definitions
- `.github/actions/setup-vanan/` - Composite action
- `.github/CODEOWNERS` - Review assignments
- `guard-check.ps1` - Custom validation script

---

*Version: 1.0*  
*Last Updated: June 1, 2026*
