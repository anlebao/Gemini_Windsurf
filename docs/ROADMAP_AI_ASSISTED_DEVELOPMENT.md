# ShopERP - AI-Assisted Development Roadmap

> **Version:** 1.0  
> **Created:** June 1, 2026  
> **Status:** Draft - Pending Review  
> **Goal:** Transform ShopERP from AI-assisted coding to AI-driven Software Factory

---

## Executive Summary

This roadmap builds upon the existing **Windsurf Knowledge Base** (`.windsurf/rules/`, `workflows/`, `skills/`) to create a complete AI-assisted development pipeline. We do NOT start from scratch—we **augment** what already works.

**Current State:** Giai đoạn 2-3 (Knowledge Base exists, need Domain Docs + AGENTS.md + CI/CD)  
**Target State:** Giai đoạn 10 (Software Factory with Multi-Agent orchestration)

---

## Phase Overview

| Phase | Duration | Goal | Key Deliverable | Status |
|-------|----------|------|-----------------|--------|
| 1 | 1-2 tuần | Knowledge Base Consolidation | Domain Docs + ADRs | ⏳ Pending |
| 2 | 1 tuần | AI Standardization | AGENTS.md + PROJECT_CONTEXT.md | ⏳ Pending |
| 3 | 1 tuần | CI/CD Foundation | GitHub Actions | ⏳ Pending |
| 4 | 1 tuần | MCP Integration | GitHub MCP | ⏳ Pending |
| 5 | 2 tuần | Documentation Automation | Auto-update KB | ⏳ Pending |
| 6 | 2-4 tuần | Project Memory | PostgreSQL + History | ⏳ Pending |
| 7 | 2-4 tuần | Semantic Search | Qdrant Integration | ⏳ Pending |
| 8 | 1-2 tháng | Agent Layer | Multi-Agent (Semantic Kernel) | ⏳ Pending |
| 9 | 1 tháng | Software Pipeline | Full workflow orchestration | ⏳ Pending |
| 10 | Ongoing | Software Factory | OpenClaw/LangGraph | ⏳ Future |

---

## Phase 1: Knowledge Base Consolidation (1-2 tuần)

### Mục tiêu
Biến kiến thức trong đầu người và chat history thành **tài sản dự án**. Tận dụng `.windsurf/` hiện tại.

### Deliverables

```
docs/
├── decisions/
│   ├── README.md                           # Index tất cả ADRs
│   ├── ADR-Template.md                     # Template chuẩn
│   ├── ADR-001-SQLite-Offline-First.md     # Quyết định SQLite + NATS
│   ├── ADR-002-Multi-Tenancy-Everywhere.md # TenantId mandatory
│   ├── ADR-003-Accounting-Immutability.md  # Entry append-only
│   ├── ADR-004-UI-Platform-Mandatory.md    # No custom CSS
│   └── ADR-005-Playwright-Isolation.md     # E2E governance
│
├── knowledge-base/
│   ├── 00-core/
│   │   ├── Vision.md                       # Product vision
│   │   └── PROJECT_CONTEXT.md              # Context tổng hợp
│   │
│   ├── 02-domains/
│   │   ├── Order.md                        # Order aggregate
│   │   ├── Inventory.md                    # Inventory domain
│   │   ├── Payment.md                      # Payment domain
│   │   └── Accounting.md                   # Accounting domain
│   │
│   ├── 03-architecture/
│   │   ├── SystemOverview.md               # High-level architecture
│   │   └── DataFlow.md                     # SQLite → NATS → PostgreSQL
│   │
│   ├── 04-standards/
│   │   ├── CodingStandards.md              # Reference .editorconfig + .windsurfrules
│   │   └── ReviewChecklist.md              # PR review checklist
│   │
│   └── 08-ai/
│       ├── AGENTS.md                       # Agent definitions
│       └── windsurf-integration.md           # Link đến .windsurf/
│
└── .windsurf/                              # GIỮ NGUYÊN - Đã tối ưu
    ├── rules/.windsurfrules                # Core governance v7.0
    ├── workflows/                          # 12 workflows
    └── skills/                             # 21 skills
```

