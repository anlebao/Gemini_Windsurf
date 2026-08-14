# TASK CARD — Sprint 0: ANALYZE (Issue #126)

> **Status:** 📋 PENDING
> **Priority:** P0 — Must complete before Sprint 1
> **Branch:** `feature/guard-qr-verify` (create from `main` @ `f7201ef4`)
> **Mode:** ANALYZE (no code changes)
> **Domain modification:** NO (investigate only)

## Objective
Verify 6 integration points + draft 8 BR spec + confirm external dependencies (R2, printer, QR libs). Output: `sprint0_findings.md` approved trước khi vào Sprint 1.

## Prerequisites
- [ ] Master plan reviewed + approved
- [ ] Issue #126 read
- [ ] Current `Scan.cshtml` reviewed (confirmed 100% hardcode)

## Investigation Tasks

### Task 1: Verify UserRole.Guard integration
**Context:** Sprint 2 cần `[Authorize(Roles="Guard")]` trên GuardController.
**Steps:**
1. Read `1_Shared/Domain/Aggregates/UserAggregate/UserRole.cs` — confirm `Guard=3` exists
2. Grep `Roles="Guard"` — ai dùng hiện tại? (NavMenu.razor đã có)
3. Check auth pipeline — JWT claim có role "Guard" không?
4. Check seed data — có user Guard test không?
**Output:** Confirm Guard role ready cho API auth.

### Task 2: Investigate Cloudflare R2 setup
**Context:** Sprint 1 cần R2 client. Cần confirm account + bucket.
**Steps:**
1. Check existing config — có R2/S3 config nào trong `appsettings.json` chưa?
2. Grep `AWSSDK.S3` — đã có nuget package nào chưa?
3. Confirm với user: R2 account created? Bucket `vanan-guard-photos` created? API token created?
4. Document R2 endpoint format: `https://<account_id>.r2.cloudflarestorage.com`
**Output:** R2 credentials checklist + endpoint URL.

### Task 3: Investigate QR generation library
**Context:** Sprint 2 cần generate QR PNG từ QrPayload.
**Steps:**
1. Check `nuget` — `QRCoder` đã installed chưa?
2. Alternative: `Net.Codecrete.QrCodeGenerator` (MIT, lightweight)
3. Confirm QR payload format: JSON `{sessionId, token, tenantId}` hay raw token string?
4. Check QR size cho thermal printer 58mm (384 dots width → QR ~200px)
**Output:** QR library choice + payload format spec.

### Task 4: Investigate camera QR scan library (frontend)
**Context:** Sprint 3 (Guard) + Sprint 4 (KhachLink) cần camera scan QR.
**Steps:**
1. Check `package.json` (KhachLink) — có `jsQR` hoặc `@zxing/browser` chưa?
2. Check ShopERP — có JS QR lib nào chưa?
3. Compare: `jsQR` (pure JS, 47KB) vs `@zxing/browser` (fuller features, 200KB+)
4. Confirm browser compat: WebRTC `getUserMedia` + Canvas — WASM/Blazor OK?
**Output:** QR scan library choice cho Guard (ShopERP) + KhachLink.

### Task 5: Investigate thermal printer integration
**Context:** Sprint 5 cần in vé qua WebUSB ESC/POS.
**Steps:**
1. Confirm printer model với user (recommend: Xprinter XP-58IIH)
2. Check WebUSB browser support — Chrome/Edge OK, Safari/Firefox limited
3. Research ESC/POS commands: init, print text, print QR bitmap, cut
4. Check existing JS lib: `escpos.js` hoặc manual byte construction
5. Confirm ticket layout: tenant name (bold), plate (large), time/date, QR bitmap
**Output:** Printer integration approach + ESC/POS command sequence.

### Task 6: Investigate EF migration impact
**Context:** Sprint 1 thêm 2 bảng mới. Cần confirm không conflict.
**Steps:**
1. Check latest migration — số migration hiện tại?
2. Check PG (Gateway) vs SQLite (ShopERP) — migration apply ở cả 2?
3. Confirm: VehicleSessions + GuardScanLogs ở PG (Gateway source of truth) hay SQLite?
4. Per Option C: Guard API ở Gateway → tables ở PG
**Output:** Migration target DB + table placement decision.

### Task 7: Draft 8 BR spec
**BR list:**
```
BR-G01  QR Issuance (guard creates QR with photos)
BR-G02  QR Claim — Camera (KhachLink scans QR from guard screen)
BR-G03  QR Claim — Short Code (6-digit fallback)
BR-G04  Paper Ticket (thermal print, no phone needed)
BR-G05  QR Verification (guard scans customer QR → match photos)
BR-G06  Check-out (guard confirms match → close session)
BR-G07  Flag/Suspicious (guard flags mismatch → alert)
BR-G08  Void (guard/admin voids stale session)
```
**Spec format per BR:** Rule, Enforcement, Phase, Invariant, Edge cases.

## Output Deliverable
File: `docs/AI/tasks/guard_qr_verify/sprint0_findings.md`

### Section 1: UserRole.Guard Verification
### Section 2: R2 Setup Checklist
### Section 3: QR Generation Library Choice
### Section 4: Camera QR Scan Library Choice
### Section 5: Printer Integration Approach
### Section 6: EF Migration Target + Table Placement
### Section 7: 8 BR Spec
### Section 8: Sprint 1 Domain Entity Final Field List

## Verification
- [ ] `sprint0_findings.md` written
- [ ] User approves 8 BR spec
- [ ] R2 account confirmed
- [ ] Printer model confirmed
- [ ] Sprint 1 field list confirmed (no drift from master plan)

## Rollback
N/A — investigation only.
