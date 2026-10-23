# Task P7 — CreditWallet + Deduct + Check/Deny + Cảnh báo 50/90% + Top-up VietQR

> **Status:** ✅ ĐÃ DUYỆT — decisions chốt (D5=poll, D7=vietqr.io, alert = % còn lại), sẵn sàng implement
> **Phase:** P7 (GĐ-1 — phase cuối) · **Gate:** tests concurrency + e2e top-up + RV
> **Căn cứ:** SRS [G12][G14][G18][G19][G20] · FR-13..16 · O1/O2 chốt · D5 user

---

## 1. Mục tiêu

Ví credit quota cho tenant: admin cấp → usage trừ credit theo định mức → **cảnh báo khi dùng 50%/90%** → **deny khi hết số dư** (feature/chat) → **top-up VietQR thủ công**: user quét QR chuyển khoản → SystemAdmin xác nhận → cộng credit.

## 2. Data model mới (Domain + DbContext)

| Entity | Cột | Ràng buộc |
|---|---|---|
| `CreditWallet` | Id · TenantId (unique) · BalancePurchased · BalancePool · BalanceEarned (GĐ-1 chỉ dùng BalancePurchased) · GrantedTotal · UpdatedAtUtc | update chỉ qua service; balance ≥ 0 enforce tại service + CHECK nếu có thể |
| `CreditLedgerEntry` | Id · TenantId · Type(Grant/Deduct/Adjust — GĐ-1) · Source(Purchased) · Credits(signed) · BalanceAfter · UsageEventId? · TopupRequestId? · Reason · CreatedBy · CreatedAtUtc | **append-only** — điều chỉnh = entry đảo |
| `WalletAlert` | Id · TenantId · Level(Warn50/Warn90/Depleted) · CycleKey · FiredAtUtc · NotifiedChannels | unique(TenantId,Level,CycleKey) — 1 lần/ngưỡng/chu kỳ |
| `TopupConfig` | Id · BankCode · BankName · AccountNumber · AccountName · UpdatedBy · UpdatedAtUtc | singleton (1 row; đa hệ thống → CallerSystem sau) |
| `TopupRequest` | Id · TenantId · ReferenceCode (unique) · AmountVnd? · CreditsGranted? · Status(Pending/Confirmed/Rejected/Expired) · ConfirmedBy? · ConfirmedAtUtc · Note? · CreatedAtUtc | unique(CallerId,IdempotencyKey) |

`UsageEvent` += `CreditsApplied?` (decimal?) + `UnpaidCredits?` — ghi số credit đã trừ / phần thiếu khi ví không đủ (vẫn ghi usage fact, không chặn ingest).

## 3. Quyết định thiết kế

