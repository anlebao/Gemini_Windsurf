# MASTER PLAN: VẠN AN ZERO-COST IDENTITY — ĐỊNH DANH THEO CẤP ĐỘ TIN CẬY (L0–L3)

> **Created:** 2026-11-10
> **Status:** ✅ APPROVED 2026-11-10 · ✅ **ID-P0 DONE** (`identity-inventory-p0.md`) · ✅ **Q1 CHỐT: 2 hệ song song** (customer ≠ owner; collaborator ở customer hệ) · ✅ **REVIEW 2026-11-10: 4/4 đề xuất ĐƯỢC DUYỆT** (① ID-P1 += `VerifiedChannel` + downgrade hook khi tenant deactivate · ② P4/ID-P3 chốt email-first Brevo + composite fallback + slot ZNS + hardening `OtpService` cũ · ③ Q2/Q3 chốt trước ID-P2, Zalo Login defer có mốc re-evaluate · ④ **Phase MFA-1 Admin TOTP** — ưu tiên trước ID-P2) — chi tiết §8 · ID-P1 card + P4 card + card MFA-1 đã cập nhật
> **Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
> **Liên quan:** `self-serve-onboarding-master-plan.md` (P4 OTP card được định vị lại thành L2 channel) · `task_p4_otp_phone_verification.md` · archived `tiered_auth_loyalty_master_plan.md` (tiền nhiệm — cần đối chiếu, không sao chép mù)
> **Domain modification:** **CÓ — Gate 5.** Thêm `LinkedIdentities`/`ContactVerification`/`TrustLevel` vào User aggregate = Domain modeling change → PHẢI user duyệt trước IMPLEMENT.

---

## 1. MỤC TIÊU & NGUYÊN TẮC

**Thay đổi tư duy:** không dùng "xác minh người dùng bằng OTP" làm nền tảng. OTP chỉ chứng minh quyền truy cập một kênh liên lạc tại một thời điểm — KHÔNG chứng minh danh tính pháp lý hay chủ thể kinh doanh.

Thay vào đó: **định danh theo cấp độ tin cậy** — mỗi hành động yêu cầu mức trust tối thiểu; người dùng chỉ verify đến mức cần cho việc họ làm. Kênh verify ưu tiên **0đ** (OAuth, magic link, anonymous ID); OTP trả phí chỉ là MỘT kênh ở L2.

### 4 cấp độ

| Level | Tên | Chi phí verify | Nội dung |
|---|---|---|---|
| **L0** | Guest / Anonymous | 0đ | Device/Anonymous ID tự động; xem cửa hàng, tìm kiếm, giỏ hàng; không SĐT/email/OTP |
| **L1** | Social / Account | 0đ phí xác minh | Google OAuth · Zalo Login · Email magic link · Passkey (phase sau). Xác nhận quyền kiểm soát tài khoản bên thứ 3 — không phải danh tính pháp lý |
| **L2** | Contact Verified | theo kênh (email 0đ · ZNS 300đ · SMS ~600đ) | Xác minh SĐT/email thật khi cần; chống multi-account + account takeover |
| **L3** | Business / Legal Verified | thủ công/đối tác | MST/GPKD, người đại diện, tư cách thành viên HTX — qua pipeline sysadmin verify có sẵn |

### Nguyên tắc kỹ thuật (bất biến)

1. `UserId` UUID bất biến là khóa nội bộ duy nhất — KHÔNG dùng SĐT/email làm PK.
2. Một UserId liên kết nhiều phương thức: Google, Zalo, email, SĐT, passkey.
3. Không auto-merge tài khoản chỉ vì trùng email/SĐT **chưa verify** — merge chỉ qua flow có consent.
4. Trust Level **độc lập Authorization** — quyền truy cập (role/permission) khác mức tin cậy; action nhạy cảm check cả hai.
5. Nâng cấp L-level KHÔNG mất lịch sử: đơn hàng, điểm loyalty, ví metering gắn UserId bất biến.
6. OTP/session token: single-use, bind `purpose` + `contact` + expiry, chống replay; state store phải chuyển khỏi IMemoryCache khi multi-gateway (ghi nợ Redis/PG).

