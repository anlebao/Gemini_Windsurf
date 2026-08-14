# MASTER PLAN: Guard QR Verification (Issue #126)

> **Created:** 2026-08-14
> **Last Updated:** 2026-08-14 (Sprint 0 + Sprint 1 complete)
> **Source:** GitHub Issue #126 — "Guard page đang hardcode"
> **Branch:** `feature/guard-qr-r1`
> **Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT, 7 steps)
> **Domain modification:** YES — approved by user 2026-08-14 (VehicleSession + GuardScanLog aggregates)

## Sprint Status

| Sprint | Status | Notes |
|---|---|---|
| Sprint 0 — Analyze | ✅ COMPLETE | 6 integration points verified, 8 BR spec drafted, R2 bucket created + verified |
| Sprint 1 — Domain + Infrastructure | ✅ COMPLETE | Domain entities + EF config + migration + R2 service + repositories + DI. Build pass + guard-check pass |
| Sprint 2 — Gateway API | ✅ COMPLETE | GuardController (9 endpoints) + GuardService + QR generation + R2 presigned URL flow |
| Sprint 3 — Guard UI | ✅ COMPLETE | Blazor Scan.razor (3 tabs: Issue/Verify/Today) + GuardApiClient + guard-camera.js |
| Sprint 4 — KhachLink Claim | 📋 PENDING | QR claim page (Channel A/B/C→A) |
| Sprint 5 — Printer | ✅ COMPLETE | PrintTicket.razor (58mm thermal, auto-print, QR on canvas) |
| Sprint 6 — Tests | 📋 PENDING | E2E + integration tests |

---

## 1. BUSINESS FLOW (Final — approved 2026-08-14)

```
[Issuance — Guard app]
  1. Bảo vệ mở Guard Scanner → chụp ảnh biển số + chụp ảnh khách
  2. Ảnh upload trực tiếp lên Cloudflare R2 (presigned PUT, TTL 15min)
  3. Guard app → POST /api/guard/issue {plateNumber, platePhotoKey, customerPhotoKey, customerPhone?}
  4. Gateway tạo VehicleSession(Issued, QrToken hash, photos) → trả:
     - QrPayload (string — encode vào QR image)
     - ShortCode (6-digit, human-readable fallback)
     - SessionId
  5. Guard app hiển thị:
     - QR lớn trên màn hình (cho khách quét bằng KhachLink)
     - 6-digit short code (fallback)
     - Nút "In vé" → in thermal ticket

[Delivery — 3 channels, khách chọn 1 hoặc chuyển đổi sau]
  Channel A — KhachLink Claim (primary):
    Khách mở KhachLink → "Nhận QR gửi xe" → camera quét QR trên màn hình Guard
    → POST /api/guard/claim {qrToken, customerId} → QR vào "Ví QR"
  Channel B — 6-digit code (fallback, no camera):
    Khách mở KhachLink → "Nhận QR gửi xe" → nhập 6-digit code
    → POST /api/guard/claim {shortCode, customerId} → QR vào "Ví QR"
  Channel C — Paper ticket (no phone at issuance):
    Guard in thermal ticket chứa: biển số, giờ vào, ngày, tenant name, QR code
    Khách giữ giấy → lúc lấy xe đưa giấy cho guard quét lại

[Channel C → A migration — khách rảnh sau, muốn số hóa vé]
  Khách nhận paper ticket (Channel C) → CustomerId = null (chưa link)
  Sau đó, lúc rảnh, khách mở KhachLink → "Nhận QR gửi xe" → camera quét QR trên giấy vé
  → POST /api/guard/claim {qrToken, customerId}
  → Nếu session vẫn Issued (chưa checkout): Claim thành công → QR vào Ví QR
    → Từ đây khách dùng Channel A (digital) thay vì giấy — chống ướt/rách/mất
  → Nếu session đã CheckedOut (đã lấy xe): Claim fail → thông báo "Vé đã sử dụng"
  → Nếu session đã Voided: Claim fail → thông báo "Vé đã hết hạn"
  Note: Cùng 1 QrToken — khách có thể quét từ màn hình Guard (A) HOẶC từ giấy vé (C→A)
        Không cần tạo QR mới, chỉ là claim trễ

[Verification — Guard app, lúc khách lấy xe]
  6. Khách đưa QR (KhachLink screen) HOẶC paper ticket cho guard
     - Nếu khách đã Channel C→A: đưa QR trên KhachLink (paper có thể đã rách/mất)
     - Nếu khách vẫn Channel C: đưa giấy vé
  7. Guard app quét QR → POST /api/guard/verify {scannedQrPayload}
  8. Gateway lookup VehicleSession by QrToken hash:
     - Match → trả {plateNumber, platePhotoUrl, customerPhotoUrl, status, issuedAt, claimedBy?}
     - Mismatch/voided → trả error
  9. Guard app hiển thị: biển số + 2 ảnh (biển số, chân dung) → guard manual check
     + Nếu claimedBy != null → hiển thị "Đã liên kết KhachLink" badge (khách đã số hóa)
  10. Guard bấm "Match" → POST /api/guard/checkout {sessionId}
      → VehicleSession → CheckedOut, GuardScanLog ghi lại
      Guard bấm "Mismatch" → POST /api/guard/flag {sessionId, reason}
      → VehicleSession → Flagged, GuardScanLog ghi lại, alert admin
```

