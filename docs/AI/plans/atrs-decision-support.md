# HỖ TRỢ CHỐT 6 DECISIONS + RECOVERY-ENTRY AUTH MODEL (ATRS Wave 0)

> **Created:** 2026-11-15 · **Mục đích:** cung cấp bằng chứng + trade-off + khuyến nghị để user chốt nhanh 6 decisions "chốt ngay" (master plan §3) + open question recovery-entry auth model (ATRS-8).
> **Nguyên tắc:** mỗi quyết định kèm bằng chứng codebase/SRS + lựa chọn + rủi ro nếu chọn khác. Khuyến nghị = an toàn, ít nợ kỹ thuật, không chặn IdP vision tương lai.

---

## D1 — Canonical identity: ATRS profile gắn với gì?

**Bằng chứng thực tế:**
- `Customer` = per-tenant (`IMustHaveTenant`), 1 người có thể có N Customer rows ở N tenant + guest stubs (`IdentityLevel.Guest`) merge theo `DeviceId` qua `CustomerMergeService`.
- `DemoUser` (staff/owner) = per-tenant, password-only + MFA fields.
- Loyalty là **silo per (customer, tenant)** — "luật Silo".
- Identity master plan nguyên tắc 1: *"UserId UUID bất biến là khóa nội bộ duy nhất — KHÔNG dùng SĐT/email làm PK"*.

**Lựa chọn:**
| Option | Mô tả | Ưu | Nhược |
|---|---|---|---|
| **a) `TrustSubjectId` (UUID mới) + link table** | Profile key = UUID độc lập; `TrustSubjectLink` map → `CustomerId` (per-tenant), `DemoUserId`, DeviceId-cluster; lookup bằng `PrimaryContactHash` (HMAC, index-only) | Đúng nguyên tắc identity plan; 1 profile/người xuyên tenant/app; sẵn sàng IdP vision; không dùng PII làm PK | Mapping phức tạp; cần resolve-by-contact |
| b) DeviceId-cluster | Gộp theo device | Đơn giản | **Sai nguyên tắc cốt lõi SRS**: device là tín hiệu, không phải định danh; device churn thường xuyên |
| c) Phone-hash làm PK | Contact-hash = khóa | Đơn giản | Vi phạm nguyên tắc identity plan (PII làm PK); đổi SĐT = mất profile |

**Khuyến nghị: (a) `TrustSubjectId`.** Chi phí thêm là 1 bảng link + HMAC lookup key — nhỏ so với việc sửa sai sau này. HMAC key đặt trong secret manager (pattern sẵn có). Cột `ContactHash` additive trên `Customer`/`DemoUser` → phối lịch với ID-P1/P4 (họ cũng cần lookup theo contact).

**Rủi ro nếu chọn khác:** (b) → recovery/dispute gắn thiết bị, vỡ khi đổi máy — đúng cái SRS cấm; (c) → đổi SĐT = mất lịch sử bảo mật + vi phạm governance identity.

---

## D2 — Deployment: ATRS nằm ở đâu?

**Bằng chứng:**
- 3 service độc lập đã có pattern chuẩn: `8_Metering` (SQLite), `9_TenantAI` (PG), `10_Voice` (SQLite) — mỗi cái: sln riêng, Dockerfile, port riêng, CI matrix trong `cd-multivps.yml`, contracts trong `1_Shared`.
- Gateway hiện đã nặng: YARP + community entities + loyalty PG source-of-truth + internal API keys.
- **Hard stop:** CoreHub = class library, không host API → SRS §4.1 "đến CoreHub" phải sửa dù chọn gì.

**Lựa chọn:**
| Option | Ưu | Nhược |
|---|---|---|
| **a) Service riêng `vanan-atrs`** (pattern `10_Voice`) | Blast radius riêng; pattern proven 3× (CI/CD sẵn); attachable by contract; không phình Gateway; cross-tenant PG dễ | Thêm 1 VPS/compose/ops surface |
| b) Module trong Gateway PG | Không thêm service; kế thừa `InternalApiKey` | Gắn chu kỳ deploy Gateway; domain mới chen vào Gateway vốn đã nặng; khó scale riêng |
| c) Module trong ShopERP | Gần staff UI | KhachLink customer flow phải đi vòng; ShopERP là per-tenant SQLite — **sai** cho profile cross-tenant |

**Khuyến nghị: (a) service riêng, PG riêng.** Trust registry là cross-tenant nên PG (không phải SQLite per-tenant). Tận dụng 100% pattern voice: sln 4 projects (Api/Contracts/Domain/Infra/Tests) + Dockerfile port ~5080 + `cd-multivps.yml` matrix + auth bằng `StaffJwtValidator` clone + customer token forward tới ShopERP `/me` (pattern đã có trong `DeviceRegistrationController`).

