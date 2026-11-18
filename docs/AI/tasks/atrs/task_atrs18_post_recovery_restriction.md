# Task ATRS-18: Post-Recovery Sensitive-Transaction Restriction (FR-011)

> **SRS:** `docs/requirements/VAN_AN_Account_Trust_and_Recovery_Service_v1.0.md` — FR-011 ("delay/restrict giao dịch nhạy cảm sau recovery rủi ro cao")
> **Parent:** `task_atrs8_recovery_workflow.md` — hook đã comment "chưa wire" từ W1 (`RecoveryCaseManager.cs:14`)
> **Traceability gap:** `atrs_srs_traceability_review_2026-11-17.md` gap #1
> **Depends on:** ATRS-7 (RiskPolicyEngine — `TEMPORARY_RESTRICT`/`REQUIRE_MANUAL_REVIEW` đã có), ATRS-10 (RBAC/audit), W5c SecurityPolicy (A21 — policy versioned)
> **Mode:** IMPLEMENT (khi duyệt) · **Layer:** 11_Atrs (domain+infra+api) + ShopERP/Gateway enforcement points

## 1. OBJECTIVE

Sau khi recovery case ở risk cao được approve/complete, subject bị **hạn chế giao dịch nhạy cảm** trong cửa sổ thời gian (cooldown), chống kịch bản "attacker vừa chiếm account qua recovery → chuyển/rút ngay". Restriction phải: policy-driven (A21), audit đầy đủ, expire tự động, và **enforce được ở nơi giao dịch thật xảy ra** (không chỉ atrs).

## 2. VERIFIED FACTS

- `RecoveryRequest` có `RiskDecisionJson` (policy version + reason codes) — đủ tín hiệu để quyết định restrict; `RiskDecisionKind.TEMPORARY_RESTRICT` đã tồn tại trong `RiskPolicyEngine` (R4 takeover) nhưng **không ai consume**.
- `RecoveryCaseManager.CompleteAsync` đã wire: TokenVersion bump (`ITokenVersionBumpNotifier`) + notification + metrics — restriction hook đặt cạnh đây.
- `SecurityPolicyService` (W5c) đã có maker-checker + versioned policy → thêm key `recovery.restriction.*` không cần schema mới.
- **Điểm enforce nhạy cảm nằm NGOÀI atrs:** loyalty points adjust/transfer/refund đi qua Gateway `ILoyaltyGatewayClient`/`AdjustPointsAsync` (đã dùng cho dispute); device register/recovery codes generate nằm TRONG atrs (dễ enforce trực tiếp); payment info change = ShopERP.
- atrs↔Gateway đã có kênh: `X-Internal-Api-Key` internal endpoint pattern (ATRS-17 `POST /api/v1/internal/account-status` vừa ship — cùng pattern dùng lại).

## 3. DESIGN (đề xuất — chốt khi implement)

### 3.1 atrs side — restriction record

- Entity mới `AccountRestriction` (append-only + 1 active/subject): `Id, TrustSubjectId, RecoveryRequestId?, Scope (enum: SENSITIVE_TX|DEVICE_ENROLL|ALL), ReasonCode, PolicyVersion, RestrictedUntil, LiftedAt?, LiftedBy?, CreatedAt`. Single-identity pattern (Id = business key, không VO riêng — hoặc VO `RestrictionId` theo convention hiện có, theo đúng audit checklist governance).
- Tạo restriction khi: `CompleteAsync` (hoặc `ApproveAsync`) và risk decision ∈ {`TEMPORARY_RESTRICT`, `REQUIRE_MANUAL_REVIEW`} **hoặc** policy `recovery.restriction.min_risk` thấp hơn — policy quyết định, không hardcode.
- Cooldown duration từ policy `recovery.restriction.cooldown_hours` (default đề xuất 24-72h — chốt với user).
- Lift paths: hết hạn (lazy check) · staff manual lift (`restriction.lift` permission mới, maker-checker nếu policy yêu cầu) · user re-verify step-up mạnh (passkey) — optional phase 2.
- Audit: mọi create/lift → `AccountSecurityEvent` (ActorType System/Staff).

### 3.2 Enforcement — 2 lớp

**Lớp A — trong atrs (dễ, fail-closed):**
- `POST /webauthn/register/*` (device enroll), `POST /recovery-codes/generate`, dispute submit loại sensitive → check `AccountRestriction` active → 403 `account-restricted` + `retryAfter`.
- Implement qua 1 `IRestrictionGuard` inject vào các service tương ứng.

**Lớp B — ngoài atrs (contract mới):**
- Internal endpoint `GET /api/v1/internal/restrictions/check?subjectId={id}` (X-Internal-Api-Key) → `{ restricted, until, scope }` — mirror pattern ATRS-17.
- **ShopERP/Gateway enforcement:** trước `AdjustPointsAsync` (manual adjust, transfer, refund-dispute paths) + payment-info change endpoints → gọi check. Fail policy: **fail-open + log/metric** (không chặn giao dịch vì atrs sập — quyết định này cần user chốt; fail-closed an toàn hơn nhưng atrs down = mất doanh thu).
- Alternative (ghi nợ, không làm v1): atrs push event `account.restricted` qua NATS → consumer cache local — tránh per-request HTTP.