### Week 1 Tasks

| Day | Task | File | Owner |
|-----|------|------|-------|
| 1 | Create decisions/ structure | `docs/decisions/README.md` | AI + User |
| 2 | Write ADR-001 (SQLite+NATS) | `docs/decisions/ADR-001-SQLite-Offline-First.md` | AI |
| 3 | Write ADR-002 (Multi-tenancy) | `docs/decisions/ADR-002-Multi-Tenancy-Everywhere.md` | AI |
| 4 | Write ADR-003 (Accounting) | `docs/decisions/ADR-003-Accounting-Immutability.md` | AI |
| 5 | Write ADR-004 (UI Platform) | `docs/decisions/ADR-004-UI-Platform-Mandatory.md` | AI |
| 6 | Write ADR-005 (Playwright) | `docs/decisions/ADR-005-Playwright-Isolation.md` | AI |
| 7 | Review all ADRs | All ADRs | User |

### Week 2 Tasks

| Day | Task | File | Owner |
|-----|------|------|-------|
| 8 | Create knowledge-base structure | Folders | AI |
| 9 | Write Vision.md | `docs/knowledge-base/00-core/Vision.md` | User |
| 10 | Write PROJECT_CONTEXT.md | `docs/knowledge-base/00-core/PROJECT_CONTEXT.md` | AI |
| 11 | Extract Order domain | `docs/knowledge-base/02-domains/Order.md` | AI |
| 12 | Extract Inventory domain | `docs/knowledge-base/02-domains/Inventory.md` | AI |
| 13 | Extract Payment domain | `docs/knowledge-base/02-domains/Payment.md` | AI |
| 14 | Extract Accounting domain | `docs/knowledge-base/02-domains/Accounting.md` | AI |

### Success Criteria
- [ ] 5 ADRs viết xong và được approve
- [ ] Domain docs cover 4 core domains
- [ ] PROJECT_CONTEXT.md reference đúng `.windsurf/` structure
- [ ] User confirm: "AI hiểu domain, kiến trúc, business rules"

---

## Phase 2: AI Standardization (1 tuần)

### Mục tiêu
Biến Windsurf thành thành viên chính thức của team. Định nghĩa agents rõ ràng.

### Deliverables

```
docs/knowledge-base/08-ai/
├── AGENTS.md
├── windsurf-integration.md
└── prompts/
    ├── feature-development.md
    ├── bug-fix.md
    └── code-review.md
```

### AGENTS.md Structure

Từ 21 skills + 12 workflows hiện tại, derive thành agents:

```markdown
# ShopERP AI Agents

## 1. Build Fixer Agent
- **Mode:** FIX_ONLY
- **Trigger:** Build errors > 0
- **Skills:** pattern-based-fixing, build-error-analysis, domain-integrity-validation
- **Workflow:** `Fix_Errors.md`
- **Constraints:** 
  - Max 3 files per batch
  - Never modify Domain.cs
  - Pattern-based fixing only

## 2. Feature Developer Agent
- **Mode:** ANALYZE → IMPLEMENT
- **Trigger:** New feature request
- **Skills:** [feature-specific from table]
- **Workflow:** `newfeaturebuild.md`
- **Constraints:
  - Phase isolation: Domain → App → Infra → UI
  - Playwright disabled during Steps 1-6
  - Max 10 files per phase

## 3. Playwright Guardian Agent
- **Mode:** TRIAGE_ONLY / VALIDATE_ONLY / FIX_PLAYWRIGHT
- **Trigger:** E2E test failures
- **Skills:** playwright_guard, playwright_cost_optimizer
- **Workflows:** playwright_triage.md → playwright_fix.md
- **Constraints:**
  - NEVER during IMPLEMENT mode
  - Max 1 rerun per spec
  - Cost tiers enforced

## 4. Domain Guardian Agent
- **Mode:** REVIEW_ONLY (default)
- **Trigger:** Any Domain layer change
- **Skills:** domain-integrity-validation
- **Hard Stops:**
  - Domain modification to fix UI/Service issue
  - AccountingEntry immutability violation
  - Multi-tenancy bypass

## 5. Refactoring Safety Agent
- **Mode:** ANALYZE → IMPLEMENT (with extra validation)
- **Trigger:** Refactoring request
- **Skills:** system-refactor-safety, test-strategy-planning
- **Constraints:**
  - Tests first (Retrofit TDD)
  - No public API change without approval
```

