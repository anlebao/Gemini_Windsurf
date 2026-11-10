# IDENTITY INVENTORY — Phase ID-P0 Audit (2026-11-10)

> **Master plan:** `van-an-identity-tiered-trust-master-plan.md` §4 ID-P0
> **Mode:** ANALYZE — read-only audit, không code change
> **Kết luận chính:** hệ sinh thái **đã có sẵn tiered identity ở customer side** (`IdentityLevel` enum + DeviceId + MergeService) — Zero-Cost Identity KHÔNG phải xây mới hoàn toàn; vấn đề là **2 hệ identity song song chưa thống nhất** (Customer per-tenant vs DemoUser owner/staff) và chưa có external login.

---

## 1. HAI HỆ IDENTITY HIỆN TẠI (song song, chưa liên thông)

### 1a. Customer Identity — phía Khách hàng (ShopERP SQLite, per-tenant)

Entity `Customer` (`1_Shared/Domain.cs:828`):

| Field | Ghi chú |
|---|---|
| `Id` = `CustomerId.Value` | ✅ Single-identity pattern đúng |
| `FullName`, `PhoneNumber`, `Email?` | PII cơ bản |
| `LoyaltyPoints`, `CustomerTier` (Bronze→Platinum) | gắn tenant |
| **`IdentityLevel`** (`Domain.cs:815`) | **ĐÃ CÓ tiered model**: `Guest=0, Social=1, Verified=2 (SMS OTP), Full=3, DeviceVerified=4` |
| `DeviceId?` | anonymous tracking — tiền thân của L0 |
| `IsActive`, soft-delete | |

Domain methods: `UpgradeIdentityLevel` (chỉ tăng, không giảm), `MarkAsGuestStub` (bảo vệ account thật khi merge theo DeviceId).

**`CustomerMergeService`** (`3_CoreHub/Services/`): merge DeviceId guest stubs → login account; merge loyalty points; community data (roles/referrals/wallet) là **PG-only** qua `VanAnDbContext` riêng (comment 2026-09-25). → Đã có "anonymous → registered merge" đúng nguyên tắc master plan, nhưng chỉ trong phạm vi 1 tenant.

**Customer OTP login (ShopERP):**
- `5_WebApps/ShopERP/Controllers/CustomerIdentityController.cs` → `/api/customer-identity/otp/send|verify`, `upgrade/send-otp|verify-otp`
- `5_WebApps/ShopERP/Services/OtpService.cs` — IMemoryCache 5', 6 số. **⚠️ Dùng `System.Random` (không CSPRNG) + không rate-limit counter + không bind purpose** — cần hardening nếu giữ.
- Gateway forward: `2_Gateway/Controllers/CustomerIdentityController.cs` + `CustomersController.cs` (AllowAnonymous).
- SMS gửi qua đâu: cần verify — nhiều khả năng `ISmsService`/eSMS (cũng `not-configured` → customer OTP login hiện có thể đang degraded).

**Collaborator:** `CollaboratorVerificationController` (Gateway) — SMS OTP verify + wallet deposit cho phí OTP — pattern "user tự trả phí verify" đã tồn tại.

### 1b. Owner/Staff Identity — phía Tenant (Gateway PG)

Entity `DemoUser` (`1_Shared/Domain/Aggregates/UserAggregate/DemoUser.cs`):

| Field | Ghi chú |
|---|---|
| `Username`, `PasswordHash` (bcrypt), `DisplayName`, `Email?`, `Role`, `IsActive`, `TenantId` | **KHÔNG có phone, KHÔNG external identity, KHÔNG trust level** |
| `UserTenant` join entity | user↔tenant |
| `UserRole`, `PermissionGroup`, `UserPermissionGroup` | RBAC per-tenant |

- JWT (`3_CoreHub/Services/JwtTokenService.cs`): HS256 symmetric, claims `sub`(userId), `email`, `tenant_id`+`TenantId`, `role` — **không có trust_level/identity_level claim**. Gateway validate cùng secret; ShopERP cũng phát hành → shared-secret cross-service.
- Owner phone/email đi qua `TenantSettings` (VerifyAsync `OwnerPhone`/`OwnerEmail`) — **không nằm trên DemoUser** → liên hệ owner nằm rải rác settings, không phải identity attribute.

---

## 2. MA TRẬN ĐỐI CHIẾU L0–L3 ĐỀ XUẤT vs HIỆN CÓ

| Cấp | Đề xuất | Hiện có | Gap |
|---|---|---|---|
| L0 Guest | Device/Anonymous ID | `Customer.DeviceId` + guest stub + merge service ✅ (chỉ customer, per-tenant) | Chưa có anonymous ID cho owner-side/KhachLink global; DeviceId là Guid local — chưa chuẩn hóa phát hành |
| L1 Social | Google/Zalo OAuth, email link, passkey | **Không có** — chỉ username+password (owner) và phone+OTP (customer) | Toàn bộ net new |
| L2 Contact Verified | verify SĐT/email theo nhu cầu | `IdentityLevel.Verified` (SMS OTP) + `DeviceVerified` trên Customer; P4 merchant-signup OTP đang thiết kế | OTP infra trùng lặp 2 nơi (ShopERP OtpService vs Gateway MerchantSignupOtpService); chưa có `ContactVerification` record có purpose/channel |
| L3 Business/Legal | MST/GPKD, đại diện, HTX member/voting | Tenant Pending→`VerifyAsync` sysadmin = L3 thủ công ✅ | Chưa map lên trust level; HTX member/voting chưa tách |

