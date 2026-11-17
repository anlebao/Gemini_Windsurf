# CHIẾN LƯỢC THỰC THI ATRS — AN TOÀN & NHANH (v1)

> **Created:** 2026-11-15 · **Mode:** ANALYZE (review plan/cards → đề xuất strategy — chưa implement)
> **Nguồn:** `account-trust-recovery-master-plan.md` + 16 cards `docs/AI/tasks/atrs/` + `BASE_CODE_GAP_ANALYSIS_ATRS.md`
> **Mục tiêu:** rút ngắn time-to-value mà KHÔNG vi phạm governance (Gate 5, Single-Identity, domain purity, RV, Playwright isolation, flag-gated rollout).

---

## 1. CHẨN ĐOÁN: vì sao plan hiện tại chậm nếu làm tuần tự

Plan §4 hiện là chuỗi tuần tự `1→2→3→{4∥6}→5→7→8→9→{10∥11}→12→13→14→15`. Vấn đề:

1. **Chuỗi critical path dài**: ATRS-1 → 2 → 3 → 7 → 8 → 11 → 12 → 14 → 15 ≈ 9-10 waves trước khi user thấy giá trị recovery.
2. **Không có walking skeleton**: mỗi card hoàn thành riêng lẻ → tích hợp muộn, RV muộn, bug kiến trúc phát hiện trễ.
3. **Rủi ro lớn nhất (WebAuthn) để cuối cùng** — đúng ra phải tách khỏi critical path: recovery-code path không cần nó.
4. **ATRS-3 (token revocation) đụng auth dùng chung mọi service** (voice/tenantai clone validators) — nếu để giữa chuỗi sẽ gây regression khó truy. Phải xử lý sớm với test regression riêng.
5. **UI bị chặn hoàn toàn bởi backend** — có thể build contract-first song song.
6. **Chưa có review gate giữa các wave** — user chỉ thấy kết quả cuối.

---

## 2. NGUYÊN TẮC THỰC THI (bất biến)

| # | Nguyên tắc | Lý do |
|---|---|---|
| S1 | **Contract-first**: mọi backend card xuất `1_Shared` contracts + OpenAPI trước; UI/QA build theo contract | Cho phép UI chạy song song; test contract chung |
| S2 | **Walking skeleton trước, dày lên sau**: wave đầu ship 1 vertical slice THẬT (profile → recovery-code → recovery → review → notify) | Tích hợp + RV sớm, phát hiện lỗi thiết kế rẻ |
| S3 | **Một wave = một deploy = một RV** (CD + RV 5-layer theo governance) | Giảm churn CI; mỗi wave tự chứng minh được |
| S4 | **Flag-gated dark launch**: `AtrsEnabled` per app/profile; rollback = tắt flag, không xóa data | An toàn khi rollout; test prod an toàn |
| S5 | **Đụng shared-auth làm sớm + riêng**: ATRS-3 (tokenVersion) thành wave riêng với regression test mọi validator (Gateway, voice, tenantai) | Auth chạm mọi service; sai sót = outage toàn hệ |
| S6 | **Không đụng nhau trên cùng file**: phối lịch với identity initiative (ID-P1/P4 sửa `Customer`/`DemoUser`) | Tránh conflict migration/schema |
| S7 | **Hard-stop tuân thủ**: Gate 5 cho mọi domain change, Single-Identity, domain pure, không đụng AccountingEntry, guard-check mỗi wave | An toàn theo governance, không bàn cãi |
| S8 | **Review gate cuối mỗi wave** (user approve → wave sau) | Phát hiện lệch hướng sớm; vẫn nhanh vì wave ngắn |

---

## 3. CHIẾN LƯỢC WAVE (song song 3 rail)

### Gate 0 — Wave 0: Chốt quyết định (1 lần, docs only)
- **ATRS-1** nhưng chia 2 nhóm:
  - **Chốt NGAY** (chặn mọi thứ): D1 canonical identity, D2 deployment, D3 token revocation, D7 omit U04, D8 security events, D10 scope.
  - **Chốt SAU** (không chặn wave 1): D5 RBAC chi tiết (chỉ cần minimum 2 roles cho wave 1: reviewer/approver), D6 WebAuthn lib (cần trước wave 2), D9 attachment (cần trước dispute UI).
- **Bắt buộc giải trong wave 0**: open question "recovery entry auth model" của ATRS-8 (submit khi mất hết credential = unauthenticated proof: verified-contact + captcha + risk engine) — nó định hình API + risk engine.
- Deliverables: ADR + 2 SRS patched + status mapping table.

### Wave 1 — Recovery walking skeleton (rail A, backend-first)
Cards: **ATRS-2 (subset) + ATRS-4 + ATRS-7 (minimal R0-R2) + ATRS-8 (minimal) + ATRS-3 (riêng phần customer token version)**

