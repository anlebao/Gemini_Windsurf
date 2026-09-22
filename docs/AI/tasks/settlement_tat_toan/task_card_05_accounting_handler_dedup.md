# Task Card TC-05: SimpleAccountingEventHandler — revenue ghi trùng + không idempotent

> **Status:** ⬜ PENDING (chờ decision Q2: retire hay fix)
> **Severity:** P1 — sổ sách (revenue PG bị nhân đôi + sai gross)
> **Findings:** B2, B3, B6
> **Files:** `3_CoreHub/Services/Events/SimpleAccountingEventHandler.cs`, `3_CoreHub/Services/OrderService.cs` (GenerateAccountingEntriesAsync ~L163-406), `2_Gateway/Program.cs` (~L638-642)

## Root cause (verified)

Mỗi `OrderCompleted` event, handler tạo **2 Revenue AccountingEntry** cùng `TotalAmount`:
1. `accountingService.CreateEntryAsync` → `CreateRevenue` (no accountCode, correlationId=orderId).
2. `hkdService.RecordRevenueAsync` → `CreateRevenue` lần 2 (no accountCode, no correlationId).

→ Revenue trên PG = **2× TotalAmount** mỗi đơn; amount là **gross** (gồm VAT + phí ship) trong khi path đúng (`OrderService.GenerateAccountingEntriesAsync` trên ShopERP) ghi net 511 + 3331 + 632.

Không idempotent: `CreateEntryAsync` (L74-106 của AccountingEntryService) **không** gọi `CheckDuplicateEntryAsync`, không check CorrelationId; `message.Ack()` chỉ khi thành công → NATS redelivery = +2 entries mỗi lần. Subscribe 2 subject (`order.completed` + `ordercompleted`) → nếu publisher nào bắn cả hai → double.

Timing: entry tạo lúc order **completed**, không phải lúc thực thu → vi phạm cash-basis mà comment OrderService tuyên bố.

## Options (Q2 — cần user duyệt)

**Option A (khuyến nghị) — Retire handler:** `GenerateAccountingEntriesAsync` (payment-confirm path) đã làm đúng + đầy đủ hơn (net revenue, VAT split, COGS, Reseller 3-bookset, JournalEntry). Handler legacy chỉ gây nhiễu. Action: bỏ `AddHostedService<SimpleAccountingEventHandler>` (hoặc gate config default OFF) + **cleanup plan** cho entries trùng đã tồn tại trên PG (reversal entries theo CorrelationId, không xóa — immutability rule).

**Option B — Fix handler:** giữ làm fallback cho đơn không qua payment path. Cần: (1) dedup `ExistsByCorrelationIdAsync(orderId)` trước khi tạo; (2) chỉ tạo 1 entry (bỏ double call); (3) amount = net (`SubTotal − DiscountAmount`) hoặc skip nếu order đã có entries từ path A; (4) bỏ legacy subject hoặc dedup EventId; (5) qua period-closed guard.

## Data cleanup (bắt buộc dù chọn option nào)

- Query PG: `AccountingEntries` group by CorrelationId → đơn có ≥2 Revenue entries cùng amount = bị trùng.
- Đảo bằng `CreateReversal` (append-only), KHÔNG delete/update.
- Report số liệu trước khi đảo (user duyệt danh sách).

## Tests

- Option B: redelivery cùng event → không thêm entry; 1 event = 1 entry; entry amount = net; closed period → reject/log.
- Option A: handler không đăng ký → không entry mới; order.completed không còn consumer (log NATS no-subscriber OK).

## Acceptance

- [ ] 1 order completed = đúng 1 bộ bút toán duy nhất trên mỗi sổ.
- [ ] Không còn gross-TotalAmount revenue entry mới.
- [ ] Dữ liệu trùng cũ được đảo (reversal), có report.
- [ ] Build 0 errors · guard-check PASS · Core.Tests PASS.