| # | Điểm | Đề xuất |
|---|---|---|
| D1 | Deduct timing | **Tại ingest** (deduct-at-ingest): sau khi ghi UsageEvent thành công → resolve rate tại `occurredAtUtc` → `credits = qty × rate` → ledger Deduct + update wallet **cùng 1 transaction**. Không trừ quá số dư: trừ `min(credits, balance)` + `UnpaidCredits = phần thiếu` — usage vẫn được ghi (đúng MeterOnly cho store.visit; Deny do `check` API quyết, không do ingest) |
| D2 | Atomicity | `UsageEvent insert + LedgerEntry + Wallet update + WalletAlert` trong **1 EF transaction**. Race 2 deduct đồng thời → EF concurrency token (`Wallet.UpdatedAtUtc` rowversion-style) hoặc `UPDATE ... SET balance = balance - @x WHERE balance >= @x` pattern trên SQLite — chọn **conditional UPDATE + retry 1 lần** (đơn giản, không cần migration phức tạp) |
| D3 | Check API | `GET /v1/wallets/{tenantId}/check?serviceCode=` → `{balance, required, deny, alertLevel}`. `deny = (Enforcement==Deny AND balance < rate×1unit)` — với event nhiều unit, caller truyền `estimate?` optional. Wallet chưa tồn tại → tạo lazy balance 0 → deny nếu Deny-service. **Caller cache 30s** [G20] |
| D4 | Alert thresholds — **CHỐT: % CÒN LẠI** | Config `Metering:AlertThresholds` default `0.5,0.1` — ngưỡng theo **balance còn lại / GrantedTotal**: còn ≤50% → `Warn50` (vàng), còn ≤10% → `Warn90` (đỏ), =0 → `Depleted`. Vd cấp 1000: còn 500 → warn50, còn 100 → warn90. CycleKey reset khi Grant mới (top-up) → alert lại được cho chu kỳ mới |
| D5 | Alert delivery — **CHỐT: poll** | `WalletAlert` insert + trả `alertLevel` trong mọi response wallet/check. ShopERP đọc check/balance khi user mở trang → hiển thị banner. NATS push defer P9/P10 |
| D6 | Topup flow | `POST /v1/topups {tenantId, amountVnd?}` → TopupRequest + `referenceCode = "TOPUP-" + 8 ký tự (đủ unique, nhập tay được)` + **QR payload** → user CK kèm mã → `POST /v1/topups/{id}/confirm {credits, note?}` (admin, idempotent qua status + unique key) → ledger Grant + `GrantedTotal +=` + reset alert cycle → `POST /v1/topups/{id}/reject {reason}` |
| D7 | QR generation — **CHỐT: A vietqr.io** | `img.vietqr.io/image/{bank}-{acc}-{template}.png?amount=&addInfo={ref}` — pattern `VietQrService`/`BankConfig` đang production (đã verify `VietQrController`), payload là URL string sinh local, ảnh render phía client |
| D8 | VND→credit | **Admin quyết khi confirm** — request chỉ ghi `AmountVnd?`; confirm nhận `credits` do admin nhập (có thể hiển thị gợi ý từ config `Metering:VndPerCredit` optional — KHÔNG cố định quan hệ 1 credit = X VND, giữ nguyên tắc policy) |
| D9 | Topup expiry | Request Pending hết hạn sau `Metering:TopupTtlHours` (default 72h) — background sweep hoặc lazy-expire khi query (chọn **lazy**: status check khi đọc — không cần job GĐ-1) |
| D10 | Grant thủ công | `POST /v1/wallets/{tenantId}/grants {credits, reason}` admin — dùng cùng ledger Grant (TopupRequestId null) — cấp hạn mức ban đầu/điều chỉnh |
| D11 | Enforcement mặc định | Rate card chưa khai Enforcement → **MeterOnly** (an toàn — không ai bị chặn ngoài ý muốn). Deny chỉ khi admin chủ động set |

## 4. API endpoints thêm (SRS §6.1 — đã spec)

`GET /v1/wallets/{t}` · `GET /v1/wallets/{t}/check?serviceCode=&estimate=` · `POST /v1/wallets/{t}/grants` · `GET /v1/wallets/{t}/ledger?page=` · `GET/PUT /v1/topup-config` (admin) · `POST /v1/topups` · `POST /v1/topups/{id}/confirm|reject` · `GET /v1/topups?status=&tenantId=` — mutations đều Idempotency-Key + admin (D5-A task P2).

## 5. Edge cases đã liệt kê hết

**Deduct:** (a) ví chưa tồn tại + event Deny-service → lazy-create balance 0, deduct → unpaid=full · (b) balance < credits → trừ phần có, ghi unpaid, fire Depleted alert · (c) unpriced event → không deduct, `CreditsApplied=null` · (d) replay idempotent → KHÔNG deduct lần 2 (deduct gắn insert, replay trả cũ) · (e) rate đổi giữa chừng → event mới áp rate mới, ledger cũ giữ nguyên (append-only) · (f) 2 ingest đồng thời vượt số dư → conditional update chỉ 1 thắng phần còn lại, kia unpaid

**Check/deny:** (a) serviceCode MeterOnly → deny luôn false · (b) wallet không tồn tại → balance=0 · (c) metering chậm → caller timeout 3s + cache stale-while-revalidate → quyết fail-open/closed theo fail-list (caller side, không phải metering) · (d) estimate lớn hơn balance → deny=true kèm required

**Alert (% còn lại):** (a) `GrantedTotal=0` → tránh chia 0 → remaining%=0 → Depleted · (b) top-up → cycle mới, alert cũ không lặp nhưng alert mới fire được · (c) grant nhỏ → % còn lại tính trên `GrantedTotal` mới (grant 100 vào ví đã dùng 900/1000 → còn 200/1100 ≈ 18% → warn90 ngay cycle mới — đúng hành vi, KHÔNG reset mù) · (d) deduct không chạm ngưỡng mới → không alert · (e) alert kênh: GĐ-1 chỉ `alertLevel` trong response (poll)