**QR 1 lần:**
- Sau `claim` (Channel A/B hoặc C→A migration): QrToken lock với CustomerId — không ai claim lại được
- Sau `checkout` hoặc `void`: QrToken voided — không dùng lại được
- Paper ticket (Channel C, chưa claim): cùng QrToken, guard quét trực tiếp từ giấy
- **C→A migration:** Khách claim trễ bất cứ lúc nào (trừ khi đã CheckedOut/Voided) — cùng QrToken, không tạo mới

---

## 2. ARCHITECTURE DECISIONS

| Decision | Rationale |
|---|---|
| **Image storage: Cloudflare R2** | Free 10GB + egress FREE + S3-compatible (AWSSDK.S3) + đã có Cloudflare account |
| **QR delivery: Claim (A) + 6-digit (B) + Paper (C)** | 3 channel phủ 100% khách — có phone / không camera / không phone |
| **QR token: hash-only in DB** | Không lưu raw QR payload, chỉ lưu SHA256 hash — chống replay nếu DB leak |
| **Photos: presigned URL upload** | Guard app upload thẳng R2, không qua Gateway → giảm load bandwidth |
| **Photos access: presigned GET (TTL 1h)** | Không public URL — bảo vệ privacy khách, URL hết hạn tự động |
| **Printer: thermal 58mm, ESC/POS** | Phổ biến ở VN, rẻ (~500K VND), driver chuẩn ESC/POS, WebUSB API |
| **Domain: VehicleSession + GuardScanLog** | Approved — 2 aggregate mới, Single-Identity pattern |
| **UI Platform compliance** | Guard page rewrite từ .cshtml thô → Blazor component + UI Platform (Gate 5) |

---

## 3. DOMAIN MODEL (approved)

### VehicleSession (Aggregate Root)
```
- Id (PK, UUIDv7)           ← Single-Identity pattern
- TenantId (Guid)
- PlateNumber (string)       // biển số xe
- CustomerId (Guid?)         // null nếu Channel C (paper only)
- CustomerPhone (string?)    // optional, cho Channel B lookup
- PlatePhotoKey (string)     // R2 object key
- CustomerPhotoKey (string)  // R2 object key
- QrTokenHash (string)       // SHA256 hash of QR payload
- ShortCode (string)         // 6-digit, human fallback
- Status: VehicleSessionStatus (enum: Issued | Claimed | CheckedOut | Voided | Flagged)
- IssuedBy (Guid)            // GuardId
- IssuedAt (DateTimeOffset)
- ClaimedBy (Guid?)          // CustomerId, set khi claim
- ClaimedAt (DateTimeOffset?)
- CheckedOutBy (Guid?)       // GuardId
- CheckedOutAt (DateTimeOffset?)
- FlagReason (string?)
- VoidedAt (DateTimeOffset?)

Methods:
- Create(tenantId, plateNumber, photos, issuedBy, qrTokenHash, shortCode) → Issued
- Claim(customerId) → Claimed (throw nếu đã claimed)
- Checkout(guardId) → CheckedOut (throw nếu chưa Issued/Claimed)
- Flag(reason, guardId) → Flagged
- Void() → Voided
```

