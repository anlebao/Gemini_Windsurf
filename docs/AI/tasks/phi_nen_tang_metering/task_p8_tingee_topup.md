# Task P8 — Top-up ví tự động qua Tingee Payment Gateway (sandbox → prod)

> **Status:** 🔵 IMPLEMENTED — code + tests done (110/110 metering tests PASS 2026-11-18), chờ deploy + cấu hình Tingee portal + RV sandbox E2E
> **Phase:** P8 · **Gate:** unit tests ✅ · CD deploy · Tingee sandbox E2E
> **Căn cứ:** user directive 2026-11-18 — tích hợp `developers.tingee.vn` cho luồng top-up ví trước (order/booking = phase sau). User chốt: partial payment confirm theo `paidAmount` thực trả; tạo task card trước khi code.

---

## 1. Mục tiêu

Thay quy trình top-up ví thủ công (Pending → admin confirm) bằng link thanh toán Tingee: tenant bấm "Thanh toán online" → checkout URL → webhook IPN có chữ ký → auto-confirm + grant credits theo tỷ giá cấu hình.

## 2. Quyết định thiết kế — ĐÃ CHỐT

| # | Điểm | Quyết định |
|---|---|---|
| D1 | Phạm vi | CHỈ top-up ví metering (P8). Order/booking payment = phase sau (order cần endpoint mới verify chữ ký — KHÔNG tái dùng `/api/webhooks/payment` unauthenticated hiện có). |
| D2 | API | `POST {BaseUrl}/v1/payment-gateway/create-link`; UAT `https://uat-open-api.tingee.vn`, PROD `https://open-api.tingee.vn`. orderId = `TopupRequest.ReferenceCode` (`TOPUP-XXXXXXXX`). |
| D3 | Link expiry | Tingee `expireInMinute` ∈ [5,30] ≪ Topup TTL 72h → KHÔNG lưu link. `POST /v1/topups/{id}/pay` mint link MỚI mỗi lần gọi. |
| D4 | VND→credit | `TopupConfig.VndPerCredit` (decimal, **default 0 = TẮT auto-confirm — opt-in**). credits = paidAmountVnd / VndPerCredit. Admin set qua `PUT /v1/topup-config` + UI `/admin/metering/topups`. |
| D5 | Partial payment | User chốt: confirm theo `paidAmount` THỰC TRẢ (kể cả < amount). |
| D6 | Webhook non-success | `expired`/`canceled` → 200 ignored; TopupRequest giữ Pending (tạo link mới được; lazy-expire 72h dọn). |
| D7 | VndPerCredit = 0 | Webhook vẫn `AttachProviderPayment` (PaidAmountVnd/ProviderTransactionId) + giữ Pending → admin confirm tay. Transition an toàn. |
| D8 | Auth webhook | `/v1/webhooks/*` EXEMPT caller-auth middleware (exempt hẹp, chỉ prefix này); auth = `x-signature` HMAC-SHA512(`x-request-timestamp:rawBody`, ClientSecret). `webhook-type: url-validation` → `{plainToken, encryptedToken=HMAC-SHA256(plainToken, WebhookSecret ?? ClientSecret)}`. |
| D9 | Secrets | Env `Tingee__ClientId/ClientSecret/BaseUrl/WebhookSecret/ReturnUrl` — OPTIONAL, thiếu → tự tắt. GH secrets `TINGEE_*` → `.env.metering` qua deploy-metering job; KHÔNG thêm vào required-secrets check. |
| D10 | Tiền về | 1 merchant account Tingee của VanAn (sandbox trước). Per-tenant settlement = Tingee Master Merchant — phase sau. |

## 3. Changes