### 1A. TẦM NHÌN DÀI HẠN: VẠN AN ID — IdP cho hệ sinh thái (2026-11-10, user directive)

**Suy nghĩ ngược:** Vạn An không nên mãi là *khách hàng* của dịch vụ xác thực (eSMS/Brevo/Zalo) — hướng dài hạn là trở thành **Identity Provider**: các app/hệ thống khác dùng "Đăng nhập bằng Vạn An" hoặc gọi verify API. Moat không nằm ở kênh OTP (ai cũng thuê được SMS/ZNS) mà ở **trust registry**: mạng merchant VN đã verify MST/GPKD + sysadmin duyệt tay (L3) — thứ Google/Zalo không có cho merchant nhỏ.

**Phần phụ thuộc không tránh được:** last-mile delivery (SMS→nhà mạng, ZNS→Zalo, email→MTA) là vật lý mạng — Vạn An không tự "gửi" được tới thiết bị người khác. `IIdentityOtpChannel` đúng hướng: transport = commodity pluggable, registry phía trên không đổi.

**API-first principles — thiết kế ngay từ ID-P1 (không xây public API sớm):**

| Nguyên tắc | Hệ quả |
|---|---|
| Identity core = service nội bộ sạch | `IIdentityService`/trust registry không gắn Blazor/ShopERP — bọc thành public API được sau này |
| Assertion-based, không share DB | Partner nhận signed JWT/assertion verify chữ ký — Vạn An giữ raw phone/email mã hóa, chỉ vends claims tối thiểu (privacy + NĐ 13/2023) |
| Trust level kèm `purpose`/`audience` | Assertion phát hành cho app A không mặc nhiên dùng được cho app B |
| Metering sẵn | `vanan-metering` đã chạy — mỗi verify/assertion call là usage event tự nhiên → thu phí theo volume |
| OTP transport pluggable | eSMS/ZNS/email/voice/missed-call — kênh nào rẻ/khả dụng thì dùng |

**Lộ trình IdP (defer — chỉ khi có demand thật):**
1. Hiện tại: internal L0-L3 vững, chưa public.
2. Khi 2-3 use-case thật: **read-only verify API** `POST /v1/identity/assert` → signed JWT (trustLevel + claims, không raw PII).
3. Khi ecosystem đủ lớn: OIDC provider "Đăng nhập bằng Vạn An" + partner dashboard + pricing qua metering.

**Rủi ro IdP (ghi nhận sớm):** gánh trust lớn — key management, uptime SLA, assertion abuse, compliance NĐ 13/2023 + NĐ 91/2020. Không xây trước demand; nhưng KHÔNG thiết kế registry theo cách chặn đường expose sau này.

**Self-owned verification dài hạn (spike sau, khi volume lớn):** missed-call/flash-call qua SIP/telephony gateway hoặc GSM gateway — chi phí/cuộc thấp hơn SMS, vẫn là thuê đường truyền nhưng commodity hơn eSMS brandname.

---

## 2. HIỆN TRẠNG — ID-P0 AUDIT DONE (2026-11-10)

**Audit đầy đủ:** `docs/AI/plans/identity-inventory-p0.md`. Tóm tắt:

- **Customer side ĐÃ CÓ tiered identity:** `Customer.IdentityLevel` enum (`Guest=0, Social=1, Verified=2, Full=3, DeviceVerified=4`) + `DeviceId` anonymous tracking + `CustomerMergeService` merge guest stubs → account (đúng nguyên tắc L0→L1, nhưng per-tenant SQLite).
- **Owner/staff side (`DemoUser`) nghèo:** Username/bcrypt/Email?/Role/IsActive — không phone, không external identity, không trust level. JWT claims: `sub, email, tenant_id, role` — không trust claim.
- **OTP infra trùng lặp 3 nơi:** ShopERP `OtpService` (IMemoryCache, ⚠️ `Random` non-CSPRNG) · Gateway `MerchantSignupOtpService` (P4 planned) · collaborator OTP.
- **Chưa có:** Google/Zalo OAuth, passkey, anonymous ID toàn cục, `ContactVerification` record.
- **L3 đã có sẵn dạng thủ công:** tenant Pending → sysadmin `VerifyAsync`.

