# TASK CARD B — COA PER-TENANT + ACCOUNT MAP (B1 — data-driven TK mapping, zero behavior change)

> Status: ⏳ PENDING — chờ Phase A
> Master plan: `docs/AI/plans/accounting-engine-master-plan.md` (§4.B) · Gap đóng: **G-H1 (B1) · G-H2** + fix GL missing VAT JE
> ⚠️ Domain changes (`TenantAccountChart` entity + `AccountChartEntity` fields) → **Gate 5 approval**
> **B2 (full rule engine conditions/formula/preview): DEFER — chỉ làm khi tenant thật cần (R5)**

---

## 1. MỤC TIÊU

COA độc lập per-tenant (override layer trên global chart) + mapping sự kiện→TK Nợ/Có bằng **data** (không hardcode), seed = semantics hiện tại → deploy không đổi hành vi. Bonus: VAT 3331 có JE (GL completeness — phát hiện trong phản biện).

## 2. THIẾT KẾ

### 2.1. `TenantAccountChart` (M4 — bảng mới)

- Fields: `TenantId · AccountCode · AccountName · Type? · IsNormalCredit? · ParentCode? · IsActive · EffectiveFrom · Source(Override|Custom)`.
- `AccountChartEntity += ParentCode?(string) · EffectiveFrom?(DateTime)`.
- `IAccountChartService` mở rộng: resolve tenant chart = global standard chart + tenant rows (Override ghi đè, Custom thêm TK chi tiết; ParentCode bắt buộc tồn tại global hoặc tenant).
- FR-COA-005: deactivate-only — service chặn deactivate TK đã phát sinh? Không — chặn **xóa**; deactivate TK có phát sinh vẫn cho (dừng dùng mới). Validation: TK chi tiết phải có parent hợp lệ.
- FR-COA-006: `EffectiveFrom` + audit log khi thêm/sửa/deactivate.

### 2.2. `AccountMappingRules` (M5 — bảng mới)

| Cột | Ý nghĩa |
|---|---|
| `TenantId` | NULL = global default theo `Regime`; set = tenant override |
| `Regime` | TT99/TT133/TT58/TT71/TT152-nullable — scope khi TenantId null |
| `EventCode` | `manual-receipt` `manual-expense` `deposit-offset` `order-revenue` `order-vat` `order-cogs` `receivable` `receivable-payment` `payable` `payable-payment` `transfer` `closing-*` … |
| `DebitAccount`/`CreditAccount` | TK; `{src}` = placeholder account hint từ entry |
| `AmountKind` | `amount`/`net`/`vat`/`cogs` — semantic số tiền |
| `EffectiveFrom`/`Version`/`IsActive` | versioning (FR-RULE-003/007 nền) |

- `IAccountMapResolver.ResolveAsync(eventCode, tenantId, accountHint?) → (debit,credit)?` — precedence **tenant > regime > builtin hardcode fallback**; cache per-tenant (60s, invalidation khi đổi rules).
- `null` → PostingStatus=Skipped("no-rule") — observable, không đoán (SRS §18.4.1.5).

### 2.3. Seed = hardcode hiện tại (R5 — T-parity bắt buộc)

`manual-receipt→(111,{src})` · `manual-expense→({src},111)` · `deposit-offset→(3387,{src})` · `order-revenue→(111,511)` + HTX `(111,512-internal/511)` · `order-cogs→(632,111)` + HTX `(611|612,111)` · **`order-vat→(111,3331)` — MỚI, fix GL thiếu nợ thuế** · `receivable→(131,511|512)` · `receivable-payment→(111,131)` · `payable-payment→(331,111)` · `payable→(NULL,331)` → Skipped cho đến khi tenant cấu hình.

### 2.4. Switch call sites

- `ManualEntryJournalBridge.ResolveAccounts` → resolver (regime lookup qua `VasFeatureFlagService`/tenant — pattern `IsHtxSellingTenantAsync`).
- `OrderService.GenerateAccountingEntriesAsync`: hardcode `"511"/"632"/"3331"` → event codes `order-revenue`/`order-vat`/`order-cogs` (hint = sector/HTX-internal cho resolver).
- Feature flag `AccountMappingEnabled` per tenant: OFF → code path cũ (dự phòng); ON → resolver. Pilot 1 tenant → verify R-checks → mở rộng.
- JE ghi `RuleVersion` (field nullable trên JournalEntry — FR-RULE-007 nền, map vào PostingPurpose-adjacent column hoặc cột riêng `MappingVersion`).

## 3. FILES TẠO/SỬA

- Tạo: `1_Shared/Domain/TenantAccountChart.cs`?? — **quyết định implement:** TenantAccountChart là persistence-entity kiểu `AccountChartEntity` (Infrastructure.Entities — KHÔNG domain aggregate, precedent AccountChartEntity) tránh chạm Domain nặng; `AccountMappingRule` tương tự Infrastructure entity hoặc Domain nếu có logic — chốt khi IMPLEMENT (ưu tiên Infrastructure entity nếu chỉ là data). `3_CoreHub/Services/AccountMapResolver.cs` + `IAccountMapResolver.cs` · `Infrastructure/Entities/TenantAccountChartEntity.cs` + `AccountMappingRuleEntity.cs` · configurations · seeder `AccountMapSeeder` · migrations.
- Sửa: `AccountChartEntity` (ParentCode/EffectiveFrom) · `AccountChartService` (tenant resolve + deactivate rules) · `ManualEntryJournalBridge` + `OrderService` + `JournalEntry` (MappingVersion?) · `IAccountChartService`.
- Tests: resolver precedence/parity · tenant chart override · seed coverage mọi event-code × regime · VAT JE.

## 4. ACCEPTANCE CRITERIA

- [ ] **T-parity:** cùng input → resolver trả cặp TK giống hardcode hiện tại cho mọi event đã seed (zero behavior change OFF→ON)
- [ ] VAT 3331 entry → JE Nợ111/Có3331 (GL có nợ thuế — B01 đúng dần)
- [ ] Tenant override rule thắng global; regime seed đúng HTX (512/611/612)
- [ ] Tenant thêm TK chi tiết (parent hợp lệ) + deactivate; xóa bị chặn; EffectiveFrom + audit
- [ ] Missing rule → Skipped observable, không fallback tùy tiện — ACC12-013
- [ ] JE lưu MappingVersion — rule đổi không hồi tố — ACC12-016 (nền)
- [ ] guard + build + sweep PASS; pilot flag 1 tenant production

## 5. TEST CASES (tối thiểu)

- T1 Parity matrix: mọi (event,regime) → expected cặp TK = hardcode cũ
- T2 Precedence: tenant > regime > fallback; cache invalidate
- T3 VAT JE: order có VAT → JE (111,3331) — trước đây không có
- T4 TenantAccountChart: thêm chi tiết OK · parent sai → reject · deactivate TK có phát sinh → allowed-warn · delete → reject
- T5 HTX receivable → 512 nội bộ qua hint
- T6 payable-create → Skipped("no-rule") cho đến khi tenant config