### GuardScanLog (Entity, không phải aggregate root)
```
- Id (PK, UUIDv7)
- TenantId (Guid)
- VehicleSessionId (Guid)    // FK → VehicleSession.Id
- ScannedQrTokenHash (string)
- MatchResult: GuardScanResult (enum: Match | Mismatch | ManualOverride | Flagged)
- ScannedBy (Guid)           // GuardId
- ScannedAt (DateTimeOffset)
- Notes (string?)
```

### Value Objects
```
- VehicleSessionId (VO, inherits BaseEntity) — Ignore in EF config
- GuardScanLogId (VO, inherits BaseEntity) — Ignore in EF config
```

### Enums
```
- VehicleSessionStatus { Issued=0, Claimed=1, CheckedOut=2, Voided=3, Flagged=4 }
- GuardScanResult { Match=0, Mismatch=1, ManualOverride=2, Flagged=3 }
```

---

## 4. API CONTRACT (Gateway — new)

| Endpoint | Method | Auth | Input | Output |
|---|---|---|---|---|
| `/api/guard/presign-upload` | POST | Guard | `{contentType, suffix}` | `{platePhotoPutUrl, customerPhotoPutUrl, platePhotoKey, customerPhotoKey}` |
| `/api/guard/issue` | POST | Guard | `{plateNumber, platePhotoKey, customerPhotoKey, customerPhone?}` | `{sessionId, qrPayload, shortCode, issuedAt}` |
| `/api/guard/claim` | POST | Customer (JWT) | `{qrPayload? \| shortCode?}` | `{sessionId, plateNumber, platePhotoUrl, customerPhotoUrl, issuedAt}` |
| `/api/guard/verify` | POST | Guard | `{scannedQrPayload}` | `{sessionId, plateNumber, platePhotoUrl, customerPhotoUrl, status, issuedAt, claimedBy?}` |
| `/api/guard/checkout` | POST | Guard | `{sessionId}` | `{ok, checkedOutAt}` |
| `/api/guard/flag` | POST | Guard | `{sessionId, reason}` | `{ok, flaggedAt}` |
| `/api/guard/sessions/today` | GET | Guard | `?status=&page=&pageSize=` | `{items[], total, checkInCount, checkOutCount, inLotCount}` |
| `/api/guard/sessions/{id}` | GET | Guard | — | `{session detail}` |
| `/api/guard/void` | POST | Guard | `{sessionId, reason}` | `{ok, voidedAt}` |

---

## 5. SPRINT BREAKDOWN

### Sprint 0 — ANALYZE (1 session, no code)
**Goal:** Verify integration points, draft BR spec, confirm R2 account + printer model.
**Output:** `phase0_findings.md`
**Task card:** `sprint0_analyze_task_card.md`

### Sprint 1 — Domain + Infrastructure (2 sessions)
**Goal:** Add VehicleSession + GuardScanLog to Domain.cs, EF config, migration, R2 client setup.
**Phases:** Domain → Infrastructure
**Task card:** `sprint1_domain_infra_task_card.md`

### Sprint 2 — Gateway API + Services (2 sessions)
**Goal:** GuardController (9 endpoints) + GuardService + QR generation + R2 presigned URL logic.
**Phase:** Application
**Task card:** `sprint2_gateway_api_task_card.md`

### Sprint 3 — Guard UI (ShopERP) (2 sessions)
**Goal:** Rewrite Scan.cshtml → Blazor component (`Scan.razor` + `.razor.cs`) với:
- Camera QR scan (jsQR / zxing-js)
- Photo capture (plate + customer)
- Issue flow → display QR + short code
- Verify flow → display plate + 2 photos + Match/Mismatch buttons
- Today's sessions list (real data, thay hardcode)
- Stats (real data, thay hardcode 24/18/6)
- UI Platform components (Gate 5)
**Phase:** UI
**Task card:** `sprint3_guard_ui_task_card.md`

### Sprint 4 — KhachLink Claim (2 sessions)
**Goal:** KhachLink 2 trang mới:
- `/qr/claim` — camera quét QR (Channel A) + nhập 6-digit (Channel B)
- `/qr/wallet` — "Ví QR" — list claimed QR, tap to show QR fullscreen cho guard quét
**Phase:** UI
**Task card:** `sprint4_khachlink_claim_task_card.md`