---

## 3. ÁP DỤNG THEO NHÓM NGƯỜI DÙNG

| Đối tượng | L-level ban đầu | Nâng cấp khi |
|---|---|---|
| Khách mua hàng (KhachLink) | L0 | Đặt hàng / tích lũy quyền lợi → L1 |
| Khách hàng thành viên | L1 | Liên kết tài khoản, đổi điểm → L2 |
| Cộng tác viên | L1 | Trước nhiệm vụ có thưởng → L2 |
| Chủ cửa hàng | L1 | Tạo tenant + bắt đầu kinh doanh → L2 |
| Hộ kinh doanh | L2 + L3 | Dùng dịch vụ thuế/hóa đơn → L3 (sysadmin verify) |
| HTX | L3 | **Phân biệt 3 việc:** verify tài khoản đăng nhập ≠ tư cách thành viên ≠ quyền biểu quyết — không thay thế nhau |
| Nhân viên Vạn An | L1 + MFA quản trị | Truy cập hệ thống nội bộ |

---

## 4. PHASES + TASK CARDS (đề xuất)

| Phase | Nội dung | Layer | Ghi chú |
|---|---|---|---|
| **ID-P0** | **Identity audit** — inventory User entity, customer accounts, membership, JWT claims, loyalty keys → `identity-inventory.md` | docs only | Điều kiện tiên quyết mọi phase sau |
| **ID-P1** | **Trust model core (2 hệ song song — Q1 đã chốt)** — Customer side: chuẩn hóa `IdentityLevel` ordering + map L0-L3 · Owner side: `DemoUser` += contact/trust fields (phone? external ids? trust level?) + `ContactVerification` record · EF + migration | Domain + CoreHub | **Gate 5 — cần duyệt** ✅ card đã duyệt 2026-11-10 · **bổ sung theo review:** `VerifiedChannel` (kênh verify — cheap, để provenance) + **downgrade hook** khi tenant deactivate (`BusinessVerified` → `ContactVerified`) |
| **MFA-1** | **Admin MFA (ưu tiên trước ID-P2 — REVIEW approved 2026-11-10)** — TOTP (authenticator app, 0đ) cho sysadmin/owner/staff nhạy cảm · trusted-device cookie + cảnh báo login IP lạ (tận dụng audit IP/UserAgent đã có từ W4 pháp lý) · email OTP fallback (Brevo, 0đ) khi mất authenticator | Gateway + CoreHub + ShopERP | Rẻ, không cần kênh trả phí, không đụng Domain L0-L3 · card `docs/AI/tasks/identity/task_idp8_admin_mfa.md` |
| **ID-P2** | **L1 free channels** — Google OAuth + Email magic link (Brevo free) · account linking flow · JWT claims += trust_level | Gateway + CoreHub + KhachLink | Google consent screen lead time ~vài ngày · Q2/Q3 đã chốt (§7) trước khi code |
| **ID-P3** | **L2 contact verification** — hoàn thiện P4 channel-agnostic (**email trước 0đ — Brevo · chốt 2026-11-10**) + `ContactVerification` có `purpose` · **kèm hardening `OtpService` cũ** (CSPRNG + attempt limit + purpose binding — lỗ hổng B1 review) · ZNS 300đ slot khi OA sẵn sàng | Gateway + CoreHub | P4 card hiện tại = phase này (đã cập nhật) |
| **ID-P4** | **Linking & recovery UX** — link nhiều providers vào 1 UserId · recovery flow · anonymous→registered merge rules (cart giữ, không merge tự động) | KhachLink + ShopERP + CoreHub | |
| **ID-P5** | **Enforcement** — action→min-trust map · `RequireTrustLevel` policy/filter · audit log truy cập | Gateway + tất cả WebApps | |
| **ID-P6** | **L3 business/legal** — map tenant verify pipeline lên TrustLevel · HTX member/voting distinction | CoreHub + ShopERP | Tái dùng `VerifyAsync` |
| **ID-P7** | **Passkey/WebAuthn** — phase sau, khi L0-L2 ổn định | KhachLink + Gateway | Defer |

