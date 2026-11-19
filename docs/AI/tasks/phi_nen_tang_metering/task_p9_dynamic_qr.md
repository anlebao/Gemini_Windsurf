# Task P9 — Nạp ví bằng QR động Tingee (quét QR → CK thẳng → auto cộng credit)

> **Status:** 🔵 IN PROGRESS — task card + TDD
> **Phase:** P9 · **Gate:** unit tests · CD deploy · RV (tạo QR + CK thật nhỏ)
> **Căn cứ:** user directive 2026-11-19 — "không redirect qua form payment của tingee, chỉ scan vietQR, chuyển khoản luôn trên app banking mà hệ thống nhận được số dư và tự động cộng ví credit". User chốt: VA `VQRQANSQG9134` (Tingee cấp), MB BIN `970422`; prod probe `generate-dynamic-qr` đã trả `code 00` + `billId=QRD4EC75764587`.

---

## 1. Mục tiêu

Khách quét 1 mã VietQR động duy nhất cho mỗi top-up → chuyển khoản trực tiếp trong app ngân hàng (không qua checkout Tingee) → Tingee đối soát theo `billId` → bắn Payment Callback webhook → metering auto-confirm + cộng credit theo **số tiền thực nhận** (partial payment OK). Flow checkout-link P8 giữ nguyên làm phương án phụ.

## 2. Quyết định thiết kế — ĐÃ CHỐT

| # | Điểm | Quyết định |
|---|---|---|
| D1 | API tạo QR | `POST {BaseUrl}/v1/generate-dynamic-qr` — body `{vaAccountNumber, qrCodeType:"dynamic-one-time-payment", bankBin, amount, purpose, extraInfo}`; cùng HMAC-SHA512 `timestamp:body` + canonical-decimal như P8. |
| D2 | Đối soát | Persist `TopupRequest.QrBillId` (= `billId` Tingee trả về) → webhook Payment Callback match theo `additionalData[].billId`; fallback regex `TOPUP-XXXXXXXX` trong `content` (user có thể sửa nội dung CK). |
| D3 | QR lifecycle | Endpoint `POST /v1/topups/{id}/qr` mint MỚI mỗi lần gọi (one-time QR), overwrite `QrBillId` cũ — chỉ billId mới nhất còn hiệu lực. Topup vẫn Pending cho tới khi webhook về. |
| D4 | Amount | `amount` = `AmountVnd` khai báo; credits tính theo `amount` THỰC NHẬN trong callback (`paidAmount = evt.amount`), chia `VndPerCredit` — reuse `ConfirmPaidByReferenceAsync` core. |
| D5 | Webhook phân loại | Cùng endpoint `/v1/webhooks/tingee`. Payment Callback (QR/VA) nhận diện bằng `transactionCode`/`vaAccountNumber`/`additionalData` (không có `orderId`/`status`); chỉ xử lý `type=="credit"`, debit → 200 ignored. |
| D6 | Config | `Tingee:VaAccountNumber` + `Tingee:VaBankBin` (env `TINGEE_VA_ACCOUNT_NUMBER`/`TINGEE_VA_BANK_BIN`); thiếu → `/qr` trả `qr_disabled`, hệ thống vẫn chạy flow P8/P7. |
| D7 | UI | ShopERP wallet: nút "QR tự động" gọi `/qr` → render payload EMV bằng QRCoder (đã có package) → base64 PNG; QR động **thay** QR VietQR tĩnh khi có; nút "Thanh toán online" (P8) giữ làm fallback. |
| D8 | Secrets | VA + BIN là giá trị cấu hình thường (không phải secret) — vẫn đi qua env/compose như `TINGEE_*` khác. |

## 3. Changes

