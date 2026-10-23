# Task P2 — Rate Card Versioned CRUD + Audit + Resolver + Unit Tests

> **Status:** ✅ IMPLEMENTED — build 0 warnings/errors · tests 25/25 PASS · HTTP smoke PASS (401/403/CRUD/resolve/delete-idempotent/audit)
> **Phase:** P2 (GĐ-1) · **Gate:** tests pass
> **Căn cứ:** SRS §6.1 (`/v1/rate-cards`) · [G7] versioned · [G20] Enforcement · NFR-5 audit · O-3/O-4 đã chốt

---

## 1. Mục tiêu

SystemAdmin quản lý hạng mục tính phí (serviceCode) + định mức **không cần deploy**: tạo/sửa/ngưng hạng mục, mọi thay đổi là **version mới có hiệu lực theo thời điểm**, lịch sử không bao giờ bị sửa/xóa, mọi mutation ghi audit. Resolver trả đúng định mức tại `occurredAtUtc` để P3 ingest dùng.

## 2. Quyết định thiết kế (cần user xác nhận ở §7)

| # | Điểm | Đề xuất |
|---|---|---|
| D1 | Version model | **Append-only**: mỗi `ServiceCode` có chuỗi `RateCardVersion`. PATCH = insert version mới (`EffectiveFrom` mới) + version hiện tại `EffectiveTo = at`. DELETE = soft-deactivate (`IsActive=false` + `EffectiveTo=now`). KHÔNG UPDATE giá trị đã ghi, KHÔNG xóa row |
| D2 | "Current version" | `IsActive = true AND EffectiveTo IS NULL`. Superseded: `IsActive=true AND EffectiveTo != null` (vẫn resolve được cho quá khứ). Deactivated: `IsActive=false` (không bao giờ resolve) |
| D3 | PATCH scope | Chỉ patch được **current version** (`IsActive && EffectiveTo==null`). Patch version đã superseded/deactivated → **409**. Lý do: sửa "quá khứ" phá tính append-only; muốn đổi giá cũ phải tạo version mới có EffectiveFrom tương ứng |
| D4 | PATCH `effectiveFrom` | Cho phép **tương lai** (đổi giá theo lịch — vd giá mới từ 01/11). Ràng buộc: `effectiveFrom > current.EffectiveFrom`, và `effectiveFrom >= serverNow - 5 phút` (chống nhầm ngày quá khứ) → sai → 400 |
| D5 | Admin auth | **CHỐT — A: `CallerKey.IsAdmin`** — mutation endpoints (POST/PATCH/DELETE) yêu cầu admin caller; GET cho mọi caller. Bootstrap env `Metering__BootstrapAdmin="callerId:key"` seed admin |
| D6 | Idempotent delete | DELETE id không tồn tại → 404; DELETE version đã inactive → **204 idempotent** (không lỗi) |
| D7 | Resolve miss | Không có version hiệu lực → trả `null` (unpriced). P3 quyết định xử lý (ghi event với `unpriced=true`) — KHÔNG throw |
| D8 | Audit | Mọi create/patch/delete → `AuditLog{Actor=callerId, Action, EntityType, EntityId, OldJson, NewJson, AtUtc}` append-only |

## 3. Data model (Domain — thêm vào project đã có)

`RateCardVersion` *(file đã tồn tại từ lần trước — giữ nguyên)*:
`Id · ServiceCode · MeterUnit · CreditRate(decimal) · Enforcement(enum MeterOnly|Deny) · EffectiveFrom · EffectiveTo? · IsActive · Label? · ChangedBy · ChangedAtUtc · AuditNote?` — methods: `Create(...)`, `SupersedeAt(at)`, `Close(at)`.

`AuditLog` *(mới — recreate)*:
`Id · Actor · Action · EntityType · EntityId · OldJson? · NewJson? · AtUtc`.