**Dependency:** `P0 → P1 → {MFA-1 ∥ P2 ∥ P3} → P4 → P5 → P6` · P7 độc lập sau P5.
**MFA-1 không nằm trên dependency L0-L3** — chạy song song từ sau P1, KHÔNG block P2/P3; chỉ nên hoàn thành TRƯỚC khi public admin surface rộng hơn (IdP vision §1A).
**P4-OTP card hiện tại** đóng vai trò ID-P3 — không bỏ, chỉ định vị lại scope (§6 card đã patch).

---

## 5. SUCCESS CRITERIA

1. Khách vãng lai dùng app đầy đủ ở L0 — không OTP, không email bắt buộc.
2. Đăng ký tài khoản L1 hoàn toàn 0đ (Google/email link) — OTP không nằm trên đường critical.
3. Mọi UserId bất biến; liên kết/nâng cấp không mất lịch sử giao dịch/điểm/ví.
4. Action→trust mapping enforce được; audit log đầy đủ.
5. OTP chỉ phát sinh phí khi thật sự cần verify liên hệ (L2) — `RequireOtp` mặc định false cho đến khi có use-case cụ thể.
6. HTX: 3 loại verify (account/member/voting) tách bạch, audit được.

---

## 6. RISKS

| Risk | Mitigation |
|---|---|
| Identity hiện tại rải rác (owner vs customer vs membership) — service mới chồng lấn | ID-P0 audit bắt buộc trước; có thể chỉ cần mở rộng User entity thay vì service mới |
| JWT HS256 symmetric share secret Gateway↔ShopERP — OAuth trust_level claim phải nhất quán 2 phía | ID-P2 spec claim format + test cross-validation; dài hạn cân nhắc asymmetric |
| Zalo Login/Google OAuth lead time approval | Làm song song P2 email magic link (không phụ thuộc) |
| Anonymous ID dễ fake/mất khi clear data | Chấp nhận — chỉ dùng cho cart/session, không làm khóa merge; merge chỉ khi user chủ động link |
| Trust Level bị bypass (check quên ở endpoint mới) | ID-P5 tập trung `RequireTrustLevel` attribute/policy + arch-test quét endpoint public |
| Domain change lớn | Gate 5: trình bày entity design ở ID-P1 trước, user duyệt mới code |
| **Admin password-only, không MFA (REVIEW B2 — 2026-11-10)** | **MFA-1** (TOTP 0đ + trusted device + email fallback) — ưu tiên trước ID-P2; cổng L3 verify là điểm tấn công giá trị nhất (đầu độc trust registry, HĐĐT giả = rửa tiền) |
| **Trust level monotonic — không downgrade khi tenant deactivate (REVIEW B3 — 2026-11-10)** | ID-P1 thêm `RevokeBusinessVerification` + hook ở mọi path tenant→inactive (RejectPendingAsync + admin deactivate + cleanup job) → `BusinessVerified` → `ContactVerified` |
| **`OtpService` cũ: `Random` non-CSPRNG + không attempt limit + không purpose (REVIEW B1 — 2026-11-10, verified code)** | Hardening trong ID-P3 (cùng wave P4 email-first): `RandomNumberGenerator` + attempt counter + purpose binding + thống nhất state store |
| **Trust thiếu provenance (REVIEW B7 — 2026-11-10)** | ID-P1 thêm `VerifiedChannel` (kênh đã verify: email/zns/sms/manual) — rẻ, đủ cho audit giai đoạn đầu; `ContactVerification` đầy đủ khi ID-P3 |

---

## 7. OPEN QUESTIONS (chờ user chốt)

