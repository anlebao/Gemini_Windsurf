# TASK CARD: W2-ADR-T2 — Architecture Test: Fix Rule H + Add Rule I

**Wave:** 2 — Create docker-compose.edge.yml
**Branch:** `feature/adr001-wave2-edge-compose`
**Estimated effort:** 1 hour
**Dependency:** W2-ADR-T1 complete ✅ (docker-compose.edge.yml tồn tại)

---

## 1. GOAL & CONTEXT

Hiện tại **Rule H** trong `ArchitectureRulesTests.cs` đang check sai:
- Nó kỳ vọng `docker-compose.prod.yml` chứa SQLite stations
- Nhưng theo 2-version strategy: **prod = PostgreSQL only** (v1 SaaS)
- Edge compose (`docker-compose.edge.yml`) mới là file cần SQLite

**Việc cần làm:**
1. **Fix Rule H** — đổi thành "CoreHub trong prod MUST dùng PostgreSQL" (đúng với v1 SaaS)
2. **Add Rule I** — check `docker-compose.edge.yml` có SQLite + NATS sync worker (ADR-001 v2 Edge)

---

## 2. VERIFIED FACTS

| Fact | Source |
|------|--------|
| Rule H hiện tại check sai (prod không có SQLite, test đang FAIL) | `ArchitectureRulesTests.cs` L228-263 |
| `docker-compose.prod.yml`: corehub dùng PostgreSQL connection string | `docker-compose.prod.yml` L69 |
| `docker-compose.edge.yml` sẽ tạo ở W2-ADR-T1 | W2-ADR-T1-card.md |
| Architecture test file: `6_Tests/VanAn.Architecture.Tests/ArchitectureRulesTests.cs` | verified |

---

## 3. IMPLEMENTATION SPEC

### 3.1 Fix Rule H (lines 228-263)

**Thay đổi DisplayName và logic:**

```csharp
[Fact(DisplayName = "Rule H: ADR-001 v1 SaaS - docker-compose.prod.yml CoreHub MUST use PostgreSQL")]
public void DockerComposeProd_CoreHub_MustUse_PostgreSQL()
{
    var repoRoot = GetRepoRoot();
    var dockerComposeFile = Path.Combine(repoRoot, "docker-compose.prod.yml");

    if (!File.Exists(dockerComposeFile))
        Assert.Fail($"docker-compose.prod.yml not found: {dockerComposeFile}");

    var content = File.ReadAllText(dockerComposeFile);

    // v1 SaaS: CoreHub MUST connect to PostgreSQL (not SQLite)
    var hasPostgresForCoreHub = content.Contains("Host=postgres") ||
                                content.Contains("postgres:5432");

    Assert.True(hasPostgresForCoreHub,
        "ADR-001 v1 violation: docker-compose.prod.yml CoreHub must use PostgreSQL for cloud accounting");
}
```

**Lý do:** Rule H không thay đổi mục đích — vẫn là ADR-001 compliance check cho v1, nhưng v1 SaaS đúng phải dùng PostgreSQL.

### 3.2 Add Rule I (append trước closing brace của class)

```csharp
[Fact(DisplayName = "Rule I: ADR-001 v2 Edge - docker-compose.edge.yml MUST include SQLite + NATS sync worker")]
public void DockerComposeEdge_MustInclude_SQLite_And_NatsSyncWorker()
{
    var repoRoot = GetRepoRoot();
    var edgeComposeFile = Path.Combine(repoRoot, "docker-compose.edge.yml");

    if (!File.Exists(edgeComposeFile))
        Assert.Fail($"docker-compose.edge.yml not found at: {edgeComposeFile}. Create it as part of Wave 2.");

    var content = File.ReadAllText(edgeComposeFile);

    // ADR-001 v2 Edge: Must have named SQLite volume for persistence
    var hasSQLiteVolume = content.Contains("shoperp_sqlite_data");

    // ADR-001 v2 Edge: Must have NATS sync worker service
    var hasNatsSyncWorker = content.Contains("shoperp-nats-sync") ||
                            content.Contains("nats-sync");

    // ADR-001 v2 Edge: Must still have NATS broker
    var hasNatsBroker = content.Contains("image: nats:") ||
                        content.Contains("nats:2.10");

    Assert.True(hasSQLiteVolume,
        "ADR-001 v2 Edge violation: docker-compose.edge.yml must declare shoperp_sqlite_data volume for SQLite persistence");
    Assert.True(hasNatsSyncWorker,
        "ADR-001 v2 Edge violation: docker-compose.edge.yml must include shoperp-nats-sync worker service");
    Assert.True(hasNatsBroker,
        "ADR-001 v2 Edge violation: docker-compose.edge.yml must include NATS broker service");
}
```

---

## 4. EDIT LOCATION

**File:** `6_Tests/VanAn.Architecture.Tests/ArchitectureRulesTests.cs`

- **Rule H:** Lines 228-263 → replace toàn bộ method
- **Rule I:** Insert mới TRƯỚC dấu `}` cuối cùng của class (sau Rule H)

---

## 5. HARDENING GATES

- [ ] Rule H sau khi sửa: **MUST PASS** (prod có PostgreSQL đúng rồi)
- [ ] Rule I sau khi tạo: **MUST PASS** (edge.yml được tạo ở W2-ADR-T1)
- [ ] Tổng số rules: từ 21 → 22 (thêm Rule I)
- [ ] `dotnet test 6_Tests/VanAn.Architecture.Tests/` — all pass

---

## 6. VALIDATION

```powershell
cd c:/VibeCoding/Gemini_Windsurf
dotnet test 6_Tests/VanAn.Architecture.Tests/ --verbosity normal

# Expected output:
# Rule H: ADR-001 v1 SaaS - ... PASS
# Rule I: ADR-001 v2 Edge - ... PASS
# All 22 tests passed
```

---

## 7. EXIT CRITERIA

- [ ] Rule H DisplayName updated, logic changed to check PostgreSQL in prod
- [ ] Rule H PASSES (không còn fail do architecture drift)
- [ ] Rule I added, checks `docker-compose.edge.yml` structure
- [ ] Rule I PASSES (file tồn tại từ W2-ADR-T1)
- [ ] `dotnet build VanAn.sln` 0 errors
- [ ] Architecture test suite: 22/22 PASS
