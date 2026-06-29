# TASK CARD: W5-ADR-T1 — Tạo CI Edge Pipeline (ci-edge.yml)

**Wave:** 5 — CI Edge Pipeline
**Branch:** `feature/adr001-wave5-ci-edge`
**Estimated effort:** 1 hour
**Dependency:** Wave 4 complete ✅ (`docker-compose.edge.yml` + NatsSyncWorker tồn tại)

---

## 1. GOAL & CONTEXT

Tạo `.github/workflows/ci-edge.yml` — CI pipeline cho v2 Edge deployment.  
Pipeline này validate:
1. Code builds thành công (cùng solution)
2. Architecture tests pass (Rule H + Rule I)
3. `docker-compose.edge.yml` có cấu trúc đúng (lint/validate)
4. NatsSyncWorker unit tests pass

**Trigger:** Push to `feature/edge*`, manual dispatch (`workflow_dispatch`).  
**Không** run trên mọi push to main (để tránh duplicating ci.yml).

---

## 2. VERIFIED FACTS

| Fact | Source |
|------|--------|
| `ci.yml` dùng `ubuntu-latest`, `dotnet 8.0.x`, `VanAn.sln` | `.github/workflows/ci.yml` |
| Architecture tests: `6_Tests/VanAn.Architecture.Tests/` | `ci.yml` L92 |
| NuGet cache key: `hashFiles('**/Directory.Packages.props', '**/*.csproj')` | `ci.yml` L41 |
| `docker compose config --quiet` validate syntax (không cần Docker daemon) | Docker docs |
| Integration tests hiện tại disabled (`if: false`) | `ci.yml` L104 |
| `ci.yml` trigger: push `feature/**`, PR to `main/develop` | `ci.yml` L4-11 |

---

## 3. IMPLEMENTATION SPEC

### File tạo mới: `.github/workflows/ci-edge.yml`