`CallerKey` += `IsAdmin` *(D5-A đã chốt)* — dev DB tạo lại (EnsureCreated, chưa có data production).

EF config (MeteringDbContext):
- `RateCardVersions`: unique `(ServiceCode, EffectiveFrom)` · index `(ServiceCode, IsActive)` · CreditRate precision(18,4) · Enforcement → int
- `AuditLogs`: index `(EntityType, EntityId)` · append-only (không endpoint nào update/delete)

## 4. API endpoints (SRS §6.1 — tất cả `/v1/*` qua auth middleware P1)

| Method | Path | Auth | Body → Response |
|---|---|---|---|
| GET | `/v1/rate-cards?includeInactive=` | caller thường | `RateCardDto[]` — default: current versions (IsActive && EffectiveTo==null); `includeInactive=true` → tất cả rows (version history) |
| POST | `/v1/rate-cards` | **admin** | `RateCardCreateRequest` → 201 `RateCardDto` · 400 validation · 409 active-exists |
| PATCH | `/v1/rate-cards/{id}` | **admin** | `RateCardPatchRequest` → 200 `RateCardDto` (version mới) · 404 · 409 not-current · 400 |
| DELETE | `/v1/rate-cards/{id}` | **admin** | → 204 · 404 |
| GET | `/v1/rate-cards/resolve?serviceCode=&atUtc=` | caller thường | `RateCardDto` hoặc `{resolved:false}` — dùng cho P3 + debug |

DTOs (Contracts): `RateCardCreateRequest{serviceCode, meterUnit, creditRate, enforcement?, effectiveFrom?, effectiveTo?, label?, auditNote?}` · `RateCardPatchRequest{creditRate?, meterUnit?, enforcement?, effectiveFrom?, label?, auditNote?}` (null = giữ giá trị cũ) · `RateCardDto`.

`RateCardService` (Infra): `ResolveAsync(code, atUtc)` · `ListAsync(includeInactive)` · `CreateAsync(...)` · `PatchAsync(id, ...)` · `DeactivateAsync(id)` — trả `(result, errorCode)` không throw cho lỗi nghiệp vụ.

## 5. Edge cases đã liệt kê hết

**Create:** (a) serviceCode rỗng/whitespace → 400 · (b) creditRate < 0 → 400 (rate=0 cho phép — free tier) · (c) enforcement string không parse được → 400 · (d) `EffectiveFrom >= EffectiveTo` → 400 · (e) đã có active version cùng serviceCode → 409 (bắt buộc PATCH — tránh tạo 2 "current") · (f) tạo lại sau khi deactivate → OK · (g) EffectiveFrom tương lai → tạo được, resolve chỉ trả khi đến hạn · (h) serviceCode trim + case-normalize? → **giữ nguyên case, so sánh ordinal** (serviceCode là technical key, admin tự chuẩn — ghi rõ trong label)

**Patch:** (a) id không tồn tại → 404 · (b) version superseded/deactivated → 409 · (c) `effectiveFrom <= current.EffectiveFrom` → 400 · (d) `effectiveFrom` quá khứ >5 phút → 400 · (e) field null → giữ giá trị cũ · (f) rate mới < 0 → 400 · (g) khi patch với effectiveFrom tương lai: current version vẫn resolve đến `at` — hai version "active" cùng lúc là **by design** (current + scheduled) · (h) patch version đã bị superseded bởi scheduled version → 409 (đúng D3 — phải patch scheduled version hoặc tạo mới)

**Delete:** (a) không tồn tại → 404 · (b) inactive rồi → 204 · (c) delete scheduled version tương lai → chỉ đóng version đó, current cũ KHÔNG tự mở lại (giữ append-only; admin patch lại nếu cần)

