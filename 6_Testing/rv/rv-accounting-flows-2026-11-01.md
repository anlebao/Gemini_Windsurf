# RV REPORT — 4 luồng kế toán + double revenue (2026-11-01)

- **Script:** `6_Testing/rv/rv-accounting-flows.sh` (read-only, chỉ SELECT)
- **VPS:** vanan-gateway (asia-southeast1-a) · PG `vanan-postgres-1` / db `VanAnCoreHub` / user `vanan_admin`
- **Kết quả:** **PASS 16 / FAIL 0 / INFO 10**

## Dữ liệu nền

| Bảng | Rows |
|---|---|
| JournalEntries | 240 |
| JournalEntryLine | 502 |
| AccountingEntries | 455 |
| Orders | 177 |
| EntryPostingStatuses | 451 |
| PurchaseReceipts | 1 |

**ReferenceType:** ManualEntry 172 · Order 35 · Expense 9 · Adjustment 6 · COGS 5 · Sale 5 · PeriodClosing 3 · OpeningBalance 2 · ManualReversal 2 · PurchaseReceipt 1
**PostingPurpose:** original 177 (backfill pre-Phase-A) · revenue 30 · cogs 20 · closing 3 · manual-expense 3 · opening-balance 2 · vat 2 · reversal 2 · purchase-cash 1

## Kết quả theo luồng

| Luồng | Check | Kết quả |
|---|---|---|
| 1. Thu/Chi forms | entries có VoucherNumber | PASS (3 entries — NM/PT-CN/PC-CN) |
| | posting Failed (Status=3) | **PASS — 0** |
| | Posted/Skipped/Pending | INFO 220/76/155 |
| 2. Phiếu Nhập Mua | 1 receipt Status=1 · 1 JE purchase-cash · reversal JE=0 | INFO ✓ |
| 3. Order→JE | dup JE cùng (Tenant,RefType,RefId,Purpose) — RefId non-null | **PASS — 0** |
| | refs có cả JE 'Order' lẫn 'ManualEntry' (pattern #195) | **PASS — 0** |
| | orders có >1 JE purpose=revenue | **PASS — 0** |
| | JE revenue 5xx > order TotalAmount | **PASS — 0** |
| | orders có >1 JE deposit-offset | **PASS — 0** |
| 4. Số dư đầu kỳ | 2 OpeningBalance JEs, đều cân đối | **PASS — 0 lệch** |
| Tổng | JE không cân đối (ΣNợ≠ΣCó) | **PASS — 0/240** |

## Kết luận

1. **Không còn double revenue** — cả 4 pattern (#195 Order+ManualEntry cùng ref, dup purpose, revenue JE>1/order, 5xx>order total) đều = 0. Data fix #195 giữ vững.
2. **Posting pipeline healthy** — 0 Failed; 76 Skipped (HKD/non-journalable by design); **155 Pending = artifact backfill migration `20261030152647` (cùng timestamp 16:35:32 — entries pre-Phase-A, không phải lỗi)**.
3. **Mọi JE cân đối** — 0/240 lệch (gồm OpeningBalance).
4. **Observations (không phải lỗi):**
   - CTGS `JournalVoucher` JEs = 0 — chưa có user submit phiếu đầu tiên (L5 chờ user).
   - `VoucherSequences` chỉ có NM/PT-CN/PC-CN — sequences PT/PC/CTGS tạo khi dùng lần đầu; **chỉ 3 entries có VoucherNumber → số phiếu trên in 01-TT/02-TT có thể trống nếu form thu/chi chưa gọi NextNumberAsync** — đáng verify trên phiếu thật đầu tiên.
   - `Vouchers` table = 0 rows (voucher number nằm trên entry.VoucherNumber — by design).
   - `jnl_order_dup_backup_20261029*` — backup tables từ data fix #195 vẫn còn (giữ theo policy).