### Skills to Agents Mapping

| Feature Type | Agent | Active Skills |
|--------------|-------|---------------|
| Accounting UI | Feature Developer | accounting-ui-implementation, ui-platform-migration, domain-integrity-validation |
| UI Platform | Feature Developer | ui-platform-migration, ui-platform-compliance-review |
| Outbox/NATS | Feature Developer | outbox-pattern-implementation, nats-sqlite-deployment-validation |
| E-Invoice | Feature Developer | einvoice-integration, ui-platform-compliance-review |
| Period Closing | Feature Developer | period-closing-audit-trail, domain-integrity-validation |
| Build Errors | Build Fixer | pattern-based-fixing, build-error-analysis, domain-integrity-validation |
| Test Failures | Build Fixer + Playwright | test-system-upgrade, pattern-based-fixing |

### Week 2 Tasks (chi tiết)

| Day | Task | Output |
|-----|------|--------|
| 1 | Derive agents từ skills | AGENTS.md draft |
| 2 | Define agent workflows | AGENTS.md workflows section |
| 3 | Create hand-off protocols | AGENTS.md transitions |
| 4 | Write feature-dev prompt | `prompts/feature-development.md` |
| 5 | Write bug-fix prompt | `prompts/bug-fix.md` |
| 6 | Test với real task | Validation |
| 7 | Review & refine | Final AGENTS.md |

### Success Criteria
- [ ] AGENTS.md định nghĩa rõ 5 agents
- [ ] Mỗi agent có: Mode, Skills, Workflow, Constraints
- [ ] Hand-off protocols rõ ràng
- [ ] Test với 1 task thực tế → Code sinh ra nhất quán

---

## Phase 3: GitHub Pipeline (1 tuần)

### Mục tiêu
Tự động hóa build và test. Mọi commit đều được kiểm tra.

### Deliverables

```
.github/
├── workflows/
│   ├── ci.yml                    # Build + Unit Test
│   ├── e2e.yml                   # Playwright tests
│   └── pr-check.yml              # PR validation
├── actions/
│   └── setup-vanan/              # Composite action
│       ├── action.yml
│       └── setup.ps1
└── CODEOWNERS                    # Review assignments
```

### CI Workflow (ci.yml)

```yaml
name: CI

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main, develop ]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      
      - name: Restore
        run: dotnet restore VanAn.sln
      
      - name: Build
        run: dotnet build VanAn.sln --no-restore --configuration Release
      
      - name: Unit Tests
        run: dotnet test 6_Tests/VanAn.Core.Tests/ --no-build --verbosity normal

  architecture-tests:
    runs-on: ubuntu-latest
    needs: build
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - run: dotnet test 6_Tests/VanAn.Architecture.Tests/

  guard-check:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - name: Run Guard Check
        shell: pwsh
        run: ./guard-check.ps1
```

### Week 3 Tasks

| Day | Task | File |
|-----|------|------|
| 1 | Create .github/ structure | Folders |
| 2 | Write ci.yml | `.github/workflows/ci.yml` |
| 3 | Write e2e.yml | `.github/workflows/e2e.yml` |
| 4 | Setup Playwright in CI | E2E workflow |
| 5 | Test với PR | Validation |
| 6 | Add CODEOWNERS | `.github/CODEOWNERS` |
| 7 | Document CI/CD | `docs/knowledge-base/05-operations/CICD.md` |

### Success Criteria
- [ ] Build pass trên mọi PR
- [ ] Unit tests chạy tự động
- [ ] Architecture tests chạy tự động
- [ ] E2E tests chạy được (có thể flaky ban đầu)
- [ ] guard-check.ps1 chạy trong CI

---

## Phase 4: MCP Integration (1 tuần)

