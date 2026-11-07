# EOM Deep-Dive Plan — module detail expansion

> **Status:** ✅ APPROVED 2026-11-07 — owner directive "deep-dive lần lượt tất cả module"
> **Mục đích:** nâng twin từ coverage-level (GAP-016) lên detail-level cho mọi module. Mỗi module = 1 session riêng (context fresh).
> **Deliverable mẫu mỗi module:** swimlane chi tiết + activity metadata đủ 18 fields + RULE/CTRL/RISK + swimlane exceptions + traceability chain code-level + gap findings.

## Thứ tự (theo replication value × risk)

| # | Module | Scope verified anchors | Est entries |
|---|---|---|---|
| D1 | **VALCN** (goal — làm đầu) | CommunityOrderService, CommunityFundService, CommunityAdminService, CommerceModeService, CommunityFundSpendRecord, 3 controllers | ~8-10 ACTV |
| D2 | **Inventory** | InventoryService, count configs, valuation, stock movements ↔ voucher link | ~8-10 ACTV |
| D3 | **Accounting depth** | PeriodClosing, regime resolver, HKDBooks, TrialBalance, financial statements by regime, reconcile | ~10-12 ACTV |
| D4 | **Finance Intelligence** | BreakEvenAnalysis, BusinessProfile, advisory boundary | ~4-6 ACTV |
| D5 | **Booking/appointments** | PublicBooking + TenantBooking controllers | ~5-7 |
| D6 | **Notifications** | NotificationCenterService + hub notifier | ~3-4 |
| D7 | **Community/Growth/Campaigns/Missions** | controllers verified, partial | ~6-8 |
| D8 | **CRM** | partial (GAP-001) | ~3-5 |
| D9 | **Drift backlog processing** | 216 findings → chấp nhận/whitelist/register | varies |

## Quy tắc mỗi session

1. Chọn 1 module → grep đủ evidence (service+controller+domain+test) trước khi viết
2. Seed registries (PROC L3 detail + ACTV 18-field + RULE/CTRL/RISK) → `python scripts/validate-twin.py` phải 0 errors
3. Viết swimlane `03-processes/swimlane-diagrams/{module}.md` + exceptions/recovery nếu critical
4. Update `07-as-is` coverage row + GAP-016 progress
5. Commit batch per module (docs-only)

## Track song song (vẫn mở)

- Owner questions Q1–Q5 (legal responsibility, governance drift, crawler)
- CI wiring proposal cho validate-twin + drift --fail-on-drift
- `docs/twin-portal-site/` deploy + nginx include khi user chọn gating option