**Rủi ro nếu chọn (b):** tạm ổn nhưng ATRS gắn vòng đời Gateway; nếu sau này tách → migration toàn bộ.

---

## D3 — Token revocation: làm thế nào?

**Bằng chứng:** JWT HS256 8h stateless (staff) · customer token IDataProtector 30 ngày · **không có revocation nào** · validators clone ở 3 nơi (Gateway, voice, tenantai).

**Lựa chọn:**
| Option | Mô tả | Ưu | Nhược |
|---|---|---|---|
| **a) `TokenVersion` claim + cache check** | Cột `TokenVersion` (int); JWT claim `tv`; customer token payload += version; validator so với cache (TTL 60s, bump = write-through) | Additive, per-user kill; đơn giản; tương thích JWT lẫn customer token | Cửa sổ ≤60s token cũ còn sống (chấp nhận được, ghi rõ); phải đồng bộ 3 validators |
| b) Rút TTL | JWT 1h, customer 7d | Đơn giản nhất | Không revoke token ĐÃ cấp — recovery approve vẫn không đá được attacker |
| c) Blacklist jti | Lưu revoked jti | Chính xác tức thì | Bộ nhớ tăng; phức tạp hơn (a) cho ít lợi ích hơn |

**Khuyến nghị: (a).** Bump version khi: recovery COMPLETED, đổi password, đổi MFA, revoke device, staff break-glass. Viết **1 helper dùng chung** (`TokenVersionValidator` trong `1_Shared`) → 3 validators gọi chung, hết drift. Cache 60s + write-through khi bump (không chờ TTL). Customer token: version per-TrustSubject (xuyên tenant) + per-Customer fallback (theo D1).

**Rủi ro nếu chọn (b):** FR-011 "thu hồi token cũ" không thật sự làm được — recovery đá attacker không hiệu quả.

---

## D7 — Secret question (U04): giữ hay bỏ?

**Bằng chứng:**
- FR-007 chính nó: *"Cân nhắc không triển khai security question ở các luồng có thể thay thế bằng passkey/recovery code an toàn hơn."*
- UI SRS U04: *"If security review rejects secret questions, omit U04 from navigation and mark the feature as not implemented; do not silently replace it."*
- §9.1 phân loại: secret answer = **"Thấp — dễ đoán/lộ"** — yếu nhất trong mọi bằng chứng.
- Thay thế đã có: recovery codes (ATRS-4) + passkey (ATRS-5) + manual review (ATRS-8) + verified contact.

**Khuyến nghị: OMIT v1.0** — đánh dấu "not implemented, documented" trong SRS (không giấu, không thay bằng factor yếu khác). Tiết kiệm: Argon2id+pepper, hint encrypted, rate-limit/lockout, UI U04, toàn bộ rule "staff không thấy đáp án", thêm attack surface.

**Rủi ro nếu giữ:** thêm 1 factor yếu nhất cho ít giá trị; tốn 1 card phụ + wave-slice. Nếu PO có use-case cụ thể (vd người dùng lớn tuổi quen security questions) → bật lại card ATRS-6 conditional — nhưng KHÔNG làm trước v1.0.

---

## D8 — AccountSecurityEvents: bảng riêng hay extend AuditLog?

**Bằng chứng:** `AuditLog` immutable append-only đã có (ForSecurityEvent factory, background writer) — nhưng schema FR-009 cần thêm: `ActorType(USER/SYSTEM/STAFF/SERVICE)`, `DeviceId`, `SessionId`, `Outcome`, `ReasonCode`, `EvidenceReferences`, `SchemaVersion`. AuditLog hiện thiếu các cột này.

**Lựa chọn:**
| Option | Ưu | Nhược |
|---|---|---|
| **a) Bảng riêng `AccountSecurityEvents`** (FR-009 schema đầy đủ) + AuditLog giữ cho entity CRUD | Đúng schema SRS; volume + retention riêng (2 năm vs audit kế toán); A20 query riêng; không làm nặng log kế toán | 2 nguồn audit → cần CorrelationId chung + docs |
| b) Extend `AuditLog` (thêm cột nullable) | 1 bảng | Làm bẩn audit tài chính append-only; enum SecurityEvent sẵn nhưng schema thiếu; migration phức tạp hơn |

**Khuyến nghị: (a) bảng riêng.** Mọi event quan trọng ghi cả 2 khi cần trace với kế toán (chung `CorrelationId`). A20 Audit explorer đọc chủ yếu `AccountSecurityEvents`.

**Rủi ro nếu chọn (b):** audit tài chính bị trộn dữ liệu bảo mật volume lớn → truy vấn chậm + retention khó tách.

---