### Mục tiêu
Cho AI làm việc trực tiếp với dự án: đọc Issue, tạo Branch, Commit, tạo PR.

### Deliverables

```
mcp/
├── github-mcp/
│   └── config.json
├── filesystem-mcp/
│   └── config.json
└── README.md
```

### Integration Points

| MCP | Capability | Use Case |
|-----|------------|----------|
| GitHub MCP | Read Issues, Create PRs, Comment | AI đọc requirement từ Issue |
| Filesystem MCP | Read/Write files | AI thao tác code trực tiếp |
| PostgreSQL MCP (future) | Query database | AI kiểm tra data integrity |
| Qdrant MCP (future) | Semantic search | AI tìm kiến thức |

### Week 4 Tasks

| Day | Task |
|-----|------|
| 1 | Research MCP servers |
| 2 | Setup GitHub MCP |
| 3 | Test: AI đọc Issue |
| 4 | Test: AI tạo branch |
| 5 | Test: AI tạo PR |
| 6 | Document MCP usage |
| 7 | Security review (token scope) |

### Success Criteria
- [ ] AI có thể đọc GitHub Issues
- [ ] AI có thể tạo branch từ issue
- [ ] AI có thể commit và push
- [ ] AI có thể tạo PR với description

---

## Phase 5: Documentation Automation (2 tuần)

### Mục tiêu
Code và docs cùng tiến hóa. Knowledge Base luôn cập nhật.

### Workflow

```
Feature Request
      ↓
AI reads PROJECT_CONTEXT.md + AGENTS.md
      ↓
Implementation (Phase 2 workflow)
      ↓
AI auto-updates:
  - Changelog.md
  - API docs (if endpoints changed)
  - Domain docs (if business rules changed)
  - ADR (if architectural decision changed)
      ↓
Review by User
      ↓
Commit code + docs together
```

### Auto-Update Rules

| Code Change | Doc Update Required |
|-------------|---------------------|
| New endpoint | API docs |
| Business rule change | Domain docs + Changelog |
| Architecture change | ADR mới hoặc update existing |
| New error pattern | `.windsurf/skills/` update |
| UI component change | Storybook/Docs |

### Week 5-6 Tasks

| Week | Day | Task |
|------|-----|------|
| 5 | 1 | Define auto-doc rules |
| 5 | 2 | Create doc templates |
| 5 | 3 | Test với 1 feature |
| 5 | 4 | Refine workflow |
| 5 | 5 | Add to AGENTS.md |
| 5 | 6 | Create Changelog.md |
| 5 | 7 | Review |
| 6 | 1-7 | Test với 2-3 features thực tế |

---

## Phase 6: Project Memory (2-4 tuần)

### Mục tiêu
Lưu lịch sử và quyết định của dự án. AI nhớ được: Đã làm gì, Chưa làm gì, Tại sao.

### Schema (PostgreSQL)

```sql
-- Tasks
CREATE TABLE tasks (
    id UUID PRIMARY KEY,
    title TEXT NOT NULL,
    description TEXT,
    agent_name TEXT, -- 'Build Fixer', 'Feature Developer', etc.
    status TEXT, -- 'pending', 'in_progress', 'completed', 'failed'
    git_branch TEXT,
    git_commit_hash TEXT,
    created_at TIMESTAMP,
    completed_at TIMESTAMP,
    metadata JSONB
);

-- Features (aggregates tasks)
CREATE TABLE features (
    id UUID PRIMARY KEY,
    name TEXT NOT NULL,
    description TEXT,
    related_adr_ids TEXT[], -- ['ADR-001', 'ADR-003']
    status TEXT,
    created_at TIMESTAMP,
    completed_at TIMESTAMP
);

-- Decisions (ADR history)
CREATE TABLE decisions (
    id UUID PRIMARY KEY,
    adr_id TEXT REFERENCES adrs(id),
    context TEXT,
    decision TEXT,
    consequences JSONB,
    made_at TIMESTAMP,
    made_by TEXT -- 'user' or agent_name
);

-- Agent History
CREATE TABLE agent_history (
    id UUID PRIMARY KEY,
    agent_name TEXT,
    task_id UUID REFERENCES tasks(id),
    action TEXT,
    input_summary TEXT,
    output_summary TEXT,
    files_modified TEXT[],
    success BOOLEAN,
    executed_at TIMESTAMP
);
```