Định nghĩa "minimal" rõ ràng để không scope creep:
- ATRS-2 subset: `TrustSubject`/`AccountTrustProfile`/`AccountSecurityEvents`/`ConsentRecords` + `DeviceRegistration` extend cột mới (status, friendly name, registration method) — **bỏ qua** `TrustSubjectLink` phức tạp? KHÔNG — cần vì "profile của tôi" resolve. Giữ nhưng tối giản.
- ATRS-7 minimal: chỉ rules R0 (allow), R1 (new device → step-up), R2 (recovery assisted) + reason codes; R3/R4 config sẵn nhưng rule chưa bật đầy đủ.
- ATRS-8 minimal: flow code-based recovery (còn passkey/credential) + manual-review queue backend đơn giản (list + approve/reject) — chưa cần maker-checker.
- ATRS-3: đúng full (customer token + staff JWT tv) — vì recovery approve phải revoke được token; làm sớm để regression test sớm.

**Kết thúc wave 1**: user thật (test tenant) mất thiết bị → còn recovery code → recover + re-enroll → token cũ chết. RV prod.

### Wave 2 — WebAuthn (rail A tiếp) + UI rail khởi động
- **ATRS-5** (WebAuthn + DeviceCredentials + challenge) — rủi ro lớn nhất, wave riêng, RV trên device thật (Chrome/Android, Safari/iOS).
- **Song song**: UI rail build **ATRS-11 subset** (U06 security center + U07/U08 devices + U05 recovery codes) theo contract wave 1 — KHÔNG chờ backend (contract-first).
- ATRS-3 step-up polish (step-up tokens cho revoke device/regenerate codes).

### Wave 3 — Dispute vertical (rail B)
Cards: **ATRS-9 + ATRS-10 subset**
- ATRS-9: `DisputeCases`/evidence/decisions + maker-checker → `AdjustPointsAsync` + transaction history API (`GetEntriesAsync`).
- ATRS-10 subset: `AtrsPermissions` (2-3 nhóm: recovery reviewer/approver + dispute investigator/approver) + A15/A16/A17/A19 tối thiểu (queue + review + decision) — tái dùng pattern `LoyaltyReconcile.razor`.
- Ledger read API mới (`GetEntriesAsync`) là thay đổi additive trên service stable → đưa vào đây, test regression loyalty full.

### Wave 4 — UI hoàn thiện user (rail C) — ✅ CLOSED 2026-11-17
- **ATRS-11 full** (U01-U03) + **ATRS-12** (U09-U13) — contract từ wave 1-3 đã sẵn.
- U03 passkey UI ghép wave 2 backend; dispute UI ghép wave 3.
- **Kết quả:** code `f7a07347` + fixes `9c9c1678`/`ad5814c8`/`13adb231` deployed; scope mở rộng user-approved (ATRS-6 subset + recovery mine/cancel + D9 R2 attachments); RV prod PASS — sweep 34/34 + Playwright 11/11. Chi tiết `project_state.md` + card status sections.

### Wave 5 — Staff admin full + notifications
- **ATRS-10 full** (A14 dashboard, A18 investigation, A20 audit explorer, A21 policy, A22 roles) + **ATRS-13** (notification templates + metrics/alerts).

### Wave 6 — Hardening + Rollout (ATRS-14 + ATRS-15)
- Security test suite §15.3, E2E §15.4 (12), privacy/retention, backup/restore, load.
- Rollout flags: test tenant → pilot instance → rộng; deliverables 12 items; DoD review.

---

## 4. SONG SONG HÓA (3 rails sau wave 1)

```
Wave 0  ─ Gate 0 decisions
Wave 1  ─ [Rail A] skeleton recovery (2+3+4+7+8 minimal) ───────▶ RV#1
Wave 2  ─ [Rail A] webauthn 5 ──────────────▶ RV#2
          [Rail C] UI 11-subset (contract wave 1) ─▶ merge cùng RV#2
Wave 3  ─ [Rail B] dispute 9 + 10-subset ────▶ RV#3
Wave 4  ─ [Rail C] UI 11-full + 12 ──────────▶ RV#4  ✅ DONE 2026-11-17
Wave 5  ─ [Rail B] 10-full + 13 ─────────────▶ RV#5  ◀ NEXT (session mới)
Wave 6  ─ [All] 14 + 15 ─────────────────────▶ RV#6 + DoD
```
- Rail A/B là backend (có thể 2 agent/session khác nhau, **file disjoint** — governance tránh đụng file).
- Rail C (UI) chỉ phụ thuộc contracts, chạy sau mỗi wave backend 1 nhịp.
- **KHÔNG chạy 2 rail cùng sửa `1_Shared/Domain.cs`** — domain change dồn vào 1 card mỗi wave (2 → 5 → 9), các rail khác chỉ consume contracts.