| File | Action |
|---|---|
| `8_Metering/.../Domain/TopupConfig.cs` | MODIFY — += `VndPerCredit` (Create/Update optional param, default 0) |
| `8_Metering/.../Domain/TopupRequest.cs` | MODIFY — += `PaidAmountVnd`/`PaymentProvider`/`ProviderTransactionId` + `AttachProviderPayment()` |
| `8_Metering/.../Infra/MeteringDbContext.cs` | MODIFY — map cột mới (precision 18,4 / maxlength) |
| `8_Metering/.../Contracts/WalletContracts.cs` | MODIFY — `TopupConfigDto`/`PutRequest` += `VndPerCredit` (PUT nullable = giữ cũ); += `TopupPayResponse` |
| `8_Metering/.../Infra/TingeeOptions.cs` | BUILD — options + `From(IConfiguration)` manual bind (không thêm Binder package) |
| `8_Metering/.../Infra/TingeeSignature.cs` | BUILD — HMAC-SHA512 sign/verify (fixed-time) + HMAC-SHA256 encryptedToken + timestamp UTC+7 |
| `8_Metering/.../Infra/TingeePaymentClient.cs` | BUILD — typed client create-link; serialize body 1 lần → sign exact string; fail-safe trả Result |
| `8_Metering/.../Infra/TingeeWebhookService.cs` | BUILD — url-validation + verify signature + dispatch `ConfirmPaidByReferenceAsync` |
| `8_Metering/.../Infra/TopupService.cs` | MODIFY — ctor += `TingeePaymentClient?`; refactor `ConfirmAsync`→`ConfirmCoreAsync`; += `CreatePayLinkAsync`, `ConfirmPaidByReferenceAsync`; `PutConfigAsync`/`ToDto` += VndPerCredit |
| `8_Metering/.../Api/Program.cs` | MODIFY — DI register client/service · middleware exempt `/v1/webhooks` · endpoints `POST /v1/topups/{id}/pay` + `POST /v1/webhooks/tingee` · EnsureP7Schema += idempotent ALTERs (+ cột trong legacy CREATE TABLE) |
| `8_Metering/.../tests/TingeeTests.cs` | BUILD — 15 tests (signature/webhook/pay-link) |
| `8_Metering/.../tests.csproj` | MODIFY — += `Microsoft.Extensions.Configuration` (CPM 9.0.3) |
| ShopERP `Services/MeteringDtos.cs` | MODIFY — += `MeteringTopupPayDto`, `MeteringTopupConfigDto.VndPerCredit` |
| ShopERP `Services/MeteringApiClient.cs` | MODIFY — += `CreateTopupPayLinkAsync` |
| ShopERP `Services/MeteringRateCardApiClient.cs` | MODIFY — `PutTopupConfigAsync` += `vndPerCredit` |
| ShopERP `Components/Pages/Wallet/PhiNenTang.razor` | MODIFY — nút "Thanh toán online ngay" → `/pay` → `window.open(checkoutUrl)` |
| ShopERP `Components/Pages/Admin/MeteringTopups.razor` | MODIFY — config form += VndPerCredit |
| `docker-compose.metering.yml` | MODIFY — env `Tingee__*` |
| `.github/workflows/cd-multivps.yml` | MODIFY — deploy-metering `envs` + `.env.metering` += `TINGEE_*` (optional) |

## 4. Test matrix (TDD — test viết trước, 15 tests mới)

| Test | Kỳ vọng |
|---|---|
| Signature_Roundtrip_And_Tamper | verify đúng body+secret; sai body/secret/null → false |
| EncryptedToken_IsHmacSha256Hex | 64 hex chars, deterministic |
| Webhook_UrlValidation_ReturnsEncryptedToken | 200 + encryptedToken = HMAC-SHA256(plainToken) |
| Webhook_BadSignature_401 | signature sai → 401 |
| Webhook_Success_ConfirmsPaidAmount_CreditsByRate | paid 50k / rate 1000 → 50 credits, ConfirmedBy=`tingee:webhook` |
| Webhook_PartialPayment_GrantsPaidAmountOnly | paid 30k → 30 credits (D5) |
| Webhook_Replay_NoDoubleGrant | 2 lần webhook → đúng 1 ledger Grant (bắt được bug thiếu guard Confirmed — đã fix) |
| Webhook_ExpiredStatus_Ignored_StaysPending | 200 ignored, giữ Pending |
| Webhook_VndPerCreditZero_RecordsPaid_StaysPending | ghi paid, giữ Pending (D7) |
| Webhook_UnknownReference_404 | refcode lạ → 404 |
| PayLink_Success_ReturnsCheckoutUrl_AndSignsRequest | stub handler → checkoutUrl + verify x-signature trên exact body |
| PayLink_TopupWithoutAmount_Fails | `amount_required` |
| PayLink_TopupNotPending_Fails | `topup_not_pending` |
| PayLink_TingeeDisabled_Fails | `tingee_disabled` |
| PayLink_TopupNotFound_Fails | `not_found` |

**Kết quả:** `dotnet test VanAn.Metering.sln` → **110/110 PASS** (2026-11-18).

## 5. Ops sau deploy (user)

1. GH secrets: `TINGEE_CLIENT_ID`, `TINGEE_CLIENT_SECRET`, `TINGEE_BASE_URL` (uat), `TINGEE_WEBHOOK_SECRET`, `TINGEE_RETURN_URL` (`https://app2.khachvip.online/wallet/phi-nen-tang`).
2. Tingee Merchant Portal: đăng ký webhook `https://app2.khachvip.online/admin/metering/api/v1/webhooks/tingee` (credentials quyền "Xác thực webhook") → URL-validation challenge tự pass.
3. Admin `/admin/metering/topups`: set **VndPerCredit** (VD 1000 = 1 credit/1000đ).
4. E2E sandbox: tạo topup → "Thanh toán online" → thanh toán `sandbox.tingee.vn` → ví tăng credits.
5. ⚠️ Secret đã paste trong chat → rotate sau khi test xong; KHÔNG dùng lại cho production.

## 6. Rủi ro / mở

- Webhook `VerifySecret = WebhookSecret ?? ClientSecret` — nếu Tingee ký webhook bằng ClientSecret API credentials thì chỉ cần `TINGEE_CLIENT_SECRET`; `TINGEE_WEBHOOK_SECRET` chỉ cần cho url-validation. Xác nhận khi cấu hình portal.
- `paidAmount` field name trong webhook payload theo docs; nếu Tingee dùng tên khác (`paid_amount`...) → kiểm tra payload thật lần webhook đầu, chỉnh `TingeePaymentEvent`.
- Per-tenant settlement (Master Merchant) + order/booking payment = phase sau.