### 3.3 UI

- Customer: khi bị restrict, API trả `account-restricted` → KhachLink hiển thị banner "Tài khoản đang trong thời gian bảo mật sau khôi phục — thử lại sau {time}" (thêm vào security/notifications surface hiện có).
- Staff: recovery detail + queue item hiển thị badge `RESTRICTED until …`; permission `restriction.lift` mới trong `AtrsPermissions` + grant UI (A22 roles page).

## 4. FILES (dự kiến)

| File | Change |
|---|---|
| `VanAn.Atrs.Domain/AccountRestriction.cs` (+enum Scope) | MỚI — entity append-only |
| `AtrsDbContext` + migration `AddAccountRestrictions` | additive |
| `RecoveryCaseManager.CompleteAsync`/`ApproveAsync` | wire create restriction theo risk+policy |
| `IRestrictionGuard` + impl | check active restriction |
| `RestrictionsController` (internal check) + staff lift endpoint | `X-Internal-Api-Key` pattern ATRS-17 |
| `AtrsPermissions` += `restriction.lift` | + `StaffPermissionGrant` seed/migration data |
| `SecurityPolicyService` defaults | keys `recovery.restriction.*` |
| ShopERP/Gateway: `AtrsRestrictionClient` + guard ở sensitive endpoints | client pattern `MeteringApiClient`/`AtrsAdminApiClient` |
| KhachLink: restricted banner | nhỏ |
| Tests | entity transitions · guard · policy-driven create · internal endpoint auth · ShopERP client fail-open |

## 5. ACCEPTANCE CRITERIA

- [ ] Recovery COMPLETED với risk ≥ ngưỡng policy → `AccountRestriction` active với `RestrictedUntil` đúng cooldown
- [ ] Risk thấp (R0/R1) → KHÔNG restrict (policy `min_risk` tôn trọng)
- [ ] atrs sensitive endpoints trả 403 `account-restricted` khi active; OK sau expiry/lift
- [ ] `GET internal/restrictions/check` đúng + 401 không key; ShopERP adjust-points bị chặn khi restricted (RV)
- [ ] atrs down → ShopERP sensitive op vẫn chạy (fail-open) + metric `restriction_check_fail_open` tăng
- [ ] Staff lift cần `restriction.lift` + audit event; maker-checker nếu policy bật
- [ ] Migration additive; tests + guard + build PASS

## 6. RISKS / OPEN QUESTIONS (chốt trước IMPLEMENT)

1. **Fail-open vs fail-closed** ở ShopERP/Gateway — đề xuất fail-open (availability) nhưng user quyết.
2. **Cooldown default** (24h? 72h?) + `min_risk` threshold — đề xuất policy defaults, tune sau pilot.
3. **Scope** v1: chỉ `SENSITIVE_TX` (points/adjust/payment) hay cả `DEVICE_ENROLL`? Đề xuất cả hai — enroll device mới ngay sau recovery là attack path rõ.
4. Enforcement points chính xác ở ShopERP/Gateway cần liệt kê trong ANALYZE phase (adjust points, transfer, refund, payment method, bank account).
5. Restriction có nên cover **dispute adjust** (maker-checker đã có) không? — tránh double-gate, đề xuất KHÔNG (dispute đã có maker-checker riêng).

## 7. VERIFICATION + RV

- Local: `dotnet build VanAn.sln` + `guard-check.ps1` + `dotnet test 11_Atrs/vanan-atrs/tests` + Core/ShopERP tests liên quan.
- RV prod (sweep-first): tạo recovery case risk cao trên customer test → approve → complete → verify restriction row + atrs endpoint 403 + ShopERP adjust bị chặn + expiry/lift path + fail-open khi stop atrs (nếu test được an toàn).
- Playwright: chỉ nếu có UI surface mới đáng kể (banner) — theo playwright.rules.

## 8. TRACEABILITY

| FR-011 clause | Cover |
|---|---|
| delay/restrict giao dịch nhạy cảm sau recovery rủi ro cao | toàn bộ card |
| policy-driven | §3.1 SecurityPolicy keys + W5c maker-checker |
| audit | AccountSecurityEvent mọi transition |
| revoke token cũ (đã có) | giữ nguyên — restriction bổ sung, không thay thế |

**Status:** REVIEW_ONLY — chờ user duyệt + trả lời §6 open questions.
**Naming:** ATRS-18 (tiếp ATRS-17 account-status sync).
**Lưu ý dependency:** nên làm SAU hoặc CÙNG đợt với ATRS-19 (cùng đụng recovery flow) để tránh migration conflict.
