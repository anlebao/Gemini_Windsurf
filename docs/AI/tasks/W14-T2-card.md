# TASK CARD: PRODUCTION_HYGIENE - WAVE14 - Implement Timestamp + Nonce Anti-Replay

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Implement anti-replay protection bằng timestamp validation + nonce deduplication
- **Nghiệp vụ áp dụng:** Chống Replay Attack — ngăn kẻ tấn công gửi lại request đã capture
- **SRS gốc:** `docs/requirements/Van_An_Solution_SRS_Lightweight_Key_Management_Protocol.md` (Section 3.4 items 1-2 — simplified)

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT (after approved plan)

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `2_Gateway/Services/ReplayProtectionService.cs` (TẠO MỚI)
  - `2_Gateway/Services/IReplayProtectionService.cs` (TẠO MỚI)
  - `2_Gateway/Middleware/HmacSignatureMiddleware.cs` (integrate — từ W14-T1)
  - `2_Gateway/Program.cs` (register IMemoryCache + service DI)
  - `docs/AI/tasks/PRODUCTION_HYGIENE_master_plan.md` (cập nhật status)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG thêm Redis/IDistributedCache — dùng IMemoryCache ONLY
  - KHÔNG sửa Domain layer
  - KHÔNG thay đổi existing JWT flow

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)

### Timestamp Validation
```csharp
// Logic
var clientTimestamp = long.Parse(headers["X-VanAn-Timestamp"]);
var serverTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
var drift = Math.Abs(serverTimestamp - clientTimestamp);
if (drift > 60) → REJECT "Request expired or clock drift too large"
```

### Nonce Deduplication
```csharp
// Logic using IMemoryCache
var nonceKey = $"nonce:{apiKeyId}:{nonce}";
if (_cache.TryGetValue(nonceKey, out _)) → REJECT "Duplicate nonce (replay detected)"
_cache.Set(nonceKey, true, TimeSpan.FromSeconds(120)); // TTL = 2x timestamp window
```

### Design Decisions
- **Window = 60 seconds:** Balance giữa clock drift tolerance và replay window
- **Nonce TTL = 120 seconds:** 2x window để đảm bảo nonce hết hạn sau khi timestamp window đóng
- **Nonce format:** Client tự generate (UUID v4 hoặc crypto-random 16 bytes hex)
- **IMemoryCache:** Đủ cho single-server. Khi scale multi-server → swap sang IDistributedCache (Redis)
- **Memory pressure:** Set `SizeLimit` trên MemoryCache để prevent OOM (estimate: 10K nonces × 50 bytes = 500KB max)

### Constraints
- [ ] **UTC only:** Server và client đều dùng Unix timestamp UTC
- [ ] **No NTP dependency:** Accept 60s drift — covers most scenarios
- [ ] **Idempotent reject:** Same nonce → always 401 (no side effects)
- [ ] **Thread-safe:** IMemoryCache is thread-safe by default
- [ ] **Graceful degradation:** If cache full, oldest entries evicted (acceptable — worst case: a very old replay might pass but timestamp check catches it)

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** Request với timestamp > 60s drift → 401
- [ ] **SC2:** Request với timestamp < 60s drift → pass (nếu signature valid)
- [ ] **SC3:** Request lặp lại cùng nonce + apiKeyId → 401 (replay detected)
- [ ] **SC4:** Request cùng nonce nhưng khác apiKeyId → pass (nonce is per-client)
- [ ] **SC5:** Sau 120s, nonce cũ hết hạn → có thể reuse (lý thuyết, nhưng timestamp check sẽ fail)
- [ ] **SC6:** `dotnet build VanAn.sln` → 0 errors
- [ ] **SC7:** Memory usage stable dưới 1MB cho nonce cache (normal load)
- [ ] **SC8:** PRODUCTION_HYGIENE_master_plan.md updated W14-T2 = ✅ DONE

**Implementation Date:** TBD
**Branch:** feature/wave14-api-request-signing

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Verify không violate architecture
- `build-error-analysis` — Fix compile errors
- `test-system-upgrade` — Write comprehensive replay tests

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 3
- **Verified Facts:**
  - Fact 1: .NET 8 IMemoryCache available via `Microsoft.Extensions.Caching.Memory`
  - Fact 2: Timestamp validation logic confirmed trong SRS (60s window)
  - Fact 3: Không có IMemoryCache registration hiện tại trong Gateway — cần add
- **Assumptions:**
  - Single-server deployment (IMemoryCache đủ dùng)
- **Open Questions:**
  - Q1: Memory limit cho nonce cache?
- **Recommended Action:** IMPLEMENT — straightforward caching pattern

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `2_Gateway/Program.cs` | Thêm `services.AddMemoryCache()` | Low risk — standard .NET DI |
| `2_Gateway/Middleware/HmacSignatureMiddleware.cs` | Inject IReplayProtectionService | Already planned in W14-T1 |
| `2_Gateway/Services/ReplayProtectionService.cs` | File mới | N/A |

## 9. TDD & E2E TESTING STRATEGY
- **Unit tests:** Test ReplayProtectionService isolation
- **Test boundary:**
  - Unit tests: `6_Tests/VanAn.Core.Tests/Security/ReplayProtectionServiceTests.cs`
  - Integration tests: Covered by W14-T5
  - E2E tests: N/A
- **Minimum test coverage:** 6 unit tests (valid timestamp, expired timestamp, valid nonce, duplicate nonce, cross-client nonce, edge case at boundary)

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Confirm IMemoryCache DI pattern | Create `IReplayProtectionService` + implementation |
| **S2** | Integrate into HmacSignatureMiddleware | Wire up DI, test timestamp + nonce validation |

### Rules
- Timestamp parse failure → 401 (không default)
- Nonce empty/null → 401
- Log: `"Replay detected: ApiKeyId={id}, Nonce={nonce}"` (không log sensitive data)

## 11. ESTIMATED EFFORT
- Low-Medium effort — standard caching pattern
- 1 session theo JIT Planning
- **BLOCKER:** W14-T1 (middleware phải exist trước) — nhưng service có thể code độc lập