- Q1: ~~IdentityProfile aggregate unify?~~ → **CHỐT 2026-11-10: 2 hệ song song là đúng.** Customer (KhachLink L0-L2) ≠ DemoUser (ShopERP/Gateway tenant-scoped). Collaborator (Salesman/Shipper) thuộc customer hệ — KHÔNG cấp backend login; workspace = customer UI gated `CommunityRole`+`IsPhoneVerified`. ID-P1 scope = chuẩn hóa `IdentityLevel` ordering + mở rộng DemoUser contact/trust fields. Chi tiết: `identity-inventory-p0.md` §3b.
- Q2: Google OAuth có cần verify email Google hay chấp nhận unverified? → **CHỐT 2026-11-10: yêu cầu `email_verified=true`** — không chấp nhận unverified email làm L1 (giữ nguyên tắc "L1 = control account bên thứ 3"; unverified email Google không chứng minh gì).
- Q3: Zalo Login có trong scope đợt đầu không, hay defer sau Google+email? → **CHỐT 2026-11-10: DEFER** — không có infra sẵn (chỉ `ZaloAlertNotifier` stub alert kho, không login/ZNS), lead time OA approval; Google OAuth + email magic link đủ phủ L1. **Mốc re-evaluate:** sau ID-P2 hoàn thành + khi có Zalo OA verified (ZNS song song cho L2). Không defer vô thời hạn.
- Q4: Trust Level lưu ở User (PG) hay tính động từ ContactVerification records? → **CHỐT 2026-11-10: lưu cache `TrustLevel` trên User + recompute từ records khi cần** (pattern cache + provenance). Monotonic trên cấp nâng; **CÓ hook downgrade** khi sự thật nền thay đổi (tenant deactivate → `BusinessVerified` → `ContactVerified`).
- Q5: Anonymous ID persistence — localStorage UUID đủ hay cần server-issued anonymous session? → **CHỐT 2026-11-10: localStorage UUID đủ cho MVP L0** (cart/session only, không làm merge key — merge chỉ khi user chủ động link). Server-issued session khi cần cross-device anonymous tracking (ID-P4).

---

## 8. QUYẾT ĐỊNH REVIEW 2026-11-10 — 4/4 ĐỀ XUẤT ĐƯỢC DUYỆT (implement đầy đủ)

> **Bối cảnh:** user yêu cầu review L0-L3 + câu hỏi "bỏ SMS OTP và Zalo có hợp lý?" → review xong (`REVIEW_ONLY`, findings B1-B11 ghi §6) → user duyệt 4 đề xuất sau:

| # | Đề xuất | Quyết định | Nơi implement |
|---|---|---|---|
| 1 | ID-P1: thêm `VerifiedChannel` (provenance) + **downgrade hook khi tenant deactivate** | ✅ DUYỆT | `task_idp1_demo_user_trust_fields.md` §3.2/§3.4 + test §4 |
| 2 | P4/ID-P3: chốt **kênh đầu = Brevo email 0đ** + composite fallback + slot ZNS; **hardening `OtpService` cũ** cùng wave | ✅ DUYỆT | `task_p4_otp_phone_verification.md` §3.1a/§6 + sub-task hardening §8 |
| 3 | ID-P2: chốt Q2/Q3 trước khi code; Zalo Login defer có mốc re-evaluate | ✅ DUYỆT | §7 Q2/Q3 (chốt ở trên) — áp dụng khi viết card ID-P2 |
| 4 | **Phase MFA-1 Admin TOTP** (0đ) — ưu tiên trước ID-P2 | ✅ DUYỆT | `task_idp8_admin_mfa.md` (card mới) |

**Nguyên tắc zero-cost giữ nguyên:** MFA-1 dùng TOTP (authenticator app) + email OTP (Brevo free) — KHÔNG cần SMS/ZNS trả phí.

**RV bắt buộc sau implement (mọi phase):** xem mục VERIFICATION + RV section trong từng card — build/CI PASS ≠ runtime OK; chạy RV test production (Playwright rv config pattern `playwright-rv-*.config.ts`) trước khi đóng card.