```yaml
name: CI Edge (ADR-001 v2)

on:
  push:
    branches: [ 'feature/edge*', 'feature/adr001-wave*' ]
    paths-ignore:
      - '**.md'
      - 'docs/**'
      - '.devin/**'
  workflow_dispatch:
    inputs:
      reason:
        description: 'Reason for manual run'
        required: false
        default: 'Manual validation'

env:
  DOTNET_VERSION: '8.0.x'
  SOLUTION_PATH: 'VanAn.sln'

jobs:
  # Job 1: Build
  build:
    runs-on: ubuntu-latest
    timeout-minutes: 15

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Cache NuGet packages
        uses: actions/cache@v4
        with:
          path: ~/.nuget/packages
          key: ${{ runner.os }}-nuget-${{ hashFiles('**/Directory.Packages.props', '**/*.csproj') }}
          restore-keys: |
            ${{ runner.os }}-nuget-

      - name: Restore
        run: dotnet restore ${{ env.SOLUTION_PATH }}

      - name: Build
        run: dotnet build ${{ env.SOLUTION_PATH }} --no-restore --configuration Release

  # Job 2: Architecture Tests (includes Rule H + Rule I for ADR-001)
  architecture-tests:
    runs-on: ubuntu-latest
    needs: build
    timeout-minutes: 10

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Cache NuGet packages
        uses: actions/cache@v4
        with:
          path: ~/.nuget/packages
          key: ${{ runner.os }}-nuget-${{ hashFiles('**/Directory.Packages.props', '**/*.csproj') }}
          restore-keys: |
            ${{ runner.os }}-nuget-

      - name: Restore
        run: dotnet restore ${{ env.SOLUTION_PATH }}

      - name: Build
        run: dotnet build ${{ env.SOLUTION_PATH }} --no-restore --configuration Release

      - name: Architecture Tests (ADR-001 Rule H + Rule I)
        run: dotnet test 6_Tests/VanAn.Architecture.Tests/ --configuration Release --verbosity normal --logger trx

      - name: Upload Architecture Test Results
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: test-results-architecture-edge
          path: '**/TestResults/*.trx'
          retention-days: 30

  # Job 3: NatsSyncWorker Unit Tests
  nats-sync-worker-tests:
    runs-on: ubuntu-latest
    needs: build
    timeout-minutes: 10

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Cache NuGet packages
        uses: actions/cache@v4
        with:
          path: ~/.nuget/packages
          key: ${{ runner.os }}-nuget-${{ hashFiles('**/Directory.Packages.props', '**/*.csproj') }}
          restore-keys: |
            ${{ runner.os }}-nuget-

      - name: Restore
        run: dotnet restore ${{ env.SOLUTION_PATH }}

      - name: Build
        run: dotnet build ${{ env.SOLUTION_PATH }} --no-restore --configuration Release

      - name: NatsSyncWorker Unit Tests
        run: dotnet test 6_Tests/VanAn.Core.Tests/ --configuration Release --verbosity normal --logger trx --filter "NatsSyncWorker|NatsEventPublisher"

      - name: Upload Test Results
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: test-results-nats-edge
          path: '**/TestResults/*.trx'
          retention-days: 30

  # Job 4: Validate docker-compose.edge.yml structure
  validate-edge-compose:
    runs-on: ubuntu-latest
    needs: build
    timeout-minutes: 5

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Verify docker-compose.edge.yml exists
        run: |
          if [ ! -f "docker-compose.edge.yml" ]; then
            echo "ERROR: docker-compose.edge.yml not found — ADR-001 v2 Edge deployment missing"
            exit 1
          fi
          echo "docker-compose.edge.yml found ✓"

      - name: Verify required services in edge compose
        run: |
          EDGE_COMPOSE="docker-compose.edge.yml"
          
          # Check shoperp-nats-sync service
          if ! grep -q "shoperp-nats-sync" "$EDGE_COMPOSE"; then
            echo "ERROR: shoperp-nats-sync service missing from docker-compose.edge.yml"
            exit 1
          fi
          echo "shoperp-nats-sync service found ✓"
          
          # Check SQLite volume
          if ! grep -q "shoperp_sqlite_data" "$EDGE_COMPOSE"; then
            echo "ERROR: shoperp_sqlite_data volume missing from docker-compose.edge.yml"
            exit 1
          fi
          echo "shoperp_sqlite_data volume found ✓"
          
          # Check NATS broker still present
          if ! grep -q "image: nats:" "$EDGE_COMPOSE"; then
            echo "ERROR: NATS broker missing from docker-compose.edge.yml"
            exit 1
          fi
          echo "NATS broker found ✓"
          
          echo "All ADR-001 v2 Edge requirements validated ✓"

      - name: Verify docker-compose.prod.yml NOT modified (v1 SaaS preserved)
        run: |
          # docker-compose.prod.yml must NOT contain SQLite station or sync worker
          if grep -q "shoperp-nats-sync\|shoperp_sqlite_data\|shoperp-sqlite" "docker-compose.prod.yml"; then
            echo "ERROR: docker-compose.prod.yml was modified with edge components — v1 SaaS must remain unchanged"
            exit 1
          fi
          echo "docker-compose.prod.yml v1 SaaS integrity verified ✓"
```

---

## 4. HARDENING GATES

- [ ] Pipeline trigger: KHÔNG run trên every push to main (chỉ `feature/edge*`, `feature/adr001-wave*`, manual)
- [ ] Job `validate-edge-compose` verify cả `docker-compose.edge.yml` ĐỦ và `docker-compose.prod.yml` KHÔNG bị modify
- [ ] Architecture tests include Rule H (v1) + Rule I (v2 edge)
- [ ] NatsSyncWorker tests filter chính xác tên class
- [ ] `continue-on-error: false` (default) — mọi job phải pass

---

## 5. VALIDATION

```powershell
# Kiểm tra YAML syntax
Get-Content "c:/VibeCoding/Gemini_Windsurf/.github/workflows/ci-edge.yml"

# Test locally (nếu có act CLI)
# act workflow_dispatch -W .github/workflows/ci-edge.yml
```

---

## 6. EXIT CRITERIA

- [ ] `.github/workflows/ci-edge.yml` tạo mới
- [ ] 4 jobs: build, architecture-tests, nats-sync-worker-tests, validate-edge-compose
- [ ] Trigger: `feature/edge*`, `feature/adr001-wave*`, `workflow_dispatch`
- [ ] `validate-edge-compose` job check cả presence VÀ absence đúng service
- [ ] **ADR-001 OVERALL COMPLETE** khi wave này done:
  - docker-compose.edge.yml ✅
  - NatsSyncWorker ✅
  - ShopERP `--sync-worker` mode ✅
  - Architecture tests Rule H + Rule I ✅
  - CI edge pipeline ✅
