# Project Memory (Phase 6)

> **Phase 6 Deliverable** — AI task/history tracking and knowledge persistence

---

## Overview

Project Memory là hệ thống lưu trữ lịch sử và quyết định của dự án, cho phép AI nhớ được:
- **Đã làm gì** — Task history, agent actions
- **Chưa làm gì** — Pending tasks, incomplete features  
- **Tại sao** — ADR decisions, context, consequences

---

## Architecture

### Architecture Rules Enforced

| Rule ID | Description | Enforcement Mechanism |
|---------|-------------|----------------------|
| VA-DDD-002 | Domain Layer Should Not Have Dependency On Infrastructure Or Application | ArchitectureTests.cs scan forbidden namespaces |
| VA-ARCH-001 | Application Layer Should Not Contain Migration Classes | ArchitectureTests.cs scan Migration folders/classes |
| VA-GATEWAY-003 | Gateway Boundary Guard (No DbContext/EF Core) | ArchitectureTests.cs scan Gateway project |
| VA-KHACHLINK-004 | Client UI Boundary Guard (No direct DB access) | ArchitectureTests.cs scan KhachLink project |

### SQLite-First Approach
- **Current:** SQLite (compatible với existing ADR-001)
- **Future:** PostgreSQL (cloud deployment)
- **Migration:** Same schema, different connection string

### EF Core Setup Configuration

| Project | DbContextLocation | MigrationStrategy | HasMigrationsFolder | DatabaseType |
|---------|------------------|-------------------|---------------------|--------------|
| 5_WebApps/KhachLink | 5_WebApps/KhachLink/Program.cs (Line 63-65) | EnsureCreatedAsync() | false | SQLite local dev |
| 3_CoreHub | 3_CoreHub/Infrastructure/VanAnDbContext.cs | EnsureCreatedAsync() (Program.cs Line 36) | false | PostgreSQL (Host=localhost) |

### Schema

| Table | Purpose | Key Fields |
|-------|---------|------------|
| `ai_tasks` | Track individual AI work | title, agent_name, `AiTaskStatus`, git_branch, metadata |
| `ai_features` | Group tasks into features | name, related_adr_ids, `AiFeatureStatus` |
| `ai_feature_tasks` | Many-to-many link | feature_id, task_id |
| `ai_decisions` | ADR history | adr_id, context, decision, consequences |
| `ai_agent_history` | Detailed action log | agent_name, action, input/output, files_modified |
| `ai_sessions` | Vạn An specific (S1, S2, F0...) | session_code, feature_name, tests_passed/total |

---

## Usage

### DI Registration
```csharp
// Program.cs
builder.Services.AddDbContext<ProjectMemoryDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("ProjectMemory")));

builder.Services.AddScoped<IProjectMemoryService, ProjectMemoryService>();
```

### Track New Session
```csharp
// At start of S1, S2, etc.
var session = await _memory.StartSessionAsync("S1", "UC1 QR Checkout", testsTotal: 9);
```

### Log Agent Action
```csharp
await _memory.LogAgentActionAsync(
    agentName: "Feature Developer",
    action: "implemented_domain",
    inputSummary: "PendingInvoiceQueue, RecipientType enums needed",
    outputSummary: "Created enums, added to Domain.cs",
    success: true,
    filesModified: new[] { "1_Shared/Domain.cs" }
);
```

### Complete Session
```csharp
await _memory.CompleteSessionAsync("S1", testsPassed: 9, 
    summary: "T01-T09 PASS, SQLite in-memory");
```

---

## Query Patterns

### "What did we do last month?"
```csharp
var actions = await _memory.GetWhatWeDidLastMonthAsync();
// Returns: List of successful agent actions in last 30 days
```

### Sprint Retrospective
```csharp
var report = await _memory.GenerateSprintRetrospectiveAsync("UC1 QR Checkout");
// Returns: Markdown report with sessions, decisions, stats
```

### Find Similar Patterns
```csharp
var patterns = await _memory.FindSimilarPatternsAsync("CS0311");
// Returns: Previous similar errors and fixes
```

---

## Rx Report Format

```
S1 ✅ | coverage 9/20 | next: S2
S2 ✅ | coverage 4/20 | next: S3
S3 ✅ | coverage 5/20 | next: S4
S4 ✅ | coverage 4/20 | next: Frontend
```

---

## Integration with Agents

### Feature Developer Agent
```
Start Session → Execute Tasks → Log Actions → Complete Session
     ↓                                              ↓
  [S1, S2...]                                [Rx report]
```

### Build Fixer Agent
```
Log error pattern → Fix → Log solution → [Future] Similar error? Suggest fix
```

---

## Migration to PostgreSQL

When ready for cloud deployment:

1. Update connection string
2. Run schema migration:
   ```bash
   dotnet ef migrations add InitialPostgreSQL --context ProjectMemoryDbContext
   ```
3. Update JSON fields to JSONB (PostgreSQL native)
4. Re-index for performance

---

## Files

| File | Path |
|------|------|
| Schema SQL | `3_CoreHub/Infrastructure/ProjectMemory/ProjectMemorySchema.sql` |
| DbContext | `3_CoreHub/Infrastructure/ProjectMemory/ProjectMemoryDbContext.cs` |
| Service Interface | `3_CoreHub/Infrastructure/ProjectMemory/IProjectMemoryService.cs` |
| Service Implementation | `3_CoreHub/Infrastructure/ProjectMemory/ProjectMemoryService.cs` |
| Entities | `3_CoreHub/Infrastructure/ProjectMemory/Entities/*.cs` |

---

## Version History

| Version | Date | Change |
|---------|------|--------|
| 1.0 | 2026-06-10 | Initial Phase 6 implementation (SQLite) |

---

*Next: Phase 7 Semantic Search (Qdrant integration)*
