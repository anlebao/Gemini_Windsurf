# ATRS Operational Runbook — vanan-atrs (v1.0)

> **Created:** 2026-11-17 (ATRS-15 §17.4.11 hoàn thiện từ draft W6) · **Service:** `vanan-atrs` · VPS `vanan-atrs` (34.126.90.228) · port :5080 · DB: PG `vanan_atrs` (container `vanan-atrs-postgres-1`, user `atrs`)
> **Stack:** Docker Compose (`docker-compose.atrs.yml`) · image `ghcr.io/anlebao/vanan-atrs:latest` · deploy qua `cd-multivps.yml` (matrix atrs)

## 1. Health & Metrics

```bash
curl -s http://34.126.90.228:5080/health        # {"status":"ok","service":"vanan-atrs"}
curl -s http://34.126.90.228:5080/metrics       # Prometheus text (counters + gauges)
```

- Gauges: `atrs_recovery_pending`, `atrs_dispute_open`, `atrs_sla_overdue` (live DB).
- **Chú ý:** counters in-memory → reset khi restart container (Prometheus scrape mới có lịch sử).

## 2. Quy trình vận hành

### 2.1 Mất hết credential → manual review (recovery)
1. Customer submit qua KhachLink → `SUBMITTED` → verify code fail → `AUTOMATED_CHECK` (hoặc risk `REQUIRE_MANUAL_REVIEW` → `PENDING_REVIEW`).
2. Staff: `/admin/recovery` queue (filter PENDING_REVIEW, SLA badge) → `/admin/recovery/{id}` review (risk policy version, devices, timeline, notes).
3. Actions: assign → request-info → escalate/return-to-review → approve/reject.
4. **Approve** → customer nhận notification `recovery.approved` → đăng nhập thiết bị mới → `CompleteAsync` → **token cũ bị revoke** (TokenVersion bump best-effort — nếu fail, revoke thủ công qua Gateway internal).
5. **Reject** → cần ReasonCode (audit); customer được notify.

### 2.2 Dispute adjust fail (ledger)
- Decision approve → `AdjustPointsAsync` fail → decision `FAILED` + case về `UNDER_REVIEW` — **KHÔNG sửa số dư từ ATRS** (luôn qua Gateway `/api/internal/loyalty/points/adjust-customer`, idempotency `dispute:{caseId}:adjust:{decisionId}`).
- Xử lý: xem log `Dispute {CaseId} adjust FAILED` → kiểm tra Gateway/loyalty → investigator propose lại (idempotency key mới tự sinh theo decision mới).

### 2.3 ATRS DB restore
```bash
# Backup
sudo docker exec vanan-atrs-postgres-1 sh -c 'pg_dump -U atrs -d vanan_atrs | gzip' > /tmp/atrs_backup_$(date +%F).sql.gz
# Restore (DB mới hoặc thay thế — tắt atrs trước khi restore production)
sudo docker exec vanan-atrs-postgres-1 psql -U atrs -d vanan_atrs -c "DROP DATABASE IF EXISTS vanan_atrs_restore;"
sudo docker exec vanan-atrs-postgres-1 psql -U atrs -d vanan_atrs -c "CREATE DATABASE vanan_atrs_restore;"
gunzip -c /tmp/atrs_backup_*.sql.gz | sudo docker exec -i vanan-atrs-postgres-1 psql -U atrs -d vanan_atrs_restore
# Verify table count + spot rows trước khi switch
```
- **Đã verify (W6):** dump → restore scratch → 21/21 tables + row counts khớp.
- Migration rollback: mọi migration additive; Down() DropTable sẵn — áp dụng theo thứ tự đảo.

### 2.4 RBAC / grants
- Grant mgmt: SystemAdmin-only qua `/admin/atrs-roles` (ShopERP) hoặc API `/admin/permissions`.
- Break-glass: JWT role SystemAdmin — không thể bị gỡ qua grants (role-based).
- Policy: `/admin/atrs-policy` — propose → approve bởi người khác (maker-checker). **Bootstrap:** v1 của key mới tự EFFECTIVE.

### 2.5 Secrets & RV
- `DevToken__Secret` unset mặc định; chỉ set inline khi mở RV window; remove + verify 404 sau.
- Customer token RV: mint qua DataProtection key ring `/app/keys` (ShopERP container) — KHÔNG qua dev-token.
- Cấm: nhúng secret vào repo/log/card; dùng env inline.

## 3. Troubleshooting nhanh

| Triệu chứng | Nguyên nhân | Xử lý |
|---|---|---|
| `/health` OK nhưng container `unhealthy` | healthcheck thiếu curl trong image (cosmetic, pre-existing W1) | bỏ qua hoặc thêm curl vào Dockerfile |
| Gateway 502 `/api/v1/account-trust/*` | atrs down / `ATRS_REMOTE_HOST` sai | check container + `.env.gateway` |
| Recovery queue 403 dù staff có role | thiếu grant (permission-based) | cấp grant qua `/admin/atrs-roles` |
| SLA breach hàng loạt | policy `recovery.sla_hours` bị hạ / request kẹt | check `/admin/atrs-policy` effective + queue |
| Notification không hiện | container restart reset? (không — notifications ở DB) / subject sai | check `account_notifications` rows |
| Metrics counters về 0 | restart container (in-memory) | expected — scrape Prometheus |

## 4. Deploy & Rollback
- Deploy: push `main` → CD matrix `atrs` → image `:latest` + migrate-on-startup.
- Rollback: tắt flag/redirect Gateway route hoặc pull image tag cũ + `docker compose up -d` (data additive — không xoá).
- Monitor: `/metrics` gauges (pending/open/sla) — spike = alert.