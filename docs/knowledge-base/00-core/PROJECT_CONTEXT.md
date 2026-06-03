# ShopERP Project Context

> **Context tổng hợp cho AI Assistant**  
> File này được reference mỗi khi bắt đầu session mới.

## Project Overview

| Property | Value |
|----------|-------|
| Name | ShopERP (VanAn Ecosystem) |
| Type | SaaS POS & Accounting |
| Stack | .NET 8, Blazor, MAUI, SQLite, NATS, PostgreSQL |
| Architecture | Modular Monolith + Offline-First |
| Compliance | VAT 2026 (Vietnam) |

## Repository Structure

```
c:\VibeCoding\Gemini_Windsurf\
├── 1_Shared/              # Domain (Single Source of Truth)
│   ├── Domain.cs          # Core entities, value objects
│   ├── DTOs/              # Data transfer objects
│   └── BusinessRules.cs   # Domain business rules
├── 2_Gateway/             # API Gateway (SignalR, REST)
├── 3_CoreHub/             # Business Logic + SQLite Local
├── 4_MobileApps/          # MAUI Apps (HR, Station)
├── 5_WebApps/             # Blazor Apps (ShopERP, KhachLink)
├── 6_Testing/             # E2E Tests (Playwright)
├── 6_Tests/               # Unit & Architecture Tests
├── UI.Platform/           # Shared UI Components
├── docs/                  # Documentation
│   ├── decisions/         # ADRs
│   └── knowledge-base/    # Domain docs
├── .windsurf/             # AI Knowledge Base
│   ├── rules/             # Governance rules
│   ├── workflows/         # AI Workflows
│   └── skills/            # Domain skills
└── mcp/                   # MCP Configuration (Phase 4)
    ├── github-mcp/        # GitHub MCP config
    ├── filesystem-mcp/    # Filesystem MCP config
    └── README.md          # MCP documentation
```

## Architecture Decisions (ADRs)

| ADR | Decision | Constraint |
|-----|----------|------------|
| ADR-001 | SQLite + NATS Offline First | Local SQLite → NATS → PostgreSQL |
| ADR-002 | Multi-Tenancy Everywhere | `TenantId` mandatory on all entities |
| ADR-003 | Accounting Immutability | Append-only, no edit, only reversal |
| ADR-004 | UI Platform Mandatory | Use `VanAn.UI.Platform`, no custom CSS |
| ADR-005 | Playwright Isolation | Disabled during IMPLEMENT mode |

## Core Business Rules

### Multi-Tenancy
- `TenantId` is **required** on all entities
- Global query filters enforce isolation
- Never bypass tenant checks

### Accounting
- `AccountingEntry` is **immutable**
- Only `CreateReversal()` for corrections
- Period closing prevents new entries

### Order Processing
```
Pending → Confirmed → Preparing → Ready → Completed
              ↓
          Cancelled
```
- Inventory deducted at "Confirmed"
- Kitchen status tracked separately

### Data Flow
```
Station (SQLite) → Outbox → NATS → PostgreSQL
                        ↓
                    Other Stations
```

## Technology Stack

### Backend
- .NET 8
- Entity Framework Core (SQLite + PostgreSQL)
- SignalR (real-time)
- NATS (event bus)
- Serilog (logging)

### Frontend
- Blazor Server/WebAssembly
- MAUI (mobile)
- Bootstrap (via UI.Platform)
- No custom CSS

### Testing
- xUnit (unit tests)
- Playwright (E2E)
- Architecture tests

## AI Knowledge Base

### Rules
- `.windsurf/rules/.windsurfrules` - Core governance v7.0
- `.windsurf/rules/playwright.rules.md` - E2E governance

### Workflows
| Workflow | Purpose |
|----------|---------|
| `Fix_Errors.md` | Pattern-based error fixing |
| `Fix_Tests.md` | Test failure handling |
| `newfeaturebuild.md` | 7-step feature development |
| `playwright_triage.md` | E2E triage |
| `playwright_fix.md` | E2E fixing |
| `playwright_validation.md` | E2E validation |
| `review.md` | Code review |

### Skills
| Skill | Use When |
|-------|----------|
| `domain-integrity-validation.md` | Domain layer changes |
| `pattern-based-fixing.md` | Error fixing |
| `ui-platform-migration.md` | UI development |
| `nats-sqlite-deployment-validation.md` | Deployment validation |
| `playwright_guard.md` | Playwright execution |

## Code Patterns

### Domain Entity
```csharp
public class Order : BaseEntity, IMustHaveTenant
{
    // Protected setters only
    public OrderId OrderId { get; protected set; }
    public OrderStatusId Status { get; protected set; }
    
    // Business methods
    public void UpdateStatus(OrderStatusId status)
    {
        Status = status;
        UpdateAudit();
    }
}
```

### Factory Method
```csharp
public static Order Create(Guid id, TenantId tenantId, List<OrderItem> items)
{
    // Validation logic
    // Create instance
    // Return
}
```

### Value Object
```csharp
public record TenantId(Guid Value)
{
    public static implicit operator Guid(TenantId tenantId) => tenantId.Value;
    public static implicit operator TenantId(Guid value) => new(value);
}
```

## Hard Stops for AI

**AI MUST refuse if:**
1. Domain layer modified to fix UI/Service issue
2. `TenantId` removed or made nullable
3. `AccountingEntry` made mutable
4. Custom CSS/inline styles suggested
5. Playwright run during IMPLEMENT mode

## Common Commands

```bash
# Build
dotnet build VanAn.sln

# Test
dotnet test 6_Tests/VanAn.Core.Tests/
dotnet test 6_Tests/VanAn.Architecture.Tests/

# Guard check
./guard-check.ps1

# Run ecosystem
./Start-VanAnEcosystem.ps1
```

## MCP Integration (Phase 4)

ShopERP sử dụng Model Context Protocol (MCP) để AI tương tác trực tiếp với:

### MCP Servers

| Server | Purpose | Config |
|--------|---------|--------|
| GitHub MCP | Read issues, create PRs, manage branches | `mcp/github-mcp/config.json` |
| Filesystem MCP | Read/write project files | `mcp/filesystem-mcp/config.json` |

### MCP Workflows

1. **Feature from Issue**: AI đọc issue → tạo branch → implement → tạo PR
2. **Bug Fix**: AI đọc bug report → tạo fix branch → commit → PR
3. **Code Review**: AI review PR → add comments → feedback

### Security

- GitHub token: `GITHUB_TOKEN` env var (repo, issues, PRs scope)
- Filesystem: Chỉ trong project directory, blocked: `.git/`, `.env`, `secrets/`
- Audit: Tất cả operations logged

## External References

- **Legal**: Thông tư 200/2014/TT-BTC, Thông tư 152/2025/TT-BTC
- **Framework**: .NET 8 Documentation, EF Core Docs
- **Tools**: Windsurf Documentation, Playwright Docs, MCP Spec

## Session Initialization

When starting new session:
1. Read this file
2. Read relevant ADRs
3. Check `.windsurf/rules/.windsurfrules`
4. Identify mode (ANALYZE/IMPLEMENT/FIX_ONLY/...)

---

*Document Status: Active*  
*Last Updated: June 1, 2026*  
*Version: 1.1 (includes MCP Integration)*
