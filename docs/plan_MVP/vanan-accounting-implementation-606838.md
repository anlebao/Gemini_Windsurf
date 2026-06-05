# Vạn An Accounting — Implementation Plan (Sprints 1-3)

Thực hiện 3 sprint liên tiếp để hoàn thiện MVP: UI nhập liệu kế toán → Period Closing + Audit Trail → E-Invoice Multi-Provider.

---

## Sprint 1 — Phase 2.6: Frontend Accounting Module (2 tuần)
**Target:** ShopERP có đủ UI để kế toán viên nhập liệu, xem sổ, kiểm tra số dư.  
**Location:** `5_WebApps/ShopERP/Components/Pages/Accounting/`

### Week 1: Manual Entry + Transaction History

#### Task 1.1 — Revenue/Expense Entry Forms (Phase 2.6.1)
- Tạo `RevenueEntryForm.razor` — form nhập doanh thu với account selection dropdown
- Tạo `ExpenseEntryForm.razor` — form nhập chi phí với vendor info
- Kết nối `IAccountingEntryService` (đã có trong CoreHub)
- Date picker với period validation (chặn nhập ngày ngoài kỳ kế toán)
- Description auto-complete từ journal templates

#### Task 1.2 — Transaction History View (Phase 2.6.2)
- Tạo `TransactionHistory.razor` — danh sách entries có search/filter
- Filter theo: period, account type, amount range, description
- Transaction detail modal với VanAModal component
- Export to Excel button (dùng library ClosedXML hoặc EPPlus)

### Week 2: Balance Display + Validation + Page routing

#### Task 1.3 — Account Balance Dashboard (Phase 2.6.3)
- Tạo `AccountBalance.razor` — balance per account type
- Period comparison (tháng này vs tháng trước)
- Balance trend mini-charts (dùng VanAMetricsCard component)
- Alert khi balance âm hoặc bất thường

#### Task 1.4 — Frontend Validation (Phase 2.6.4)
- Duplicate transaction detection (check same amount + date + account trong 5 phút)
- Balance constraint check trước khi submit
- Account code validation against VN chart of accounts
- Real-time error feedback với VanAAlert component

#### Task 1.5 — Navigation + Routing
- Thêm Accounting menu vào ShopERP sidebar navigation
- Routes: `/accounting`, `/accounting/revenue`, `/accounting/expenses`, `/accounting/history`, `/accounting/balance`
- UI Platform components: VanAButton, VanACard, VanALayout, VanAAlert, VanAModal

---

## Sprint 2 — Phase 2.9.3 + 2.9.4: Period Closing & Audit Trail (1 tuần)
**Target:** Kế toán viên có thể đóng sổ tháng/năm và hệ thống ghi lại đầy đủ audit log.

### Task 2.1 — Period Closing Wizard (Phase 2.9.3)
**Backend:** `3_CoreHub/Services/`
- Tạo `IPeriodClosingService` + `PeriodClosingService.cs`
- Logic: pre-closing validation checks (balance reconciliation, missing entries)
- Automated closing procedures (calculate closing entries)
- Reopen capability với reversal pattern (AccountingEntry immutable — dùng Reversal Entry)

**Frontend:** `ShopERP/Components/Pages/Accounting/PeriodClosing.razor`
- Step-by-step wizard UI (3 bước: Validate → Review → Close)
- Closing checklist display
- Closing report generation (PDF-ready format)
- Confirmation dialog với VanAModal

### Task 2.2 — Audit Trail System (Phase 2.9.4)
**Backend:** `3_CoreHub/Services/`
- Tạo `IAuditTrailService` + `AuditTrailService.cs`
- Ghi log mọi action: create, view, export, login/logout
- 5-year retention policy enforcement
- Queryable audit log với filter theo user, action, date

**Frontend:** `ShopERP/Components/Pages/Accounting/AuditTrail.razor`
- Audit log table với VanAOrderTable component
- Filter theo user/action/date range
- Export audit report

---

## Sprint 3 — Phase 5: E-Invoice Multi-Provider Integration (2 tuần)
**Design Reference:** `docs/design/EInvoice multi provider integration.md`  
**UI Reference:** `docs/design/EInvoice UI Layout Design.md`

### Week 3 (Days 1-7): Backend Implementation