## D10 — Scope v1.0: customer-only hay cả staff?

**Bằng chứng:** MFA-1 (admin TOTP) đang triển khai riêng cho staff · DemoUser đã có MFA fields + TrustLevel · staff base nhỏ (owner + staff per tenant) · customer là volume lớn + pain point chính (login phụ thuộc OTP).

**Lựa chọn:**
| Option | Ưu | Nhược |
|---|---|---|
| **a) Customer-only** recovery/dispute v1.0; staff = MFA-1 + runbook manual reset | Đúng pain point; không phình scope; staff đã có MFA-1 | Owner mất password+MFA → phải có runbook TRƯỚC khi bật (làm ở wave 0/ops doc) |
| b) Cả customer + staff | Đầy đủ | Staff recovery phức tạp (tenant scope, role, break-glass) → thêm 1-2 wave; chậm value |

**Khuyến nghị: (a).** Device/credential của staff vẫn ghi nhận qua `DeviceRegistration` extend (fraud signal) nhưng recovery workflow staff = runbook + MFA-1 re-evaluate sau v1.0. **Bắt buộc kèm theo:** runbook "reset staff account" viết ở wave 0 (ops doc) trước khi bất kỳ tính năng customer recovery nào live.

**Rủi ro nếu chọn (b):** scope tăng ~30-40%, delay mọi thứ.

---

## Open question — Recovery-entry auth model (ATRS-8)

**Vấn đề:** user mất HẾT credential (không token, không code) → vẫn phải submit được recovery request — nhưng endpoint không-auth là attack surface lớn nhất.

**Lựa chọn:**
| Option | Mô tả | Ưu | Nhược |
|---|---|---|---|
| **a) Tiered proof (khuyến nghị)** | ① Có recovery code → verify code · ② Không code → verified-contact OTP (email-first Brevo 0đ — P4 đã chốt) + captcha + risk engine · ③ Không gì → **manual review mặc định (R3)** + maker-checker | Cover mọi tình huống §6.4; không lộ attack path nào; tận dụng P4 channel | Phụ thuộc verified channel tồn tại (customer không email → SMS/manual) |
| b) Chỉ cho submit khi còn 1 factor | Đơn giản | **Bỏ sót use-case "mất tất cả"** — chính là FR-011/F04 mô tả |
| c) Device-token only | Gửi từ thiết bị mới có installation ID | Tín hiệu yếu, giả mạo được | Trái SRS (installation ID không cấp quyền) |

**Khuyến nghị: (a) tiered proof.** Đi kèm bắt buộc: captcha ở entry, anti-enumeration (response generic, không phân biệt account tồn tại), rate limit + lockout progressive theo contact/device/IP-signal, risk engine R3 → PENDING_REVIEW + maker-checker. Mọi fail đều log `AccountSecurityEvents` (ActorType=USER, không lộ lý do).

**Phụ thuộc cần verify sớm:** customer có email để nhận OTP không? (`Customer.Email` tồn tại nhưng tỷ lệ điền?) → nếu thấp, thêm kênh SMS (provider cần xác nhận) hoặc manual review là path mặc định. Card ATRS-13 sẽ verify — nhưng quyết định này không chặn wave 1 (wave 1 có thể bắt đầu với path ① recovery code + ③ manual).

---

## BẢNG TÓM TẮT CHỐT NHANH

| Decision | Khuyến nghị | Một câu lý do |
|---|---|---|
| D1 | `TrustSubjectId` + link table | 1 profile/người xuyên tenant, đúng nguyên tắc identity plan, không PII làm PK |
| D2 | Service riêng `vanan-atrs` (PG, pattern voice) | Pattern proven 3×, blast radius riêng, cross-tenant PG |
| D3 | `TokenVersion` claim + cache check | Additive, revoke đúng per-user, 1 helper chung hết drift |
| D7 | **OMIT** secret question | Factor yếu nhất, thay thế đã đủ (codes/passkey/manual) |
| D8 | Bảng riêng `AccountSecurityEvents` | Đúng schema FR-009, retention/truy vấn riêng, sạch audit kế toán |
| D10 | Customer-only v1.0 + runbook staff | Đúng pain point, staff đã có MFA-1 |
| Recovery entry | Tiered proof (code → contact-OTP → manual) | Cover "mất tất cả" an toàn nhất có thể |

**Tối thiểu để khởi động wave 1:** D1 + D2 + D3 + D8 (D7/D10 chỉ ảnh hưởng scope/nội dung SRS patch, không chặn code nền). Recovery-entry model chốt trước ATRS-8.

---

*Tài liệu hỗ trợ quyết định — không thay thế quyết định của user/PO. Sau khi chốt: cập nhật master plan §3 + card ATRS-1 → patch SRS.*