### Sprint 5 — Printer Integration (1 session)
**Goal:** Guard app "In vé" button → WebUSB ESC/POS → thermal printer 58mm.
Ticket content: tenant name, biển số, giờ vào, ngày, QR code (bitmap).
**Phase:** UI
**Task card:** `sprint5_printer_task_card.md`

### Sprint 6 — Tests + E2E (1 session)
**Goal:** Unit tests (GuardService, VehicleSession domain logic) + Integration tests (API) + 1 Playwright E2E spec (full flow: issue → claim → verify → checkout).
**Task card:** `sprint6_tests_task_card.md`

---

## 6. EXECUTION ORDER

```
Sprint 0 (ANALYZE) → approval gate
  ↓
Sprint 1 (Domain + Infra) → build pass → approval gate
  ↓
Sprint 2 (Gateway API) → build pass → approval gate
  ↓
Sprint 3 (Guard UI) ←────┐
Sprint 4 (KhachLink) ────┤ (parallel-able, independent UI)
Sprint 5 (Printer) ←─────┘
  ↓
Sprint 6 (Tests + E2E) → CI pass → RV → done
```

**Total: ~9-10 sessions**

---

## 7. CONSTRAINTS & COMPLIANCE

- **Domain PURE:** VehicleSession + GuardScanLog trong `1_Shared/Domain.cs`, no EF Core attrs
- **Single-Identity:** Constructor set `Id = VehicleSessionId.Value`, EF `Ignore(e => e.VehicleSessionId)`
- **Multi-tenancy:** Mọi query filter by TenantId
- **UI Platform:** Guard page rewrite dùng UI Platform components (Gate 5)
- **E2E test:** UI layout change → E2E test (Gate 4)
- **Playwright isolation:** DISABLED during Sprint 1-5, enabled Sprint 6
- **AccountingEntry:** Not touched — immutable
- **R2 credentials:** Store in `appsettings.json` / env vars, NOT in code
- **QR token security:** Hash-only in DB (SHA256), raw payload returned to client only once at issuance

---

## 8. ROLLBACK PLAN

- Sprint 1: Migration `Down` method drops `VehicleSessions` + `GuardScanLogs` tables
- Sprint 2-5: Feature flag `Guard:QrVerifyEnabled` (default OFF) — toggle off to revert to old hardcode page
- Sprint 6: Tests isolated, no production impact

---

## 9. OPEN ITEMS (to resolve in Sprint 0)

- [ ] Confirm Cloudflare R2 account + create bucket `vanan-guard-photos`
- [ ] Confirm R2 API token (read+write scope, bucket-restricted)
- [ ] Confirm printer model (recommend: Xprinter XP-58IIH hoặc Epson TM-T20III)
- [ ] Confirm QR library: `QRCoder` (.NET, MIT license) cho backend generate QR PNG
- [ ] Confirm camera QR scan library: `jsQR` (JS, MIT) hoặc `@zxing/browser` (JS, Apache)
- [ ] Confirm 6-digit code collision strategy (random + DB unique check per tenant per day)

---

## 10. SUCCESS CRITERIA (Definition of Done)

- [ ] `dotnet build VanAn.sln` 0 errors
- [ ] `guard-check.ps1` ALL PASSED
- [ ] CI pipeline ALL PASS (existing tests + new tests)
- [ ] Guard page (`/guard/scan`) hiển thị real data — 0 hardcode
- [ ] Full flow works: issue (photo+QR) → claim (KhachLink) → verify (guard scan) → checkout
- [ ] Paper ticket prints correctly (tenant name, plate, time, QR)
- [ ] 6-digit fallback works
- [ ] **Channel C→A migration works:** khách nhận paper ticket → sau đó quét QR trên giấy bằng KhachLink → claim thành công → QR vào Ví QR (chống ướt/rách/mất giấy)
- [ ] **Channel C→A edge cases:** claim fail khi vé đã CheckedOut (đã lấy xe) hoặc Voided (hết hạn)
- [ ] Multi-tenant: tenant A không thấy tenant B's sessions
- [ ] E2E Playwright spec PASS (bao gồm C→A migration sub-flow)
- [ ] Deploy to VPS + RV pass