| File | Action |
|---|---|
| `Domain/TopupRequest.cs` | MODIFY — += `QrBillId` (string?) + `AttachQrBill(billId)` |
| `Infra/MeteringDbContext.cs` | MODIFY — map `QrBillId` maxlength 128 |
| `Infra/TingeeOptions.cs` | MODIFY — += `VaAccountNumber`, `VaBankBin` + bind |
| `Infra/TingeePaymentClient.cs` | MODIFY — += `GenerateDynamicQrAsync` → `TingeeQrResult(QrPayload, QrAccount, BillId)`; reuse signing/canonical-decimal; `IsQrEnabled` |
| `Infra/TopupService.cs` | MODIFY — += `CreateQrAsync`, `ConfirmPaidByQrBillIdAsync`; refactor shared `ConfirmPaidCoreAsync` (lookup by QrBillId hoặc ReferenceCode → cùng grant path) |
| `Infra/TingeeWebhookService.cs` | MODIFY — detect Payment Callback payload; parse `transactionCode`/`amount`/`content`/`type`/`additionalData[].billId`; dispatch billId → `ConfirmPaidByQrBillIdAsync`, fallback `TOPUP-XXX` trong `content` → `ConfirmPaidByReferenceAsync` |
| `Contracts/WalletContracts.cs` | MODIFY — += `TopupQrResponse(QrPayload, BillId, QrAccount, ExpiresInMinute)` |
| `Api/Program.cs` | MODIFY — `POST /v1/topups/{id}/qr`; EnsureP7Schema += ALTER `QrBillId` + cột trong legacy CREATE TABLE |
| `tests/TingeeTests.cs` | MODIFY — += QR-gen + QR-webhook matrix (TDD) |
| ShopERP `Services/MeteringDtos.cs` | MODIFY — += `MeteringTopupQrDto` |
| ShopERP `Services/MeteringApiClient.cs` | MODIFY — += `CreateTopupQrAsync` |
| ShopERP `Components/Pages/Wallet/PhiNenTang.razor` | MODIFY — nút QR tự động + QRCoder render base64 PNG |
| `docker-compose.metering.yml` + `cd-multivps.yml` | MODIFY — env `Tingee__VaAccountNumber`/`VaBankBin` ← `TINGEE_VA_*` |

## 4. Test matrix (TDD — viết trước)

| Test | Kỳ vọng |
|---|---|
| `Qr_Success_ReturnsPayload_StoresBillId` | stub `generate-dynamic-qr` → `code 00` → payload+billId persist lên TopupRequest |
| `Qr_Request_Signed_CanonicalAmount` | x-signature verify trên exact body; `"amount":50000` không `.0`; body chứa vaAccountNumber/bankBin |
| `Qr_TopupNotPending/NotFound/NoAmount/Disabled/NoVa` | `topup_not_pending` / `not_found` / `amount_required` / `tingee_disabled` / `qr_disabled` |
| `Webhook_QrPayment_ByBillId_Confirms` | callback `type=credit`, `additionalData[].billId` khớp → Confirmed, credits = amount/VndPerCredit |
| `Webhook_QrPayment_Partial_UsesActualAmount` | callback amount < declared → credits theo amount thực |
| `Webhook_QrPayment_ByContentRef_Fallback` | không có billId, `content` chứa `TOPUP-XXX` → confirm qua reference |
| `Webhook_QrPayment_Debit_Ignored` | `type=debit` → 200 ignored, giữ Pending |
| `Webhook_QrPayment_Replay_NoDoubleGrant` | 2 lần callback → đúng 1 ledger Grant |
| `Webhook_QrPayment_UnknownBillId_404` | billId lạ + content không match → 404 |
| `Webhook_QrPayment_BadSignature_401` | signature sai → 401 (đã cover chung nhưng thêm cho payload mới) |

## 5. Ops sau deploy

1. GH secrets/vars: `TINGEE_VA_ACCOUNT_NUMBER=VQRQANSQG9134`, `TINGEE_VA_BANK_BIN=970422` → compose env → re-run deploy-metering.
2. RV: tạo topup có amount → `POST /v1/topups/{id}/qr` → verify payload EMV + billId → (user) CK thật nhỏ vào VA → webhook → ví tăng.
3. Check portal Tingee: Payment Callback event đã bật cho endpoint webhook (nếu QR webhook không về → xem "Hoạt động").

## 6. Rủi ro / mở

- Shape chính xác của `additionalData` trong Payment Callback chưa xác nhận 100% (có thể array object/string) → parser phải defensive: thử `billId` property nhiều dạng + fallback `TOPUP-XXX` trong `content`.
- QR `dynamic-one-time-payment` có thể hết hạn trước TTL topup 72h → UI hiển thị note "QR có hiệu lực giới hạn, tạo lại nếu cần"; mỗi lần gọi `/qr` mint billId mới.
- `qrAccount`/`bank` fields trong response — parse optional, không bắt buộc.
- Settlement VA → TK MB thật: check phí/lag trên portal (không block code).
