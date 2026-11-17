# Task ATRS-15: Rollout, RV Protocol & Completion Review

> **Master plan:** §4 · **Phụ thuộc:** ATRS-14 · **Mode:** IMPLEMENT + REVIEW
> **SRS coverage:** §16 Phase 5, §19 DoD, §17.4 deliverables, RV protocol `.devin/rules/runtime-verification.md`

## 1. OBJECTIVE

Rollout an toàn theo feature flag + hoàn tất deliverables + RV 5-layer + DoD review trước khi tuyên bố hoàn thành.

## 2. PHẠM VI

### 2.1 Rollout
- Feature flag `AtrsEnabled` per app/profile (reuse `FeatureFlagService`/`ShopFeatureSettings`/`KhachLinkProfile` gating đã spec ở ATRS-11).
- Thứ tự bật: internal tenant test → 1 KhachLink instance pilot → dần rộng. Mỗi bước có rollback = tắt flag (không xóa data).
- Backfill TrustSubject lazy (ATRS-2) — không bulk.

### 2.2 Deliverables checklist (§17.4 — 12 items)
- [x] `BASE_CODE_GAP_ANALYSIS_ATRS.md` (ATRS-0)
- [ ] ADR deployment+identity (ATRS-1)
- [ ] ERD/data model doc
- [ ] API contract/OpenAPI cập nhật
- [ ] Migration scripts + rollback notes
- [ ] Backend implementation
- [ ] Frontend/PWA integration
- [ ] Unit/integration/E2E tests
- [ ] Security test report (ATRS-14)
- [ ] Deployment & rollback guide
- [ ] Operational runbook recovery/dispute (draft ở ATRS-14 → hoàn thiện)
- [ ] Known limitations + residual risks list

### 2.3 RV 5-layer (bắt buộc sau mỗi deploy — governance)
- L1 API checks → L2 static assets → L3 Playwright runtime → L4 UI flow → L5 manual browser. STOP at first failure.
- RV accounts: KHÔNG bật MFA trên account RV chuẩn; dùng account chuyên dụng.
- `DevToken__Secret`: unset mặc định, inline env only, remove + verify sau RV window.

### 2.4 DoD review (§19)
- [ ] Integrate UserId/auth hiện hữu, không identity silo
- [ ] Device Registry + states hoạt động
- [ ] WebAuthn thật trên platform công bố hỗ trợ (hoặc documented unsupported)
- [ ] Recovery workflow có risk control
- [ ] Secret challenge không single-factor (nếu có)
- [ ] Audit trail + phân quyền phân lớp
- [ ] Dispute workflow + ledger reference; không sửa số dư từ ATRS
- [ ] AC trọng yếu có test
- [ ] Security/privacy review xong
- [ ] Backup/restore + ops + monitoring + rollback plan
- [ ] PO chấp thuận open decisions còn lại

## 3. FILES

- `docs/ops/atrs-runbook.md`, `docs/security/atrs-security-test-report.md`
- `docs/AI/project_state.md` update
- Deployment configs: `docker-compose.*.yml` + `cd-multivps.yml` matrix (nếu service riêng — D2)

## 4. ACCEPTANCE

- [ ] Tất cả deliverables §17.4 tồn tại
- [ ] RV 5-layer PASS trên prod
- [ ] DoD checklist §19 tick đủ hoặc có documented exception + PO sign-off

## 5. RISKS

- Rollout customer-facing recovery = high blast radius → flag + pilot bắt buộc, không big-bang.
- Runbook phải cover: mất hết credential manual review, dispute adjust fail, ATRS DB restore.

## 5. IMPLEMENTATION STATUS (Wave 6 — 2026-11-17) — ✅ ROLLOUT DOCS + DoD REVIEW DONE (PO sign-off pending)

**Status: deliverables §17.4 gần đủ; DoD review xong; PO chấp thuận open decisions còn lại = residual.**

Đã làm:
- Deliverables: `atrs-adr-001-deployment-and-identity.md` (ATRS-1) ✅ · `docs/design/atrs-data-model.md` (ERD/data model) ✅ · `docs/ops/atrs-runbook.md` (deploy/rollback/recovery/dispute/DB restore/troubleshoot) ✅ · `docs/security/atrs-security-test-report.md` (security report + known limitations/residual risks) ✅ · migrations + rollback notes ✅ · backend + frontend + tests ✅ (W1–W5).
- DoD checklist §19 review: 11/11 — 9 ✅, 2 ⚠️ (WebAuthn L5 manual device thật chờ user; legal review chờ PO).
- Rollout flag: ATRS đã live cho RV test tenants (W1–W5); formal per-profile gating (KhachLinkProfile) = residual nếu rollout rộng.
- RV 5-layer: đã chạy từng wave (sweep + Playwright + manual ops).

**PO cần sign-off:** retention §18 Q7 · legal checklist · open decisions (email P4, Zalo slot) · WebAuthn L5 manual.