---

## 5. RÚT GỌN TIME-TO-VALUE

| So với plan cũ | Wave mới |
|---|---|
| Recovery value đầu tiên sau ~5-6 waves tuần tự | **Wave 1** (skeleton code-based recovery) |
| WebAuthn (rủi ro nhất) phụ thuộc chuỗi dài | Wave 2 riêng, không chặn dispute/UI |
| Dispute chờ toàn bộ recovery xong | Wave 3 chạy sau wave 1 (chỉ cần 2+7) |
| UI chờ backend xong | Rail C bắt đầu ngay sau wave 1 contracts |

## 6. CƠ CHẾ AN TOÀN (mỗi wave)

1. **Review gate** (S8): user duyệt summary + RV result trước wave sau.
2. **Guard**: `guard-check.ps1` + `dotnet build VanAn.sln` + tests (Core + affected) + arch tests PASS trước push.
3. **Playwright isolation**: chỉ chạy sau build pass + implementation complete; mỗi wave ≤ spec phạm vi wave (theo `.devin/rules/playwright.rules.md`).
4. **RV 5-layer**: L1 API → L2 assets → L3 Playwright → L4 UI → L5 manual; STOP at first fail; account RV chuyên dụng (KHÔNG bật MFA trên account chuẩn); `DevToken__Secret` inline + remove.
5. **Migration**: additive only; rollback note mỗi migration; không `EnsureCreated` prod.
6. **Feature flag**: `AtrsEnabled` bật theo wave (wave 1 chỉ bật cho test tenant).
7. **Regression ngoài phạm vi**: mỗi wave chạy lại test suite liên quan shared code (auth cho wave 1.5/3, loyalty cho wave 3).

## 7. RỦI RO CHIẾN LƯỢC & GIẢM THIỂU

| Rủi ro | Giảm thiểu |
|---|---|
| Wave 0 decision-paralysis (user chậm chốt D1-D10) | Tách "chốt ngay" vs "chốt sau"; mặc định an toàn đã có; wave 1 chỉ cần 6 decisions |
| Identity initiative đụng `Customer`/`DemoUser` | Coordinated: ATRS-2/5/9 domain change nằm trong lịch đã thỏa thuận với ID-P1/P4; nếu trùng → ATRS wave dời, không merge xung đột |
| Recovery entry unauthenticated (open question) bị bỏ sót | Bắt buộc giải ở wave 0 — nó định hình risk engine + captcha + rate limit |
| `GetEntriesAsync` thêm vào `ILoyaltyPointLedgerService` làm hỏng loyalty | Additive only + full loyalty regression suite (LoyaltyP0/P1 tests) chạy lại |
| WebAuthn thất bại trên nền tảng thật | Wave 2 tách riêng; fallback giữ nguyên recovery-code path (đã ship wave 1) — không block |
| Staff admin scope phình (A14-A22) | Wave 3 chỉ subset cần cho operation; full UI wave 5; U04 đã omit |
| RV accounts/MFA dính nhau | Governance rule sẵn: account chuyên dụng, storageState riêng |

## 8. KHỐI LƯỢNG TƯƠNG ĐỐI (S/M/L — không phải ước lượng thời gian)

| Wave | Nội dung | Size |
|---|---|---|
| 0 | Decisions + SRS patch + ADR | S |
| 1 | Skeleton recovery + revocation + codes + risk-min | **L** (đụng auth) |
| 2 | WebAuthn + step-up polish + UI subset | L |
| 3 | Dispute vertical + ledger read + permissions-subset | L |
| 4 | UI user full (11+12) | L |
| 5 | Staff admin full + notifications | M |
| 6 | Hardening + rollout + DoD | M |

> Không hứa thời gian cụ thể — mỗi wave có review gate riêng để điều chỉnh nhịp.

## 9. KHUYẾN NGHỊ QUYẾT ĐỊNH NGAY (để bắt đầu)

1. User duyệt chiến lược wave này (thay cho tuần tự cũ).
2. Chốt 6 decisions "chốt ngay" (D1, D2, D3, D7, D8, D10) + recovery-entry auth model.
3. Phê duyệt ATRS-2 domain entities (Gate 5) để wave 1 khởi động.
4. Nếu D2 = service riêng `vanan-atrs` → skeleton sln/docker/CI ở đầu wave 1 (pattern `10_Voice`).

---

*Chiến lược được review từ chính plan/cards (không sửa chúng — chỉ đề xuất cách chạy). Mọi thay đổi code vẫn cần wave-gate duyệt.*