### Week 7-10 Tasks

| Week | Focus |
|------|-------|
| 7 | Schema design + PostgreSQL setup |
| 8 | CRUD APIs + Integration |
| 9 | Test với real tasks |
| 10 | Query patterns ("What did we do last month?") |

---

## Phase 7: Semantic Search (2-4 tuần)

### Mục tiêu
Cho AI tìm kiến thức theo ngữ nghĩa. Không cần nhét toàn bộ KB vào context.

### Architecture

```
User Question
      ↓
Embedding (OpenAI/text-embedding-3-small)
      ↓
Qdrant Vector Search
      ↓
Top-K Relevant Docs
      ↓
AI Context (chỉ những gì cần thiết)
      ↓
Answer
```

### Collections

| Collection | Content | Chunk Strategy |
|------------|---------|----------------|
| adrs | ADR documents | Per ADR |
| domains | Domain docs | Per entity |
| workflows | .windsurf/workflows/ | Per workflow |
| skills | .windsurf/skills/ | Per skill |
| codebase | Source code | Per file/class |
| tasks | Task history | Per task |

### Week 11-14 Tasks

| Week | Focus |
|------|-------|
| 11 | Qdrant setup + embedding pipeline |
| 12 | Index existing docs |
| 13 | Integration với agents |
| 14 | Test: "Find all ADRs about accounting" |

---

## Phase 8: Agent Layer (1-2 tháng)

### Mục tiêu
Tách vai trò: PO Agent, Architect Agent, Developer Agent, QA Agent, Documentation Agent.

### Agent Topology

```
                    ┌─────────────────┐
                    │   Coordinator   │
                    │     Agent       │
                    └────────┬────────┘
                             │
        ┌────────────────────┼────────────────────┐
        │                    │                    │
        ▼                    ▼                    ▼
┌───────────────┐   ┌───────────────┐   ┌───────────────┐
│   PO Agent    │   │ Architect     │   │  Developer    │
│   (Planning)  │   │ Agent         │   │   Agent       │
└───────────────┘   └───────────────┘   └───────┬───────┘
                                                │
                        ┌───────────────────────┼───────────────────────┐
                        │                       │                       │
                        ▼                       ▼                       ▼
                ┌───────────────┐      ┌───────────────┐      ┌───────────────┐
                │  Frontend     │      │   Backend     │      │   QA Agent    │
                │  Developer    │      │   Developer   │      │ (Validation)  │
                └───────────────┘      └───────────────┘      └───────┬───────┘
                                                                      │
                                                                      ▼
                                                              ┌───────────────┐
                                                              │ Documentation │
                                                              │    Agent      │
                                                              └───────────────┘
```

### Hand-off Protocol

```markdown
1. PO Agent: Requirement → User Story + Acceptance Criteria
   ↓ [Hand-off: Story document]
   
2. Architect Agent: Story → Technical Design + ADR (if needed)
   ↓ [Hand-off: Design doc + Task breakdown]
   
3. Developer Agent: Design → Code
   ↓ [Hand-off: Commit + Tests]
   
4. QA Agent: Code → Validation
   ↓ [Hand-off: Test report]
   
5. Documentation Agent: Feature → Doc update
```

### Implementation Options

| Option | Pros | Cons |
|--------|------|------|
| Semantic Kernel | Native .NET, good orchestration | Learning curve |
| CrewAI | Python, simple syntax | External dependency |
| LangGraph | Visual debugging | Complex state mgmt |
| Custom (Windsurf-based) | Full control, leverage existing | Build everything |

**Recommendation:** Bắt đầu với **Semantic Kernel** (native .NET ecosystem).

---

## Phase 9: Software Pipeline (1 tháng)

### Mục tiêu
Workflow end-to-end từ requirement đến deploy.

### Pipeline

