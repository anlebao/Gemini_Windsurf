# Task ATRS-17: FR-001 account lock/delete → ATRS sync (account thuộc ShopERP offline)

> **Nguồn:** SRS traceability review 2026-11-17 (GAP #4) + user confirm feasibility · **Mode:** IMPLEMENT
> **SRS coverage:** FR-001 "Nếu tài khoản bị khóa/xóa theo chính sách hiện hữu, ATRS phải nhận sự kiện đồng bộ trạng thái phù hợp"

## 1. OBJECTIVE
ShopERP lock/delete user (DemoUser/Customer) → ATRS cập nhật `AccountTrustProfile.Status` (RESTRICTED/CLOSED) để risk engine + staff review thấy trạng thái account thật. Không cần real-time tuyệt đối (ShopERP offline → event trễ, risk engine bù).

## 2. THIẾT KẾ (dựa trên hạ tầng hiện có — KHÔNG thêm NATS vào atrs)
```
ShopERP (lock/delete user) ──publish NATS `vanan.shoperp.user.status.{id}`──▶ Gateway DataSyncSubscriber
   (đã subscribe `vanan.shoperp.>`)
        └── HTTP POST atrs `/api/v1/internal/account-status` (X-Internal-Api-Key)
                └── atrs: resolve TrustSubject (DemoUser/Customer link) → AccountTrustProfile.Status
                        = RESTRICTED (lock) | CLOSED (delete) + AccountSecurityEvent
```
- Pattern: `GatewayTokenVersionBumpNotifier` (atrs→Gateway) đảo chiều + `InternalLoyalty:ApiKey` style key.
- `AccountTrustProfile` đã có `RESTRICTED/CLOSED` status (AtrsEnums) — chỉ cần internal endpoint + logic set.
- Gate: risk engine recovery khi profile RESTRICTED → REQUIRE_MANUAL_REVIEW mạnh hơn.

## 3. FILES
- atrs: `AdminInternalAccountStatusController` (internal key) + `TrustProfileService.SetStatusAsync(identityType, identityId, status)` + audit event + test.
- Gateway: `DataSyncSubscriber` handle subject `vanan.shoperp.user.status.*` → `IHttpClientFactory` call atrs (ATRS_REMOTE_HOST cluster base).
- ShopERP: `UserSyncSubscriber`/`UserManagementService` publish event khi lock/delete (xác định đúng điểm lock/delete hiện có).

## 4. TESTS
- [ ] atrs: internal endpoint auth (key sai 401) + set status + audit + resolve đúng subject
- [ ] Gateway subscriber: event → HTTP call atrs (mock)
- [ ] Recovery trên profile RESTRICTED → review bắt buộc

## 5. RV (sweep prod)
- [ ] Lock 1 user test → atrs profile RESTRICTED (psql verify) + recovery request của user đó → manual review

## 6. STATUS
- 2026-11-17: card tạo (chưa implement — xếp sau FIX-07).