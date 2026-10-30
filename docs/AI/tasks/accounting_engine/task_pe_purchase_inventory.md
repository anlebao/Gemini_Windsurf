# TASK CARD E — PURCHASE DOCS · INVENTORY GL LINK · RECONCILE MỞ RỘNG

> Status: ⏳ PENDING — chờ Phase B+C · **CÓ THỂ TÁCH TRIỂN KHAI RIÊNG (Q8)**
> Master plan: `docs/AI/plans/accounting-engine-master-plan.md` (§4.E) · Gap đóng: **G-H5 · FR-PUR-001/002/005/007 · FR-SUB-003 · FR-RECNC-004 · AT-007/008**
> ⚠️ Phần phức tạp nhất: accounting PG ↔ stock SQLite (cross-DB) — mirror/outbox pattern (precedent `MirrorOrderHttpProvider`, outbox skill `nats-sqlite-deployment-validation`).

---

## 1. MỤC TIÊU

Chuỗi chứng từ mua tối thiểu (nhập mua → phải trả → trả hàng/điều chỉnh) gắn JE đúng bản chất · sổ kho đối chiếu được với GL · không ghi trùng khi đồng bộ nhiều lần.

**Phạm vi thận trọng (SRS §15.4):** KHÔNG dựng module mua-hàng đầy đủ (PO/GRN/hóa đơn mua chuỗi đầy đủ = P1/P2 scope lớn). Phase E = **nền tối thiểu:** chứng từ nhập mua liên kết 331/15x + reconcile kho↔GL. Đầy đủ MH03/06/08-10 defer theo SRS P2.

## 2. THIẾT KẾ

### 2.1. `PurchaseReceipts` (chứng từ nhập mua — entity mới)

- `(Id, TenantId, VoucherNumber[NM-*], SupplierName/SupplierRef, ReceiptDate, TotalAmount, VatAmount, Lines[{ItemName, Qty, UnitPrice}], PayMethod[immediate|credit], Status, Reference)` — minimal doc; lines đơn giản (JSON hoặc owned collection theo precedent).
- Posting qua `AccountMappingRules` (Phase B — `payable→(counter,331)` giờ có counter-account do tenant cấu hình, default seed `156` DN / `611` HTX? — **chốt khi IMPLEMENT với kế toán trưởng; fallback Skipped("no-rule") nếu chưa config**):
  - Nhập chịu: Nợ 156|6xx / Có 331 (purpose `purchase`)
  - Nhập trả ngay: Nợ 156|6xx (+133 nếu VAT đủ điều kiện) / Có 111|112 (purpose `purchase-cash`)
  - Trả hàng: đảo bút toán link gốc (purpose `purchase-return`, ReversalEntryId pattern)
  - Thanh toán NCC: dùng CongNo payable-payment hiện có (331−, JE 331/111 từ Phase A)
- FR-PUR-007: `SourceEventId` = receipt.Id — unique index Phase A cover; sync lặp → skip.

### 2.2. Inventory GL link + reconcile (FR-SUB-003/FR-RECNC-004)

- ShopERP ingredients: `StockMovements` + audit (SQLite) — đã có; **giá trị** tồn = Σ(qty × cost) — cost nguồn: `Ingredient.LastCost`/`AverageCost` hiện có? → audit khi IMPLEMENT (UNKNOWN → ghi rõ).
- `InventoryValuationProvider` (ShopERP nội bộ → HTTP provider → CoreHub, pattern `MirrorOrderHttpProvider`): endpoint `GET /api/shoperp/inventory/valuation?period=` trả tổng GT tồn + movement tổng kỳ.
- `AccountingReconcileService` += `R13` inventory-vs-GL: Σ stock valuation (SQLite mirror) vs TK 15x balance (PG entries) → diff → suggestion. Cross-DB — fail-open (provider null → R13 Skipped, không fail run).
- Reversal/return: purchase-return entry → JE đảo + stock movement out (link Reference).

### 2.3. Công nợ NCC tích hợp

- PurchaseReceipt credit → `CongNoService.CreatePayableAsync` (331+) — reuse; `AllocatedToEntryId` link receipt↔payment khi trả.
- Tuổi nợ/hạn thanh toán: CongNo aging đã có; `DueDate` nullable trên payable entry? — **optional:** thêm `DueDate` trên PurchaseReceipt (không đụng entry immutable) — FR-RPT-006.

## 3. FILES TẠO/SỬA

- Tạo: `PurchaseReceipt` entity (+config+migration) · `PurchaseReceiptService` (+controller nội bộ tối thiểu — backend-first) · `InventoryValuationController`/`Provider` · reconcile `R13` · seeder rules `purchase*`
- Sửa: `AccountMapSeeder` (purchase rules) · `CongNoService` (link) · `AccountingReconcileService` (R13 + provider) · `InternalApiKey` reuse
- Tests: purchase JE per regime · duplicate sync skip · R13 diff detect · trả hàng reversal link

## 4. ACCEPTANCE CRITERIA

- [ ] Nhập chịu → Nợ{map}/Có331 đúng regime; trả ngay → Có 111/112 — AT-007
- [ ] Trả hàng → đảo link gốc, kho/công nợ điều chỉnh — AT-008
- [ ] Sync lặp receipt → không JE/entry trùng — FR-PUR-007
- [ ] R13: chênh lệch kho↔GL được liệt kê + suggestion; provider down → Skipped không fail
- [ ] Payable từ receipt → CongNo ledger đúng đối tượng
- [ ] guard + build + sweep PASS

## 5. TEST CASES

- T1 Purchase-credit JE theo map (DN 156 / HTX 611)
- T2 Purchase-cash + VAT input → 3-line JE cân
- T3 Return → reversal lines + link
- T4 Duplicate receipt event → single posting
- T5 R13 mock: stock 100tr vs GL 95tr → diff 5tr suggestion
- T6 Provider fail → R13 Skipped, run vẫn Pass phần còn lại