```
Requirement (Issue/Slack/Email)
      ↓
PO Agent analyzes + confirms với user
      ↓
Architect Agent designs + creates tasks
      ↓
Developer Agents implement (parallel if possible)
      ↓
GitHub Actions (Build/Test)
      ↓
QA Agent validates (automated + review checklist)
      ↓
User Review (approval gate)
      ↓
Deploy (staging → production)
      ↓
Documentation Agent updates KB
      ↓
Close loop (feedback vào Project Memory)
```

### Gates

| Gate | Condition | Action if Failed |
|------|-----------|------------------|
| G1 | Requirement clear | PO Agent asks clarifying questions |
| G2 | Design approved | User review required |
| G3 | Build pass | Auto-retry → Escalate |
| G4 | Tests pass | QA Agent analyzes → Fix |
| G5 | User acceptance | User approval required |
| G6 | Deploy success | Rollback → Alert |

---

## Phase 10: Software Factory (Ongoing)

### Mục tiêu
Trạng thái cuối: Self-improving system.

### Components

```
┌─────────────────────────────────────────┐
│           Knowledge Base                │
│  (Vector Search + Project Memory)       │
└─────────────────┬───────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────┐
│           Agent Team                    │
│  (Semantic Kernel / LangGraph)          │
│  - Self-orchestrating                   │
│  - Learns from history                  │
└─────────────────┬───────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────┐
│           GitHub + GitHub Actions       │
│  - Automated CI/CD                      │
│  - Quality gates                        │
└─────────────────┬───────────────────────┘
                  │
                  ▼
        ┌─────────────────┐
        │   Production      │
        └─────────────────┘
```

---

## Investment Priority (Nếu Nguồn Lực Hạn Chế)

**80% giá trị đến từ 4 bước đầu:**

| Priority | Phase | Value | Effort | ROI |
|----------|-------|-------|--------|-----|
| 1 | Phase 1: Knowledge Base | 🔥🔥🔥🔥🔥 | 2 tuần | Cao |
| 2 | Phase 2: AI Standardization | 🔥🔥🔥🔥🔥 | 1 tuần | Cao |
| 3 | Phase 3: GitHub Actions | 🔥🔥🔥🔥 | 1 tuần | Cao |
| 4 | Phase 4: MCP Integration | 🔥🔥🔥🔥 | 1 tuần | Cao |
| 5 | Phase 5: Doc Automation | 🔥🔥🔥 | 2 tuần | TB |
| 6 | Phase 6: Project Memory | 🔥🔥🔥 | 4 tuần | TB |
| 7 | Phase 7: Semantic Search | 🔥🔥🔥 | 4 tuần | TB |
| 8 | Phase 8: Multi-Agent | 🔥🔥 | 2 tháng | Thấp (trước) |
| 9 | Phase 9: Pipeline | 🔥🔥🔥 | 1 tháng | TB |
| 10 | Phase 10: Factory | 🔥🔥🔥 | Ongoing | TB |

**Khuyến nghị:** Dừng ở Phase 4 nếu nguồn lực hạn chế. Các phase 5-7 có thể làm sau.

---

## Immediate Next Steps (Bắt Đầu Ngay)

### Hôm nay (June 1, 2026)

1. **Approve roadmap này** ← Bạn đang ở đây
2. Tạo folder `docs/decisions/`
3. Bắt đầu ADR-001

### Tuần này (Week 1)

- [ ] Day 1: Create decisions/ + ADR-001
- [ ] Day 2: ADR-002 + ADR-003
- [ ] Day 3: ADR-004 + ADR-005
- [ ] Day 4: Review all ADRs
- [ ] Day 5: Create knowledge-base/ structure
- [ ] Day 6: PROJECT_CONTEXT.md
- [ ] Day 7: Week 1 review

### Tuần sau (Week 2)

- [ ] Domain docs (Order, Inventory, Payment, Accounting)
- [ ] AGENTS.md draft
- [ ] Integration test với 1 task thực

---

## Success Metrics