**Resolve:** (a) chưa có version → null · (b) `atUtc == EffectiveFrom` → **bao gồm** (>=) · (c) `atUtc == EffectiveTo` → **loại trừ** (>) · (d) nhiều version khớp (overlap bất thường) → `ORDER BY EffectiveFrom DESC` lấy mới nhất · (e) version inactive → bỏ qua · (f) atUtc < version đầu tiên → null

**Audit:** mọi mutation → 1 AuditLog; PATCH log cả old+new JSON; actor = callerId từ auth middleware.

## 6. Test matrix — `tests/VanAn.Metering.Tests` (xUnit + EF Core InMemory + FluentAssertions — versions đã có trong CPM)

| # | Test | Expect |
|---|---|---|
| T01 | Resolve no version | null |
| T02 | Resolve before first EffectiveFrom | null |
| T03 | Resolve at == EffectiveFrom / == EffectiveTo | include / exclude |
| T04 | Resolve 2 versions | đúng rate mỗi khoảng thời gian |
| T05 | Resolve bỏ qua inactive | null hoặc version khác |
| T06 | Resolve overlap → latest EffectiveFrom | version mới nhất thắng |
| T07 | Create valid | 201-equivalent + row + AuditLog(newJson) |
| T08 | Create duplicate active code | error serviceCode_active_exists |
| T09 | Create sau deactivate | OK |
| T10 | Create invalid (empty code / rate<0 / bad enforcement / from>=to) | error tương ứng |
| T11 | Patch current → version mới | old.EffectiveTo=at, old IsActive=true, resolve 2 đầu đúng |
| T12 | Patch giữ field null | rate/unit/enforcement cũ được copy |
| T13 | Patch superseded/deactivated | error version_not_current |
| T14 | Patch not found | error not_found |
| T15 | Patch effectiveFrom<=current.From hoặc quá khứ | error |
| T16 | Patch scheduled tương lai | current vẫn resolve đến at, version mới sau at |
| T17 | Deactivate | IsActive=false + EffectiveTo set + resolve null + AuditLog |
| T18 | Deactivate lần 2 | idempotent OK |
| T19 | List default vs includeInactive | current-only vs full history |
| T20 | Audit: mọi mutation có AuditLog đúng Action/Actor/EntityId | đếm + nội dung |

**Giới hạn test:** EF InMemory không enforce unique index — logic dup-check nằm ở service (AnyAsync) nên vẫn cover; unique index verify thật khi integration/P5. Endpoint-level (401/403 admin) test nhẹ: gọi service-level permission check hoặc 2 test với `CallerKey.IsAdmin` thật qua middleware logic — giữ ở mức service test, không spin web host (WebApplicationFactory defer nếu cần sau).

## 7. Quyết định — ĐÃ CHỐT (user duyệt)

- **D5 (admin auth): A — `CallerKey.IsAdmin`**
- D1–D4, D6–D8: duyệt theo đề xuất (chỉ patch current · cho phép lên lịch đổi giá · idempotent delete · resolve miss → null · audit append-only)
- Test matrix T01–T20: duyệt

## 8. File plan (sau khi duyệt)

| File | Action |
|---|---|
| `Domain/RateCardVersion.cs` | giữ (đã có) |
| `Domain/AuditLog.cs` | tạo lại |
| `Domain/CallerKey.cs` | += IsAdmin (D5-A) |
| `Contracts/RateCardContracts.cs` | mới |
| `Infra/MeteringDbContext.cs` | += 2 DbSet + config |
| `Infra/RateCardService.cs` | mới |
| `Api/Program.cs` | += endpoints + admin guard + DI + `Metering__BootstrapAdmin` seed |
| `Api/appsettings.json` | không đổi |
| `tests/VanAn.Metering.Tests/` | project mới (xUnit + EF InMemory + FluentAssertions), add vào `VanAn.Metering.sln` |

**Out of scope P2:** admin UI (P6) · deduct-at-ingest (P3 + P7) · CallerKey CRUD API (P7) · EF migrations (P13 — dev DB recreate khi schema đổi).