#### Task 3.1 — Domain Models (Days 1-2)
**Location:** `1_Shared/Domain.cs`
- Add `Invoice`, `InvoiceAggregate` record (immutable, append-only)
- Add `InvoiceProvider`, `SubmitAttempt`, `ProviderStatus` value objects
- Add domain events: `InvoiceSubmitted`, `InvoiceConfirmed`, `InvoiceRejected`
- State machine: Draft → Submitted → Confirmed / Rejected / Failed

#### Task 3.2 — Provider Interfaces & Infrastructure (Days 3-4)
**Location:** `3_CoreHub/Services/EInvoice/`
- Tạo `IEInvoiceProvider` interface
- Tạo `ViettelProvider.cs`, `BKAVProvider.cs`, `MISAProvider.cs` (stub implementations)
- Tạo `IProviderRegistry` + `ProviderRegistry.cs` (provider selection logic)
- Circuit breaker pattern với `ProviderCircuitBreaker.cs`
- Outbox pattern: `InvoiceOutboxProcessor.cs` (atomic Invoice + Outbox)

#### Task 3.3 — Business Logic & Services (Days 5-7)
**Location:** `3_CoreHub/Services/EInvoice/`
- `IEInvoiceService` + `EInvoiceService.cs` (orchestrator)
- Idempotency: duplicate prevention via `InvoiceIdempotencyKey`
- `ProviderFailoverService.cs` — auto-switch khi provider lỗi
- `RevenueClassificationService.cs` — phân loại HKD revenue group (≤500M, >500M-1B, etc.)
- TT152-2025 compliance validation integration

#### Task 3.4 — API Layer (Days 6-7, parallel)
**Location:** `2_Gateway/Controllers/`
- `EInvoicesController.cs` — POST /invoices, GET /invoices/{id}, POST /invoices/{id}/submit
- `WebhooksController.cs` — nhận callback từ providers (idempotent handling)
- `ProvidersController.cs` — GET /providers, GET /providers/{id}/health

### Week 4 (Days 8-14): UI Implementation

#### Task 3.5 — E-Invoice Dashboard (Days 8-9)
**Location:** `5_WebApps/ShopERP/Components/Pages/EInvoice/`
- `EInvoiceDashboard.razor` — provider status cards, recent invoices, stats
- Real-time provider health indicators (SignalR updates)
- Quick action buttons: issue invoice, check status

#### Task 3.6 — Provider Management (Days 10-11)
- `ProviderManagement.razor` — list all providers với status
- `ProviderConfiguration.razor` — configure credentials per provider
- `HealthMonitoring.razor` — health metrics + circuit breaker status

#### Task 3.7 — Invoice Management (Days 12-13)
- `InvoiceManagement.razor` — invoice list với filter/search
- Invoice detail view + status timeline
- Retry failed submissions
- `AlertManagement.razor` — system alerts + notifications

#### Task 3.8 — Integration & Testing (Day 14)
- Wire E-Invoice routes vào ShopERP navigation
- Integration test với mock providers
- TT152-2025 compliance validation test
- Build validation: `dotnet build VanAn.sln`

---

## Architectural Constraints (NON-NEGOTIABLE)

- **Domain Layer:** Tất cả new entities (Invoice, InvoiceAggregate, etc.) phải vào `1_Shared/Domain.cs`
- **AccountingEntry:** Immutable — đóng sổ dùng Reversal Entry pattern
- **Multi-tenancy:** Mọi query phải filter theo `TenantId`
- **UI Platform:** Dùng `VanAButton`, `VanACard`, `VanAAlert`, `VanAModal`, `VanALayout` — KHÔNG custom HTML/CSS
- **Build gate:** `guard-check.ps1` + `dotnet build VanAn.sln` phải PASS sau mỗi task

---

## Timeline Summary

| Sprint | Phase | Duration | Output |
|--------|-------|----------|--------|
| Sprint 1 | 2.6 Frontend Accounting | 2 tuần | Kế toán viên nhập liệu được |
| Sprint 2 | 2.9.3 + 2.9.4 | 1 tuần | Period Closing + Audit Trail |
| Sprint 3 | Phase 5 E-Invoice | 2 tuần | E-Invoice multi-provider live |
| **Total** | | **5 tuần** | **MVP production-ready** |

## Deliverable sau mỗi Sprint

- Sprint 1 done: Kế toán viên nhập/xem/export dữ liệu qua ShopERP UI
- Sprint 2 done: Đóng sổ tháng + audit log 5 năm hoạt động
- Sprint 3 done: E-Invoice submit qua ≥3 providers, TT152-2025 compliant
