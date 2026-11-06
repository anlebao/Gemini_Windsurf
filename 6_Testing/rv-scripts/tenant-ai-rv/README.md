# tenant-ai-rv — Runtime Verification cho `vanan-ai` (task_ai_p6 / CP3)

RV production chạy **trên VPS `vanan-ai`** (internal-only port 5060 — đúng threat model
D6: external port đóng, chỉ VPC/nginx tới được). Script ký HMAC tenant-ctx bằng
`openssl` với `TENANTAI_CTX_SIGNING_SECRET` đọc từ `/opt/vanan-ai/.env.tenantai`
(secrets KHÔNG in ra log/report).

## Cách chạy

```powershell
# từ Windows (gcloud SDK authed vanan-prod)
pwsh 6_Testing/rv-scripts/tenant-ai-rv/invoke-rv.ps1 -TenantId <guid-tenant-pilot>

# đầy đủ options
pwsh invoke-rv.ps1 -TenantId <guid> -TenantB <guid-khac> -ZeroWalletTenant <guid-vi-0>

# hoặc trực tiếp trên VPS
RV_TENANT_ID=<guid> bash run-on-vps.sh
```

## Coverage

| Bước | Check | Threat |
|------|-------|--------|
| 1 | `/v1/health` 200 + `db=up` | — |
| 2 | no-key 401 · bad-key 401 · good-key 200 · missing ctx 401 · forged sig 401 · expired ctx 401 · valid ctx 200 | T6 |
| 2 | gateway key → `/v1/knowledge/*` + `/v1/admin/*` → 403 | T12 |
| 3 | `products:batch` → upserted=1 · `/v1/knowledge/status` sourceVersion+syncedAt (drift/reconcile) | — |
| 4 | doc pending → chat không dùng → approve → retrievable | T11 |
| 5 | chat ≥5 câu (menu/price/faq/story/order-help) → 200, không refused; price answer có product card pid | grounded NFR-10 |
| 6 | tenant B hỏi product/doc của A → không thấy | T1 (SQL+semantic path) |
| 7 | 5 injection payloads → refused | T2 |
| 8 | tenant ví=0 → `quota_denied` (cần `-ZeroWalletTenant`) | T8 |
| cleanup | DELETE product + document test | — |

## Yêu cầu tenant test

- `RV_TENANT_ID`: tenant nằm trong `TenantAi:BootstrapIngestScope` của ingest caller
  (nếu scope=null/`"*"`, tenant nào cũng được). Nên dùng pilot tenant An Tâm hoặc
  tenant test chuyên dụng — script tự cleanup dữ liệu nó ingest.
- `RV_TENANT_ID_B`: bất kỳ tenant nào KHÔNG có data trùng marker (default GUID ảo OK —
  gateway caller scope `*` nên ctx hợp lệ).
- `RV_ZERO_WALLET_TENANT`: tenant có ví metering = 0 để verify deny path (T8) —
  skip nếu không truyền.

## Kết quả

- stdout + `report.txt` trên VPS → kéo về `tenant-ai-rv-report.txt` local.
- Exit 0 khi `FAIL=0` (SKIP không tính fail — ingest-scope 403 hoặc thiếu zero-wallet tenant).