| Phase | Metric | Target |
|-------|--------|--------|
| 1 | ADRs written | 5+ |
| 1 | Domain docs coverage | 100% core domains |
| 2 | Agent definition clarity | 5 agents defined |
| 2 | Code consistency | User confirms improvement |
| 3 | CI pass rate | >95% |
| 3 | Build time | <5 minutes |
| 4 | AI automation | Can create PR end-to-end |
| 5 | Doc freshness | <1 week lag |
| 6 | Task recall | Can query "what did we do" |
| 7 | Search accuracy | Top-3 relevant >80% |
| 8 | Agent hand-off | <1 hour between agents |
| 9 | Pipeline cycle time | Feature: 1-3 days |
| 10 | Human intervention | Only at gates |

---

## Appendix A: Existing Assets (Tận Dụng)

### Windsurf Knowledge Base (Đã có, giữ nguyên)

```
.windsurf/
├── rules/
│   ├── .windsurfrules          # Core governance v7.0 ✅
│   └── playwright.rules.md     # E2E governance ✅
│
├── workflows/
│   ├── Fix_Errors.md           # Pattern-based fixing ✅
│   ├── Fix_Tests.md            # Test failure handling ✅
│   ├── newfeaturebuild.md      # 7-step feature dev ✅
│   ├── playwright_triage.md    # E2E triage ✅
│   ├── playwright_fix.md       # E2E fixing ✅
│   ├── playwright_validation.md # E2E validation ✅
│   ├── review.md               # Code review ✅
│   └── [4 more]                # Additional workflows ✅
│
└── skills/
    ├── accounting-ui-implementation.md ✅
    ├── domain-integrity-validation.md ✅
    ├── pattern-based-fixing.md ✅
    ├── playwright_guard.md ✅
    ├── playwright_cost_optimizer.md ✅
    └── [16 more]               # Feature-specific ✅
```

**Tổng:** 2 rules + 12 workflows + 21 skills = **35 knowledge assets sẵn có**

### Codebase Structure (Đã có)

```
1_Shared/          # Domain (Single Source of Truth)
2_Gateway/         # API Gateway
3_CoreHub/         # Business Logic
4_MobileApps/      # MAUI Apps
5_WebApps/         # Blazor Apps
6_Testing/         # E2E Tests
6_Tests/           # Unit Tests
UI.Platform/       # UI Components
```

---

## Appendix B: File Reference Matrix

| New File | References | Purpose |
|----------|------------|---------|
| `docs/decisions/ADR-001` | `.windsurf/skills/nats-sqlite-deployment-validation.md` | Explain SQLite+NATS decision |
| `docs/decisions/ADR-003` | `.windsurfrules` (AccountingEntry immutability) | Document business rule |
| `docs/knowledge-base/08-ai/AGENTS.md` | All `.windsurf/workflows/*.md` | Map workflows to agents |
| `docs/knowledge-base/00-core/PROJECT_CONTEXT.md` | `.windsurf/rules/.windsurfrules` | Provide context for AI |
| `.github/workflows/ci.yml` | `VanAn.sln`, `guard-check.ps1` | Automated CI |

---

## Appendix C: Risk Mitigation

| Risk | Mitigation |
|------|------------|
| AI không hiểu domain | Phase 1: Domain docs + ADRs trước khi code |
| Code consistency kém | Phase 2: AGENTS.md define rõ constraints |
| Build break frequently | Phase 3: CI/CD bắt buộc pass |
| Knowledge base outdated | Phase 5: Auto-update workflow |
| Agent hand-off confusion | Phase 8: Clear protocols + state mgmt |
| Over-engineering | Stop ở Phase 4 nếu không cần multi-agent |

---

## Conclusion

Roadmap này:
1. **Tận dụng** knowledge base Windsurf hiện tại (35 assets)
2. **Bổ sung** domain docs và ADRs để preserve decisions
3. **Chuẩn hóa** AI agents với rõ ràng constraints
4. **Xây dựng** CI/CD foundation trước khi complex automation
5. **Tiến triển** từng bước, có thể dừng ở Phase 4 nếu cần

**Bắt đầu:** Tạo `docs/decisions/ADR-001-SQLite-Offline-First.md`

---

*Document Status: Draft - Cần User approve để bắt đầu execution*