---

## 3. PHÁT HIỆN QUAN TRỌNG CHO ID-P1

1. **`IdentityLevel` enum đã tồn tại nhưng semantics lệch** so với L0-L3 đề xuất: `DeviceVerified=4` > `Full=3` phá ordering `UpgradeIdentityLevel` dùng `>`; `Verified` comment ghi "SMS OTP". → ID-P1 nên **chuẩn hóa/mở rộng enum này** thay vì tạo enum song song (tránh 2 hệ trust).
2. **Q1 master plan có hướng trả lời sơ bộ:** Customer đã là aggregate giàu (identity+loyalty+merge). Owner-side `DemoUser` nghèo. Hai lựa chọn:
   - **A. IdentityProfile aggregate mới** — trung lập, link được cả Customer lẫn DemoUser; phù hợp "1 tài khoản toàn hệ sinh thái" nhưng phải giải bài toán user≠customer (owner có thể cũng là customer).
   - **B. Mở rộng DemoUser + chuẩn hóa Customer.IdentityLevel** — ít migration hơn nhưng vẫn 2 hệ song song.
   - **Đề xuất audit tiếp:** kiểm tra `UserTenant`/JWT `sub` có overlap với `CustomerId` không; nếu hệ sinh thái coi "người" là 1 thực thể xuyên suốt → nghiêng A.
3. **OTP state fragmentation:** `OtpService` (ShopERP, IMemoryCache) + `MerchantSignupOtpService` (Gateway, planned IMemoryCache) + collaborator OTP — 3 nơi. ID-P3 nên gom về `IIdentityOtpChannel` + 1 state store.
4. **`OtpService` hardening nợ:** `Random` → `RandomNumberGenerator`, thêm attempt counter + purpose binding — fix nhỏ, có thể làm ngay trong ID-P3. → **✅ DONE 2026-11-10 (W0):** CSPRNG + attempt lock 5/15' + purpose namespace (`otp:{purpose}:{contact}`) — `5_WebApps/ShopERP/Services/OtpService.cs` + tests 9/9 (`OtpServiceTests.cs`).
5. **DeviceId hiện per-tenant Customer** — anonymous ID toàn cục (L0 xuyên app) cần cookie/storage strategy riêng ở KhachLink; không nên tái dùng Customer.DeviceId (scope tenant).

### 3b. Collaborator (Salesman/Shipper) — PHÂN TÍCH 2026-11-10 (user chốt)

- Collaborator = `Customer` + `CommunityRole` (SalesmanCode, IsPhoneVerified) — **KHÔNG phải DemoUser**. Tenant profile `Type=null` do `CollaboratorTenantProvisioningService` tạo chỉ để chứa HTX membership (`OwnerCustomerId` link).
- **Quyết định: KHÔNG cấp DemoUser/backend login cho collaborator.** Lý do: DemoUser = tenant staff RBAC → cấp nhầm = mở attack surface dữ liệu tenant; tenant profile `Type=null` không có feature gì để dùng; mọi nhu cầu (QR/SalesmanCode, ví, hoa hồng, đơn giao) đã có API customer-side.
- **Workspace của collaborator = customer-facing UI** (KhachLink) gated bởi `CommunityRole` + `IsPhoneVerified` (đã là L2).
- **Trigger đổi quyết định:** collaborator cần chức năng vượt customer scope (dispatch board real-time, staff part-time của tenant cụ thể → lúc đó là `UserTenant` staff relation, không còn là collaborator).
- Shipper case: workflow operational → trang "Đơn của tôi" customer-side hoặc app riêng với customer token + CommunityRole gate — vẫn không cần DemoUser.

## 4. CẬP NHẬT MASTER PLAN

- §2 "HIỆN TRẠNG" → thay bằng bảng audit đầy đủ ở §1-2 file này.
- §7 Q1 → có data: Customer-side đã tiered; quyết định còn lại là **unify hay song song có kiểm soát**.
- Thêm nợ kỹ thuật: `OtpService` CSPRNG + attempt-limit; enum `IdentityLevel` ordering fix; OTP state store consolidation.

## 5. NEXT (chờ user)

- [x] ~~User review inventory → chốt hướng ID-P1~~ → **QUYẾT ĐỊNH 2026-11-10: 2 hệ song song là đúng.** Customer (KhachLink) không cần biết ShopERP/Gateway PG; DemoUser là tenant-scoped staff. KHÔNG build IdentityProfile aggregate unify.
- [ ] Chuẩn hóa `IdentityLevel` enum ordering (`DeviceVerified=4 > Full=3` bug) — ID-P1 scope nhỏ
- [ ] ID-P1 task card: mở rộng DemoUser (trust/contact fields) + fix enum + `ContactVerification` record nếu cần