**Topup:** (a) 2 request cùng tenant → 2 referenceCode khác nhau (không merge) · (b) confirm 2 lần → idempotent (status đã Confirmed → trả lại kết quả, không cộng 2) · (c) confirm request đã Rejected/Expired → 409 · (d) credits confirm ≤0 → 400 · (e) referenceCode trùng DB (cực hiếm) → regenerate 1 lần · (f) admin confirm sai số → **Adjust entry đảo** (không sửa entry gốc) — flow: reject + tạo adjust — ghi hướng dẫn vận hành · (g) tenantId không tồn tại trong hệ Vạn An → metering không validate (opaque caller claim [G3])

## 6. Test matrix — T01–T30 (EF InMemory cho logic; SQLite in-memory cho unique/race)

| Nhóm | Tests |
|---|---|
| Wallet/deduct | T01 grant → balance+ledger · T02 deduct đúng rate · T03 deduct vượt số dư → unpaid flag · T04 replay ingest không double-deduct · T05 unpriced → không deduct · T06 lazy wallet · T07 race deduct (SQLite mem) |
| Check | T08 MeterOnly → deny=false · T09 Deny + đủ → false · T10 Deny + thiếu → true + required · T11 estimate param · T12 wallet mới → balance 0 |
| Alert | T13 warn50 fire khi balance ≤50% GrantedTotal · T14 warn90 khi ≤10% · T15 mỗi ngưỡng 1 lần/cycle · T16 top-up → cycle reset → alert lại được · T17 GrantedTotal=0 → Depleted không lỗi chia 0 · T17b grant nhỏ vào ví cạn → warn90 đúng % còn lại |
| Topup | T18 create → referenceCode unique + QR payload đúng format vietqr.io (bank/acc/amount/addInfo) · T19 confirm → Grant + credits + cycle reset · T20 confirm replay idempotent · T21 confirm rejected/expired → 409 · T22 reject + reason · T23 lazy-expire sau TTL · T24 credits≤0 → 400 · T25 concurrent confirm |
| Admin/config | T26 topup-config GET/PUT + audit · T27 non-admin mutation → 403 |
| Ledger | T28 append-only (không update API) · T29 adjust đảo đúng dấu · T30 ledger paging + balance = Σ entries |

## 7. ShopERP/KhachLink UI (đi kèm P7)

| Màn | Chi tiết |
|---|---|
| ShopERP banner cảnh báo | Layout check `check` response → banner vàng warn50 / đỏ warn90/depleted + link top-up (poll khi mở trang, cache 30s) |
| ShopERP `/wallet/phi-nen-tang` | Số dư + hạn mức + lịch sử ledger + nút **Nạp phí nền tảng** → modal QR (img từ qrPayload URL — pattern `_bookingEntry.QrCodePngBase64` render sẵn có) + referenceCode hiển thị copy |
| Deny UX | Khi deny → toast/modal "Hết hạn mức phí nền tảng" + nút nạp — feature pages + chat input disable theo `check` (P4 hooks gọi check trước action Deny-service) |
| Admin `/admin/metering/topups` | List Pending → confirm (nhập credits) / reject · `topup-config` form (bank/acc/name) |
| E2E Gate 4 | spec `metering-wallet.spec.ts` + `metering-topup.spec.ts` |

## 8. File plan

| File | Action |
|---|---|
| Domain: `CreditWallet` `CreditLedgerEntry` `WalletAlert` `TopupConfig` `TopupRequest` | BUILD |
| `UsageEvent` += `CreditsApplied`/`UnpaidCredits` | MODIFY |
| Contracts += wallet/topup DTOs | MODIFY |
| `Infra/WalletService.cs` `TopupService.cs` `VietQrPayloadBuilder.cs` | BUILD |
| `MeteringDbContext` += 5 DbSet + config | MODIFY |
| `Infra/UsageService.cs` += deduct-in-transaction | MODIFY (P3) |
| `Api/Program.cs` += endpoints | MODIFY |
| ShopERP: banner + wallet page + deny UX + admin topups + client methods | BUILD/MODIFY |
| tests += T01–T30 | BUILD |

**Out of scope:** hold/capture (P8) · đa nguồn pool/earned (P9+) · NATS balance mirror (P10) · thanh toán tự động/đối soát ngân hàng (P17) · CallerKey CRUD UI.

## 9. Quyết định — ĐÃ CHỐT (user duyệt)

- **D7:** QR = vietqr.io URL (A — pattern `VietQrService`/`BankConfig` đang production)
- **D5:** alert GĐ-1 = poll qua `check`/`wallet` response — NATS defer P9/P10
- **D4:** alert theo **% còn lại** của `GrantedTotal` — còn ≤50% warn, ≤10% critical (SRS [G18] đã ghi "còn 50%/10%" — nhất quán)
