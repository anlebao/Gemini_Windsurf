# TASK CARD: P1.1 — AUDIT: CAPTURE IP/USERAGENT + TAMPER-EVIDENT (HASH-CHAIN)

> Created: 2026-10-16 (user directive master plan P0-P2)
> Master plan: `docs/AI/plans/accounting-legal-compliance-master-plan.md` (Phase P1.1)
> Branch: `main`
> Status: ✅ **Q3 CHỐT (2026-10-16): Option A (IP/UserAgent) — hash-chain (B) DEFER — READY IMPLEMENT (Wave 4)**

---

## 1. GOAL & CONTEXT

- **Mục tiêu:** đóng lỗ hổng checklist **1.1** — (A) ghi `IP Address` + `UserAgent` cho mọi thao tác Create/Update/Delete (hiện chỉ SecurityEvent có IP); (B, tùy chọn) chống giả mạo log bằng **hash-chain** (mỗi dòng log chứa hash của dòng trước) — đáp ứng yêu cầu "cảnh báo/ngăn chặn can thiệp có chủ ý" Điều 28.1.c TT 99/2025.
- **Phát hiện review (2026-10-16):** `AuditLog` domain ĐÃ có cột `IpAddress`/`UserAgent` (`AuditLog.cs:66-67`) nhưng `AuditTrailService.LogCreateAsync/LogUpdateAsync/LogDeleteAsync/LogPeriodCloseAsync/LogReopenAsync/LogCorrectionAsync/LogReversalAsync` **KHÔNG truyền** ipAddress/userAgent (chỉ `LogSecurityEventAsync` truyền).
- **Đã xác minh code:** `AuditTrailService.cs` có `_httpContextAccessor` (đọc được IP qua `HttpContext.Connection.RemoteIpAddress` + `UserAgent` từ header) · `AuditLog` immutable (factory methods nhận ipAddress/userAgent sẵn) · migration pipeline PG (VanAnDbContext) có sẵn.

## 2. ACTIVE WORKFLOW ROUTING

- **Workflow:** `newfeaturebuild.md` — Wave 3.
- **Execution Mode:** IMPLEMENT (sau Q3).
- **Skills (max 3):** `domain-integrity-validation` (append-only) + `pattern-based-fixing` + `test-system-upgrade`.

## 3. SCOPE

### 3.1 Option A — IP/UserAgent capture (bắt buộc)
- [ ] `AuditTrailService`: helper `GetIpAddress()` (`HttpContext.Connection.RemoteIpAddress?.ToString()` + X-Forwarded-For qua Gateway) + `GetUserAgent()` (`Request.Headers["User-Agent"]`) → truyền vào **mọi** factory call (Create/Update/Delete/PeriodClose/Reopen/Correction/Reversal).
- [ ] Fallback `null` khi background scope (NATS/hosted) — không crash (fail-safe pattern đã có).
- [ ] UI `AuditTrail.razor`: hiển thị cột IP/UserAgent nếu có.

### 3.2 Option B — Hash-chain tamper-evident — ⏸️ DEFER (Q3 2026-10-16: chỉ làm A)
- [ ] (Deferred — triển khai khi cần chuẩn "cảnh báo can thiệp có chủ ý" Điều 28 TT 99/2025) Domain `AuditLog` += `PrevHash`/`Hash` (SHA-256) · migration `AddAuditLogHashChain` + backfill · `IVerifyAuditIntegrityService` + UI "Kiểm tra vết kiểm toán" · tests phát hiện giả mạo.

## 4. DECISIONS — ✅ Q3 CHỐT (2026-10-16)

| # | Câu hỏi | ✅ QUYẾT ĐỊNH |
|---|---|---|
| Q3 | Chỉ IP capture (A) hay kèm hash-chain (B)? | ✅ **Chỉ A (IP/UserAgent)** — không migration, an toàn; B defer, làm khi cần chuẩn mạnh theo Điều 28 TT 99/2025 |

## 5. CONSTRAINTS (governance)

- **AccountingEntry/AuditLog immutable** — Option B CHỈ thêm property + factory (append-only pattern) — nếu Domain.cs bị đụng → Gate 5 approval (đây là modeling defect hợp lệ: bổ sung trường hash cho AuditLog) · KHÔNG phá toggle feature flag audit (Audit_Enabled) · Multi-tenancy · guard + build + tests MUST PASS.

## 6. SUCCESS CRITERIA (DoD)

- [ ] (A) Mọi AuditLog Create/Update/Delete/PeriodClose/Reopen/Correction/Reversal có IP + UserAgent
- [ ] (B — DEFER theo Q3) Hash-chain + kiểm tra toàn vẹn + test phát hiện giả mạo (khi có nhu cầu)
- [ ] guard + build + tests PASS · Deploy + RV PASS → đóng

## 7. DEFERRED

- WORM storage / export log bất biến ra file ký số · gắn mã xác thực HMAC với khóa ngoài (nếu cần chuẩn cao hơn)