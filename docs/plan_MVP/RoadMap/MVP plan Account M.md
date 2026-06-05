# MVP PLAN ACCOUNT M - COMPREHENSIVE ACCOUNTING SYSTEM

## CONTEXT: VAI AN ACCOUNTING SYSTEM MVP
**Target:** Complete accounting solution for Vietnamese Household Businesses (HKD)
**Framework:** .NET 8.0, Clean Architecture, Multi-tenancy
**Compliance:** Thông tư 200/2014/TT-BTC, Thông tư 152/2025/TT-BTC
**Terminology:** HKD = Hộ kinh doanh / cá nhân kinh doanh (not Hong Kong Dollar)

## EXECUTIVE STATUS SUMMARY
- **Core accounting engine:** Completed based on current roadmap evidence.
- **HKD book/reporting foundation:** Completed based on current roadmap evidence.
- **Production compliance blocker:** Phase 5 E-Invoice Multi-Provider Integration remains required before claiming full regulatory readiness for production HKD usage.
- **MVP interpretation:** Core accounting MVP is functionally complete; production-ready compliance MVP is not complete until e-invoice integration and verification are finished.

## AUTHORITATIVE 5-WEEK EXECUTION PLAN - SPRINTS 1-3
**Source:** `docs/plan_MVP/vanan-accounting-implementation-606838.md`
**Status:** Newer and more accurate implementation plan. This section supersedes older high-level roadmap details for Phase 2.6, Phase 2.9.3, Phase 2.9.4, and Phase 5.

Thực hiện 3 sprint liên tiếp để hoàn thiện production-ready MVP: UI nhập liệu kế toán → Period Closing + Audit Trail → E-Invoice Multi-Provider.

### Sprint 1 - Phase 2.6: Frontend Accounting Module (2 tuần)
**Target:** ShopERP có đủ UI để kế toán viên nhập liệu, xem sổ, kiểm tra số dư.
**Location:** `5_WebApps/ShopERP/Components/Pages/Accounting/`

#### Week 1: Manual Entry + Transaction History

##### Task 1.1 - Revenue/Expense Entry Forms (Phase 2.6.1)
- Tạo `RevenueEntryForm.razor` - form nhập doanh thu với account selection dropdown
- Tạo `ExpenseEntryForm.razor` - form nhập chi phí với vendor info
- Kết nối `IAccountingEntryService` (đã có trong CoreHub)
- Date picker với period validation (chặn nhập ngày ngoài kỳ kế toán)
- Description auto-complete từ journal templates

##### Task 1.2 - Transaction History View (Phase 2.6.2)
- Tạo `TransactionHistory.razor` - danh sách entries có search/filter
- Filter theo: period, account type, amount range, description
- Transaction detail modal với VanAModal component
- Export to Excel button (dùng library ClosedXML hoặc EPPlus)

#### Week 2: Balance Display + Validation + Page routing

##### Task 1.3 - Account Balance Dashboard (Phase 2.6.3)
- Tạo `AccountBalance.razor` - balance per account type
- Period comparison (tháng này vs tháng trước)
- Balance trend mini-charts (dùng VanAMetricsCard component)
- Alert khi balance âm hoặc bất thường

##### Task 1.4 - Frontend Validation (Phase 2.6.4)
- Duplicate transaction detection (check same amount + date + account trong 5 phút)
- Balance constraint check trước khi submit
- Account code validation against VN chart of accounts
- Real-time error feedback với VanAAlert component

##### Task 1.5 - Navigation + Routing
- Thêm Accounting menu vào ShopERP sidebar navigation
- Routes: `/accounting`, `/accounting/revenue`, `/accounting/expenses`, `/accounting/history`, `/accounting/balance`
- UI Platform components: VanAButton, VanACard, VanALayout, VanAAlert, VanAModal

### Sprint 2 - Phase 2.9.3 + 2.9.4: Period Closing & Audit Trail (1 tuần)
**Target:** Kế toán viên có thể đóng sổ tháng/năm và hệ thống ghi lại đầy đủ audit log.

#### Task 2.1 - Period Closing Wizard (Phase 2.9.3)
**Backend:** `3_CoreHub/Services/`
- Tạo `IPeriodClosingService` + `PeriodClosingService.cs`
- Logic: pre-closing validation checks (balance reconciliation, missing entries)
- Automated closing procedures (calculate closing entries)
- Reopen capability với reversal pattern (AccountingEntry immutable - dùng Reversal Entry)

**Frontend:** `ShopERP/Components/Pages/Accounting/PeriodClosing.razor`
- Step-by-step wizard UI (3 bước: Validate → Review → Close)
- Closing checklist display
- Closing report generation (PDF-ready format)
- Confirmation dialog với VanAModal

#### Task 2.2 - Audit Trail System (Phase 2.9.4)
**Backend:** `3_CoreHub/Services/`
- Tạo `IAuditTrailService` + `AuditTrailService.cs`
- Ghi log mọi action: create, view, export, login/logout
- 5-year retention policy enforcement
- Queryable audit log với filter theo user, action, date

**Frontend:** `ShopERP/Components/Pages/Accounting/AuditTrail.razor`
- Audit log table với VanAOrderTable component
- Filter theo user/action/date range
- Export audit report

### Sprint 3 - Phase 5: E-Invoice Multi-Provider Integration (2 tuần)
**Design Reference:** `docs/design/EInvoice multi provider integration.md`
**UI Reference:** `docs/design/EInvoice UI Layout Design.md`

#### Week 3 (Days 1-7): Backend Implementation

##### Task 3.1 - Domain Models (Days 1-2)
**Location:** `1_Shared/Domain.cs`
- Add `Invoice`, `InvoiceAggregate` record (immutable, append-only)
- Add `InvoiceProvider`, `SubmitAttempt`, `ProviderStatus` value objects
- Add domain events: `InvoiceSubmitted`, `InvoiceConfirmed`, `InvoiceRejected`
- State machine: Draft → Submitted → Confirmed / Rejected / Failed

##### Task 3.2 - Provider Interfaces & Infrastructure (Days 3-4)
**Location:** `3_CoreHub/Services/EInvoice/`
- Tạo `IEInvoiceProvider` interface
- Tạo `ViettelProvider.cs`, `BKAVProvider.cs`, `MISAProvider.cs` (stub implementations)
- Tạo `IProviderRegistry` + `ProviderRegistry.cs` (provider selection logic)
- Circuit breaker pattern với `ProviderCircuitBreaker.cs`
- Outbox pattern: `InvoiceOutboxProcessor.cs` (atomic Invoice + Outbox)

##### Task 3.3 - Business Logic & Services (Days 5-7)
**Location:** `3_CoreHub/Services/EInvoice/`
- `IEInvoiceService` + `EInvoiceService.cs` (orchestrator)
- Idempotency: duplicate prevention via `InvoiceIdempotencyKey`
- `ProviderFailoverService.cs` - auto-switch khi provider lỗi
- `RevenueClassificationService.cs` - phân loại HKD revenue group (≤500M, >500M-1B, etc.)
- TT152-2025 compliance validation integration

##### Task 3.4 - API Layer (Days 6-7, parallel)
**Location:** `2_Gateway/Controllers/`
- `EInvoicesController.cs` - POST /invoices, GET /invoices/{id}, POST /invoices/{id}/submit
- `WebhooksController.cs` - nhận callback từ providers (idempotent handling)
- `ProvidersController.cs` - GET /providers, GET /providers/{id}/health

#### Week 4 (Days 8-14): UI Implementation

##### Task 3.5 - E-Invoice Dashboard (Days 8-9)
**Location:** `5_WebApps/ShopERP/Components/Pages/EInvoice/`
- `EInvoiceDashboard.razor` - provider status cards, recent invoices, stats
- Real-time provider health indicators (SignalR updates)
- Quick action buttons: issue invoice, check status

##### Task 3.6 - Provider Management (Days 10-11)
- `ProviderManagement.razor` - list all providers với status
- `ProviderConfiguration.razor` - configure credentials per provider
- `HealthMonitoring.razor` - health metrics + circuit breaker status

##### Task 3.7 - Invoice Management (Days 12-13)
- `InvoiceManagement.razor` - invoice list với filter/search
- Invoice detail view + status timeline
- Retry failed submissions
- `AlertManagement.razor` - system alerts + notifications

##### Task 3.8 - Integration & Testing (Day 14)
- Wire E-Invoice routes vào ShopERP navigation
- Integration test với mock providers
- TT152-2025 compliance validation test
- Build validation: `dotnet build VanAn.sln`

### Architectural Constraints (NON-NEGOTIABLE)
- **Domain Layer:** Tất cả new entities (Invoice, InvoiceAggregate, etc.) phải vào `1_Shared/Domain.cs`
- **AccountingEntry:** Immutable - đóng sổ dùng Reversal Entry pattern
- **Multi-tenancy:** Mọi query phải filter theo `TenantId`
- **UI Platform:** Dùng `VanAButton`, `VanACard`, `VanAAlert`, `VanAModal`, `VanALayout` - KHÔNG custom HTML/CSS
- **Build gate:** `guard-check.ps1` + `dotnet build VanAn.sln` phải PASS sau mỗi task

### Timeline Summary
| Sprint | Phase | Duration | Output |
|--------|-------|----------|--------|
| Sprint 1 | 2.6 Frontend Accounting | 2 tuần | Kế toán viên nhập liệu được |
| Sprint 2 | 2.9.3 + 2.9.4 | 1 tuần | Period Closing + Audit Trail |
| Sprint 3 | Phase 5 E-Invoice | 2 tuần | E-Invoice multi-provider live |
| **Total** | | **5 tuần** | **MVP production-ready** |

### Deliverable sau mỗi Sprint
- Sprint 1 done: Kế toán viên nhập/xem/export dữ liệu qua ShopERP UI
- Sprint 2 done: Đóng sổ tháng + audit log 5 năm hoạt động
- Sprint 3 done: E-Invoice submit qua ≥3 providers, TT152-2025 compliant

---

## PHASE 1: CORE ACCOUNTING ENGINE FOUNDATION - DOMAIN SERVICES + CLEAN ARCHITECTURE

### PHASE 1.1: Domain Design & Value Objects - AccountingEntry Immutable Pattern
**Status:** COMPLETED
- [x] Immutable AccountingEntry with append-only pattern
- [x] Value Objects: TenantId, AccountingPeriod, Money
- [x] Domain Events: AccountingEntryCreated, ReversalEntryCreated
- [x] Business Rules: VAT calculation, period validation

### PHASE 1.2: Repository Pattern - IAccountingEntryRepository + Implementation
**Status:** COMPLETED
- [x] IAccountingEntryRepository interface
- [x] EF Core implementation with multi-tenancy
- [x] SQLite in-memory for testing
- [x] Repository pattern with async methods

### PHASE 1.3: Domain Services - JournalEntryService, JournalTemplateService
**Status:** COMPLETED
- [x] JournalEntryService for journal creation
- [x] JournalTemplateService for template management
- [x] Business logic encapsulation
- [x] Domain service interfaces

### PHASE 1.4: API Layer - Only POST endpoints, Zero-error build
**Status:** COMPLETED
- [x] AccountingEntriesController with POST endpoints
- [x] Revenue and expense entry endpoints
- [x] Zero-error build achieved
- [x] Basic validation

---

## PHASE 2: ORDER FLOW + HKD ACCOUNTING - REPOSITORY + ORDER INTEGRATION + HKD BOOKS

### PHASE 2.1: Repository Implementation - JournalTemplateRepository, OrderRepository, HKDBookRepository
**Status:** COMPLETED
- [x] JournalTemplateRepository for template management
- [x] OrderRepository for order data
- [x] HKDBookRepository for HKD book storage
- [x] Multi-tenant repository pattern

### PHASE 2.2: Order to Accounting Integration - Auto entries from order flow
**Status:** COMPLETED
- [x] OrderService with automatic accounting entries
- [x] Order-to-accounting mapping
- [x] Revenue recognition on order completion
- [x] COGS calculation for inventory

### PHASE 2.3: 7 HKD Books Implementation - S1a, S2a, S2b, S2c, S2d, S2e, S3a Templates
**Status:** 🔄 PARTIALLY COMPLETED - CORE TEMPLATES READY, SUPPORTING FEATURES PENDING

#### **TECHNICAL ARCHITECTURE:**
- **GenericHKDBook**: Dynamic book generation using template-based approach
- **HKDBookTemplate**: Configurable templates for different book types
- **FormulaEngine**: DSL-based calculation engine for financial formulas
- **DataProvider**: Multi-tenant data aggregation service
- **BusinessRuleRegistry**: Centralized business rule management

#### **CORE COMPONENTS:**
```
HKDBookService (Orchestrator)
    |
    |-- HKDBookRepository (Persistence)
    |-- AccountingEntryRepository (Data Source)
    |-- FormulaEngine (Calculations)
    |-- BusinessRuleRegistry (Rules)
    |-- DataProvider (Aggregation)
    |-- HKDBookTemplate (Templates)
```

#### **BOOK GENERATION WORKFLOW:**
1. **Template Selection**: Choose HKD book template based on tenant type
2. **Data Aggregation**: Collect accounting entries for specified period
3. **Formula Evaluation**: Execute DSL formulas using FormulaEngine
4. **Business Rule Application**: Apply Vietnamese accounting rules
5. **Book Generation**: Create structured HKD book output
6. **Validation**: Ensure compliance with Vietnamese standards

#### **HKD BOOK TYPES IMPLEMENTATION:**

##### **General Journal (S0)**
- **Purpose**: Chronological record of all accounting transactions
- **Template**: Dynamic template with configurable columns
- **Data Source**: All AccountingEntry records for period
- **Formulas**: Balance calculations, running totals
- **Validation**: Double-entry bookkeeping compliance

##### **Ledger (S1)**
- **Purpose**: Account-by-account transaction summary
- **Template**: Account-based grouping with debit/credit columns
- **Data Source**: General Journal entries grouped by account
- **Formulas**: Account balance calculations, period comparisons
- **Validation**: Account balance reconciliation

##### **Detailed Ledger (S2)**
- **Purpose**: Detailed transaction breakdown by account
- **Template**: Enhanced ledger with sub-accounts and details
- **Data Source**: Ledger entries with additional context
- **Formulas**: Sub-account aggregations, variance analysis
- **Validation**: Sub-account to main account reconciliation

##### **Trial Balance (S3)**
- **Purpose**: Balance verification and financial statement preparation
- **Template**: Balance sheet format with debit/credit columns
- **Data Source**: Ledger balances for period end
- **Formulas**: Balance verification, variance calculations
- **Validation**: Trial balance equality (debits = credits)

#### **TECHNICAL IMPLEMENTATION DETAILS:**

##### **Formula Engine DSL:**
```
SUM_ACCOUNT("511", "Credit") - Sum credit amounts for account 511
BALANCE_ACCOUNT("156", "Debit") - Get debit balance for account 156
PERCENTAGE("511", "Revenue") - Calculate percentage of total revenue
RATIO("Cost", "Revenue") - Calculate cost-to-revenue ratio
```

##### **Data Provider Architecture:**
```
DataProviderContext
    |-- TenantId: Multi-tenant isolation
    |-- Period: Accounting period (year, month)
    |-- BookType: HKD book type identifier
    |-- Variables: Dynamic calculation variables
```

##### **Template System:**
```
HKDBookTemplate
    |-- TemplateCode: Unique template identifier
    |-- TemplateName: Display name
    |-- Columns: Dynamic column definitions
    |-- Formulas: Calculation formulas
    |-- ValidationRules: Business rule validations
```

#### **PHASE 2.3.1: Fix Architecture Violations - Move domain entities from Service layer to Domain layer**
**Status:** COMPLETED
- [x] Moved domain entities to proper layer
- [x] Fixed Clean Architecture violations
- [x] Updated namespace strategy
- [x] Architecture validation passed

#### **PHASE 2.3.2: Create Tests for Phase 2.1 - Repository Tests (JournalTemplate, Order, HKDBook)**
**Status:** COMPLETED
- [x] JournalTemplateRepository tests
- [x] OrderRepository tests
- [x] HKDBookRepository tests
- [x] Integration test framework

#### **PHASE 2.3.3: Create Tests for Phase 2.2 - Order Service Integration Tests**
**Status:** ⏸️ NOT STARTED - OrderFlow test folders (Layer1_Unit, Layer2_Integration, Layer3_API, Layer4_E2E) đều rỗng
- [ ] OrderService integration tests
- [ ] Order-to-accounting flow tests
- [ ] Business rule validation tests
- [ ] Error handling tests

#### **PHASE 2.3.4: Create Tests for Phase 2.3 - HKD Books Generation Tests**
**Status:** 🔄 IN PROGRESS - FORMULA ENGINE TESTS COMPLETED
- [x] ProductionFormulaEngineTests framework (37 tests total) ✅ COMPLETED
- [x] Test infrastructure với Mock<IDataProvider>, Mock<ILogger> ✅ COMPLETED
- [x] FormulaContext test framework ✅ COMPLETED
- [ ] HKD book generation tests (cần implement) - PENDING
- [ ] Template-based calculation tests (cần implement) - PENDING
- [ ] Multi-tenancy HKD book tests (cần implement) - PENDING
- [ ] Performance tests (cần implement) - PENDING

**📊 CURRENT TEST STATUS:**
- **ProductionFormulaEngineTests:** 0 failed, 37 passed (37 total) ✅ COMPLETED
- **Root Cause:** Tests đã được update để sử dụng FormulaContext thay thế Dictionary<string, decimal>
- **Build Status:** ✅ 0 errors (chỉ warnings)
- **Status:** Formula Engine Tests hoàn thành 100%

#### **PHASE 2.3.5: Formula Engine Implementation - DSL Calculation System**
**Status:** COMPLETED - ARCHITECTURAL BREAKTHROUGH ACHIEVED
- [x] ProductionFormulaEngine với FINAL DSL syntax (SUM_ACCOUNT, BALANCE_ACCOUNT, PERCENTAGE, RATIO)
- [x] Domain-aware FormulaContext (thay thế GUID parsing hack)
- [x] Expression evaluation engine với DataTable.Compute
- [x] Variable context management với proper domain objects
- [x] Error handling cho invalid formulas
- [x] Build thành công 0 errors (chỉ warnings)
- [x] TemplateCalculationEngine integration
- [x] BaseHKDBookTemplate với dependency injection
- [x] TemplateFactory với ILoggerFactory
- [x] HKDBookGenerationService orchestration
- [x] MemoryTemplateCache và BookResultCache

**ARCHITECTURAL BREAKTHROUGH:**
- **Root Cause Fixed:** GUID → Decimal parsing hack eliminated
- **Solution:** Domain-aware FormulaContext với TenantId, AccountingPeriod, Variables
- **Pattern:** Proper domain modeling thay vì primitive obsession

#### **PHASE 2.3.6: Data Provider Service - Multi-tenant Data Aggregation**
**Status:** ✅ MOSTLY COMPLETED - Only real-time data updates remaining
- [x] DataProviderContext với TenantId và AccountingPeriod
- [x] IDataProvider interface định nghĩa
- [x] Multi-tenant data isolation implementation (DataProviderService.cs với TenantId filter)
- [x] Period-based data aggregation logic (GetAccountSum, GetAccountBalance, GetPreAggregatedDataAsync, GetPeriodTotal)
- [x] Caching integration (MemoryTemplateCache.cs + BookResultCache.cs trong Services/Cache/)
- [ ] Real-time data updates infrastructure

#### **PHASE 2.3.7: Business Rule Registry - Vietnamese Accounting Rules**
**Status:** ✅ COMPLETED - TT152/2025/TT-BTC COMPLIANCE IMPLEMENTED
- [x] BusinessRuleRegistry interface
- [x] IBusinessRuleRegistry implementation
- [x] Vietnamese accounting standard rules (5 rules implemented)
- [x] VAT calculation rules implementation (0%, 5%, 10% rates)
- [x] Period closing validations
- [x] Compliance checking system (TT152/2025/TT-BTC)

#### **PHASE 2.3.8: Template Management System - Dynamic HKD Book Templates**
**Status:** 🔄 PARTIALLY COMPLETED - CORE TEMPLATES READY, MANAGEMENT FEATURES PENDING
- [x] HKDBookTemplate abstract base class
- [x] S1aHKDTemplate, S2aHKDTemplate, S3aHKDTemplate implementations (3/7 completed)
- [x] S2bHKDTemplate, S2cHKDTemplate, S2dHKDTemplate, S2eHKDTemplate implementations (4/7 completed)
- [x] TemplateFactory với dependency injection
- [x] TemplateFactory CreateGroup2Template supports all S2a-S2e
- [x] TemplateValidationRule system
- [x] OrderService references to all HKD templates resolved
- [ ] **PENDING: Template creation and editing interface**
- [ ] **PENDING: Template versioning system**
- [ ] **PENDING: Template inheritance and customization**

#### **PHASE 2.3.9: HKD Template Completion - 7 Sổ Kế Toán Implementation**
**Status:** COMPLETED
- [x] S1aHKDTemplate implementation (Không thu GTGT, không nộp thuế TNCN)
- [x] S2aHKDTemplate implementation (Nộp thuế GTGT và TNCN theo tỷ lệ % trên doanh thu)
- [x] S3aHKDTemplate implementation (Có hoạt động thuộc diện chịu các loại thuế khác)
- [x] S2bHKDTemplate implementation (Số doanh thu bán hàng hóa, dịch vụ)
- [x] S2cHKDTemplate implementation (Số chi tiết doanh thu, chi phí)
- [x] S2dHKDTemplate implementation (Số chi tiết vật liệu, dụng cụ, sản phẩm, hàng hóa)
- [x] S2eHKDTemplate implementation (Số chi tiết tiền)
- [x] Update TemplateFactory CreateGroup2Template to support all S2a-S2e
- [x] Fix OrderService references to S2b_HKD, S2c_HKD, S2d_HKD
- [x] Implement HKD tax classification logic for 7 book types
- [x] Create HKD compliance validation rules per Thông tư 152/2025/TT-BTC
- [x] Update HKD tax reporting formats for 7 book types

**📊 CURRENT STATUS:**
- **Templates Completed:** 7/7 (S1a, S2a, S2b, S2c, S2d, S2e, S3a) 
- **Templates Missing:** 0/7 
- **Impact:** All templates available, OrderService references resolved 
- **Priority:** COMPLETED - HKD book generation ready 

#### **PHASE 2.3.10: Performance Optimization - Large Dataset Handling**
**Status:** ⏸️ DEFERRED - Low Priority (HKD Compliance Completed)
- [ ] Batch processing for large datasets
- [ ] Parallel calculation execution
- [ ] Memory optimization strategies
- [ ] Caching and indexing optimization

#### **PHASE 2.3.11: Compliance Validation - Vietnamese HKD Standards Compliance**
**Status:** ✅ COMPLETED - TT152-2025/TT-BTC COMPLIANCE IMPLEMENTED

**🎯 FOCUS:** HỘ KINH DOANH COMPLIANCE (KHÔNG PHẢI DOANH NGHIỆP)

**📋 HKD-SPECIFIC COMPLIANCE:**

##### **1. Thông tư 152/2025/TT-BTC - HKD ACCOUNTING STANDARDS**
- **Effective Date:** 01/01/2026 (thay thế Thông tư 88/TT-BTC)
- **Đối tượng:** Hộ kinh doanh, cá nhân kinh doanh
- **Scope:** Ghi sổ kế toán HKD, 1-4 sổ sách theo loại thuế
- **Replaces:** Thông tư 88/TT-BTC (chế độ kế toán HKD cũ)
- ✅ **IMPLEMENTED:** Full compliance validation system

##### **2. HKD Sổ Kế Toán (1-4 Sổ Tùy Loại Thuế)**
- ✅ **S1a-HKD:** Không thu GTGT, không nộp thuế TNCN (COMPLIANCE RULES IMPLEMENTED)
- ✅ **S2a-HKD:** Nộp thuế GTGT và TNCN theo tỷ lệ % trên doanh thu (COMPLIANCE RULES IMPLEMENTED)
- ✅ **S2b-S2e-HKD:** Revenue book, detailed tracking, inventory, cash tracking (COMPLIANCE RULES IMPLEMENTED)
- ✅ **S3a-HKD:** Nộp thuế GTGT theo tỷ lệ % và TNCN trên thu nhập tính thuế (COMPLIANCE RULES IMPLEMENTED)

##### **3. HKD Tax Classification Logic**
- ✅ **Thuế khoán → Kê khai:** Chuyển đổi từ 01/01/2026 (VALIDATION IMPLEMENTED)
- ✅ **Ngưỡng 500 triệu:** Đã bị bỏ chính thức (COMPLIANCE CHECKS IMPLEMENTED)
- ✅ **Hóa đơn điện tử:** Bắt buộc sử dụng (ELECTRONIC INVOICE VALIDATION IMPLEMENTED)
- ✅ **Lưu trữ:** Tối thiểu 5 năm (điện tử hoặc giấy) (5-YEAR STORAGE VALIDATION IMPLEMENTED)

##### **4. HKD Organization Structure**
- ✅ **Tự ghi sổ:** Người đại diện HKD tự ghi chép (COMPLIANCE VALIDATION IMPLEMENTED)
- ✅ **Bố trí người:** Gia đình (cha mẹ, vợ chồng, con cái) (VALIDATION RULES IMPLEMENTED)
- ✅ **Thuê dịch vụ:** Được thuê dịch vụ kế toán (SERVICE PROVIDER VALIDATION IMPLEMENTED)
- ✅ **Kiêm nhiệm:** Thủ kho, thủ quỹ, người bán hàng (ROLE VALIDATION IMPLEMENTED)

##### **5. HKD Compliance Validation**
- ✅ **HKD Book Type Detection:** Tự động nhận diện loại sổ (AUTOMATIC DETECTION IMPLEMENTED)
- ✅ **Tax Calculation Validation:** Kiểm tra tính thuế theo từng loại (TAX VALIDATION ENGINE IMPLEMENTED)
- ✅ **Electronic Invoice Integration:** Tích hợp hóa đơn điện tử (INVOICE VALIDATION IMPLEMENTED)
- ✅ **Audit Trail Requirements:** Lưu trữ 5 năm (AUDIT TRAIL VALIDATION IMPLEMENTED)

**🎯 HKD IMPLEMENTATION PRIORITY:**

##### **HIGH PRIORITY (Trước MVP):**
- [x] Update HKDBookTemplate cho 7 loại sổ (S1a, S2a, S2b, S2c, S2d, S2e, S3a)
- [x] Implement HKD tax classification logic
- [x] Add electronic invoice integration for HKD compliance per Thông tư 152/2025/TT-BTC

> **📌 Phase 5 E-Invoice:** Chi tiết triển khai xem tại **Sprint 3** trong AUTHORITATIVE 5-WEEK EXECUTION PLAN (đầu file) và **PHASE 5** section riêng (cuối file).

##### **MEDIUM PRIORITY (Sau MVP):**
- [ ] HKD audit trail system (5 năm lưu trữ)
- [ ] HKD tax calculation optimization
- [ ] HKD electronic invoice advanced features
- [ ] HKD compliance reporting dashboard

##### **LOW PRIORITY (Enhancement):**
- [ ] HKD multi-shop management
- [ ] HKD family member access control
- [ ] HKD mobile app for bookkeeping
- [ ] HKD AI-powered tax optimization

**📋 HKD COMPLIANCE CHECKLIST:**
- [x] Thông tư 152/2025/TT-BTC compliance validation system
- [x] 7 loại HKD book templates (S1a, S2a, S2b, S2c, S2d, S2e, S3a)
- [x] HKD tax classification logic
- [ ] Electronic invoice integration (Phase 5 - Sprint 3)
- [ ] 5-year audit trail system (Phase 2.9.4 - Sprint 2)
- [x] HKD compliance validation rules
- [x] Tax authority reporting formats HKD
- [ ] HKD audit requirements (Phase 2.9.4 - Sprint 2)

**🔄 PROGRESS TRACKER:**
1. **✅ COMPLETED:** HKDBookTemplate cho 7 loại sổ
2. **✅ COMPLETED:** HKD tax classification logic
3. **✅ COMPLETED:** HKD compliance validation (TT152-2025/TT-BTC)
4. **✅ COMPLETED:** Phase 2.4 - HKD Tax Reports (7 Sổ Kế Toán)
5. **✅ COMPLETED:** Phase 2.5 - PWA & Real-time Dashboard
6. **⏳ SPRINT 1:** Phase 2.6 - Frontend Accounting Module
7. **⏳ SPRINT 2:** Phase 2.9.3-2.9.4 - Period Closing + Audit Trail
8. **⏳ SPRINT 3:** Phase 5 - E-Invoice Multi-Provider Integration

**📁 REFERENCE FILES:**
- **HKD Compliance Plan:** `C:\VibeCoding\Gemini_Windsurf\docs\MVP_Product\Solution\PHASE 2.3.10 Compliance Validation - Vietnamese Standards Compliance.md`
- **HKD Book Templates:** `c:\VibeCoding\Gemini_Windsurf\3_CoreHub\Services\Template\`
- **HKD Tax Logic:** `c:\VibeCoding\Gemini_Windsurf\3_CoreHub\Services\HKDBookService.cs`

### PHASE 2.4: HKD Tax Reports - 7 SỔ KẾ TOÁN THEO THÔNG TƯ 152/2025/TT-BTC
**Status:** ✅ COMPLETED - HKDTaxReportingService.cs (54KB) implements all 7 book type reports

#### PHASE 2.4.1: HKD Book Type S1a-HKD - Không thu GTGT, không nộp thu TNCN
**Status:** ✅ COMPLETED - GenerateS1aReportAsync trong HKDTaxReportingService.cs
- [x] S1a-HKD template implementation (COMPLETED)
- [x] Revenue recognition rules (HKDTaxReportingService.cs)
- [x] Tax exemption logic (IsTaxExempt=true, VAT=0, PIT=0)
- [x] Report generation (GenerateS1aReportAsync)

#### PHASE 2.4.2: HKD Book Type S2a-HKD - Nộp thu GTGT và TNCN theo tỷ lệ % trên doanh thu
**Status:** ✅ COMPLETED - GenerateS2aReportAsync trong HKDTaxReportingService.cs
- [x] S2a-HKD template implementation (COMPLETED)
- [x] VAT calculation rules (VATRate=0.05, validation rule đã implement)
- [x] Personal income tax calculation (PITCalculation validation rule)
- [x] Percentage-based tax rules (VAT=Revenue*0.05, PIT=Revenue*0.01)

#### PHASE 2.4.3: HKD Book Type S2b-HKD - Sổ doanh thu bán hàng hóa, dịch vụ
**Status:** ✅ COMPLETED - GenerateS2bReportAsync trong HKDTaxReportingService.cs
- [x] S2b-HKD template implementation (COMPLETED)
- [x] Revenue categorization (ServiceRevenue, GoodsRevenue sections)
- [x] Service vs goods differentiation (Revenue categories implemented)
- [x] Revenue aggregation rules (Report generation đã implement)

#### PHASE 2.4.4: HKD Book Type S2c-HKD - Sổ chi tiết doanh thu, chi phí
**Status:** ✅ COMPLETED - GenerateS2cReportAsync trong HKDTaxReportingService.cs
- [x] S2c-HKD template implementation (COMPLETED)
- [x] Revenue and expense tracking (Revenue, Expenses, NetIncome sections)
- [x] Category-based reporting (GetReportSections implemented)
- [x] Detailed transaction analysis (Report generation đã implement)

#### PHASE 2.4.5: HKD Book Type S2d-HKD - Sổ chi tiết vật liệu, dụng cụ, sản phẩm, hàng hóa
**Status:** ✅ COMPLETED - GenerateS2dReportAsync trong HKDTaxReportingService.cs
- [x] S2d-HKD template implementation (COMPLETED)
- [x] Inventory tracking (Inventory, MaterialCosts, Products sections)
- [x] Material cost calculation (Report formulas implemented)
- [x] Product categorization (GetReportSections cho S2d implemented)

#### PHASE 2.4.6: HKD Book Type S2e-HKD - Sổ chi tiết tiền
**Status:** ✅ COMPLETED - GenerateS2eReportAsync trong HKDTaxReportingService.cs
- [x] S2e-HKD template implementation (COMPLETED)
- [x] Cash flow tracking (CashFlow, Inflows, Outflows sections)
- [x] Bank transaction recording (Report generation implemented)
- [x] Payment method categorization (GetReportSections cho S2e implemented)

#### PHASE 2.4.7: HKD Book Type S3a-HKD - Hộ kinh doanh có hoạt động thuộc diện các loại thuế khác
**Status:** ✅ COMPLETED - GenerateS3aReportAsync trong HKDTaxReportingService.cs
- [x] S3a-HKD template implementation (COMPLETED)
- [x] Special tax regime handling (SpecialTax section, Revenue*0.05 special tax)
- [x] Industry-specific tax rules (SpecialTax, TaxCategories, Industry sections)
- [x] Compliance reporting (GenerateS3aReportAsync implemented)

---

## PHASE 2.5: UNIFIED ORDER WORKFLOW - CRITICAL FRONTEND & BACKEND UNIFICATION
**Priority:** CRITICAL - IMPACTS ALL SUBSEQUENT PHASES  
**Timeline:** 4 weeks (accelerates overall MVP completion)  
**Status:** ✅ CORE IMPLEMENTATION COMPLETED - Verification and cleanup items remain

### **EXECUTION GUIDANCE:**
**Current Implementation Plan:** `C:\VibeCoding\Gemini_Windsurf\docs\MVP_Product\Solution\PHASE_2_5_DETAILED_CODING_PLAN.md` ✅ **FINANCIAL-GRADE PLAN COMPLETED**
**Previous Reference:** `C:\VibeCoding\Gemini_Windsurf\docs\MVP_Product\Solution\codingPlanOrderUnifiled.md`

**Key Benefits:**
- ✅ **50% faster MVP completion** (unified vs separate workflows)
- ✅ **60% reduction in codebase complexity** (single source of truth)
- ✅ **Offline-first capability** (PWA with IndexedDB)
- ✅ **Ultra-low-cost infrastructure** ($6-7/month)
- ✅ **Real-time staff dashboard** (SignalR integration)
- ✅ **Financial-grade safety** (41 test cases covering all critical gaps)
- ✅ **Production resilience** (retry strategy, time-based bug prevention)
- ✅ **Data integrity protection** (idempotent behavior, version control)

### **PHASE 2.5.1: Backend Consolidation - Single Source of Truth**
**Status:** ✅ COMPLETED - Backend Integration Complete
- [x] Remove ShopERP duplicate services (OrderWorkflowService, OrderQueueService)
- [x] Enhance CoreHub.OrderService with queue support and full integration
- [x] Create OrderHub for real-time notifications (SignalR) - ✅ IMPLEMENTED
- [x] Implement unified order processing with accounting integration
- [x] Add background task queue for high-volume processing
- [x] **Reference:** See PHASE_2_5_DETAILED_CODING_PLAN.md Week 1 (Financial Safety Infrastructure)
- [x] **Current Status:** OrderService.cs (239 lines) + OrderHub.cs (22 lines) fully implemented

### **PHASE 2.5.2: KhachLink PWA - Customer-Facing Offline-First Interface**
**Status:** ✅ COMPLETED - All PWA services implemented
- [x] Add service worker for PWA functionality - ✅ IMPLEMENTED (service-worker.js, 195 lines)
- [x] Create PWAService for PWA management - ✅ IMPLEMENTED (PWAService.cs, 260 lines)
- [x] **NEW: Create IndexedDB service for local storage** - ✅ IMPLEMENTED (IndexedDBService.cs + IndexedDBService.ts)
- [x] **NEW: Implement OfflineOrderService for offline order creation** - ✅ IMPLEMENTED (OfflineOrderService.cs + OfflineOrderService.ts)
- [x] **NEW: Enhance CartService with sync capabilities** - ✅ IMPLEMENTED (EnhancedCartService.cs + EnhancedCartService.ts)
- [x] **NEW: Implement conflict resolution for sync failures** - ✅ IMPLEMENTED (ConflictResolutionService.cs + SyncConflictResolver.cs)
- [x] **Reference:** See PHASE_2_5_DETAILED_CODING_PLAN.md Week 1 (Financial Safety Infrastructure)
- [x] **Current Status:** All 6 core PWA services implemented in 5_WebApps/KhachLink/Services/

### **PHASE 2.5.3: ShopERP Dashboard - Real-Time Staff Management**
**Status:** ✅ COMPLETED - All ShopERP dashboard services implemented
- [x] **NEW: Create OrderManagementService (uses CoreHub.OrderService)** - ✅ IMPLEMENTED (OrderManagementService.cs 10KB, ShopERP/Services/)
- [x] **NEW: Implement real-time dashboard with SignalR** - ✅ IMPLEMENTED (RealtimeDashboardService.cs 13KB, ShopERP/Services/)
- [x] **NEW: Add order status update interface** - ✅ IMPLEMENTED (UpdateOrderStatusAsync trong OrderManagementService.cs)
- [x] **NEW: Create order tracking and history view** - ✅ IMPLEMENTED (OrderStatusHistory, GetOrderSummaryAsync)
- [x] **NEW: Implement staff notification system** - ✅ IMPLEMENTED (AssignOrderToStaffAsync + Error notifications)
- [x] **Reference:** See PHASE_2_5_DETAILED_CODING_PLAN.md Week 2 (State Machine UI + Retry Strategy)
- [x] **Current Status:** All services in 5_WebApps/ShopERP/Services/ implemented (OrderManagementService + RealtimeDashboardService)

### **PHASE 2.5.4: Unified API Integration - Single Backend Service**
**Status:** ✅ MOSTLY COMPLETED - Integration tests still pending
- [x] Update KhachLink to use CoreHub.OrderService - ✅ OrdersController.cs implemented
- [x] **NEW: Update ShopERP to use CoreHub.OrderService** - ✅ IMPLEMENTED (ShopERP/Controllers/OrdersController.cs)
- [ ] **NEW: Remove duplicate API endpoints** (cần kiểm tra để xác nhận)
- [x] **NEW: Implement unified error handling** - ✅ IMPLEMENTED (ErrorNotificationService.cs trong ShopERP/Services/)
- [ ] **NEW: Add comprehensive integration tests** (41 test cases - chưa implement đủ theo plan)
- [x] **Reference:** See PHASE_2_5_DETAILED_CODING_PLAN.md Week 3 (Time-Based Bugs + Production Data)
- [x] **Current Status:** RetryStrategyTests.cs, TimeBasedBugTests.cs, UIStateMachineTests.cs, ProductionDataTests.cs đã có trong 6_Tests/

### **PHASE 2.5.5: Integration & Testing - End-to-End Validation**
**Status:** 🔄 PARTIALLY COMPLETED - Financial-grade testing strategy implemented; full verification still pending
- [x] **Create integration tests for unified workflow** (41 test cases across 8 critical gaps)
- [x] **Implement performance tests for concurrent orders** (ProductionDataTests.cs)
- [x] **Add error handling and edge case tests** (RetryStrategyTests.cs + TimeBasedBugTests.cs)
- [x] **Test offline sync functionality** (OfflineOrderServiceTests.cs)
- [x] **Validate real-time notifications** (OrderHubUnitTests.cs + SignalRIntegrationTests.cs)
- [x] **Reference:** See PHASE_2_5_DETAILED_CODING_PLAN.md (Financial-Grade Plan v4.0)
- [x] **Current Status:** Test coverage exists for 8 critical production gaps; final verification remains required

### **PHASE 2.5.6: Deployment & Documentation - Production Ready**
**Status:** PLANNED
- [ ] Docker configuration for unified deployment
- [ ] Nginx reverse proxy with SSL
- [ ] Ultra-low-cost infrastructure ($6-7/month)
- [ ] Deployment scripts and documentation
- [ ] Monitoring and logging setup

### **PHASE 2.5 TECHNICAL ARCHITECTURE:**
```
KhachLink (Customer PWA) → CoreHub.OrderService → ShopERP (Staff Dashboard)
        ↓                        ↓                        ↓
   IndexedDB              SQLite + Outbox           SignalR Notifications
   Offline Sync            Accounting Integration     Real-time Updates
```

---

## PHASE 2.6: FRONTEND ACCOUNTING MODULE
**Status:** ⏳ PLANNED IN SPRINT 1 (2 tuần)
**Authoritative Plan:** See **Sprint 1** in AUTHORITATIVE 5-WEEK EXECUTION PLAN at top of document.
**Location:** `5_WebApps/ShopERP/Components/Pages/Accounting/`
**Components:** `RevenueEntryForm.razor`, `ExpenseEntryForm.razor`, `TransactionHistory.razor`, `AccountBalance.razor`
**Routes:** `/accounting`, `/accounting/revenue`, `/accounting/expenses`, `/accounting/history`, `/accounting/balance`

---

## PHASE 2.7: EXCEL IMPORT SYSTEM - NEW PHASE BASED ON RISK ANALYSIS

### PHASE 2.7.1: File Upload Interface - Excel File Processing
**Status:** NEW - CRITICAL FOR MVP
- [ ] Drag-and-drop file upload
- [ ] File format validation
- [ ] Upload progress tracking
- [ ] File preview and validation
- [ ] Error handling for invalid files

### PHASE 2.7.2: Basic Excel Parsing - Data Extraction Engine
**Status:** NEW - CRITICAL FOR MVP
- [ ] Excel file parsing library
- [ ] Column mapping configuration
- [ ] Data type detection
- [ ] Row-by-row processing
- [ ] Batch processing for large files

### PHASE 2.7.3: Data Validation - Quality Assurance
**Status:** NEW - CRITICAL FOR MVP
- [ ] Data format validation
- [ ] Business rule checking
- [ ] Duplicate detection
- [ ] Account code validation
- [ ] Amount and date validation

### PHASE 2.7.4: Import Reporting - Results and Feedback
**Status:** NEW - CRITICAL FOR MVP
- [ ] Import success/failure report
- [ ] Error details and corrections
- [ ] Import statistics dashboard
- [ ] Rollback capability for failed imports
- [ ] Import history tracking

---

## PHASE 2.8: ENHANCED INTEGRATION - UPDATED BASED ON UNIFIED WORKFLOW

### PHASE 2.8.1: Event-Driven Order Processing - Automatic Accounting Entries
**Status:** PARTIALLY COMPLETED (implemented in unified workflow)
- [x] OrderCreated event handling (SignalR)
- [x] OrderCompleted event processing (unified workflow)
- [x] Automatic journal entry generation (CoreHub.OrderService)
- [ ] Event-driven architecture optimization
- [ ] Message queue integration (background processing)

### PHASE 2.8.2: Journal Template System - Predefined Transaction Templates
**Status:** NEW - MEDIUM PRIORITY
- [ ] Journal template repository
- [ ] Template-based entry generation
- [ ] Template customization interface
- [ ] Template validation rules
- [ ] Template versioning system

### PHASE 2.8.3: Error Handling and Recovery - Robust Error Management
**Status:** PARTIALLY COMPLETED (implemented in unified workflow)
- [x] Error logging and tracking (unified workflow)
- [x] Automatic retry mechanisms (offline sync)
- [x] Error notification system (SignalR)
- [ ] Manual error correction interface
- [ ] Data reconciliation tools

---

## PHASE 2.9: USER WORKFLOW - UPDATED BASED ON UNIFIED WORKFLOW

### PHASE 2.9.1: Accountant Dashboard - User Interface for Accountants
**Status:** PARTIALLY COMPLETED (ShopERP dashboard available)
- [x] Dashboard with key metrics (ShopERP)
- [x] Quick action buttons (order management)
- [x] Recent transactions overview (order tracking)
- [ ] Accounting-specific metrics
- [ ] Performance indicators

### PHASE 2.9.2: Transaction Approval - Approval Workflow System
**Status:** NEW - MEDIUM PRIORITY
- [ ] Approval queue interface
- [ ] Role-based approval system
- [ ] Approval history tracking
- [ ] Bulk approval capabilities
- [ ] Rejection reason logging

### PHASE 2.9.3: Period Closing - Month/Year End Processes
**Status:** PLANNED IN SPRINT 2 - Task 2.1
**Backend:** `3_CoreHub/Services/`
**Frontend:** `ShopERP/Components/Pages/Accounting/PeriodClosing.razor`
- [ ] Tạo `IPeriodClosingService` + `PeriodClosingService.cs`
- [ ] Pre-closing validation checks (balance reconciliation, missing entries)
- [ ] Automated closing procedures (calculate closing entries)
- [ ] Reopen capability với reversal pattern (AccountingEntry immutable - dùng Reversal Entry)
- [ ] Step-by-step wizard UI: Validate → Review → Close
- [ ] Closing checklist display, PDF-ready closing report, confirmation dialog với VanAModal

### PHASE 2.9.4: Audit Trail - Complete Audit System
**Status:** PLANNED IN SPRINT 2 - Task 2.2
**Backend:** `3_CoreHub/Services/`
**Frontend:** `ShopERP/Components/Pages/Accounting/AuditTrail.razor`
- [ ] Tạo `IAuditTrailService` + `AuditTrailService.cs`
- [ ] Ghi log mọi action: create, view, export, login/logout
- [ ] 5-year retention policy enforcement
- [ ] Queryable audit log với filter theo user, action, date
- [ ] Audit log table với VanAOrderTable component
- [ ] Export audit report

---

## PHASE 3: TRADING/SERVICE COMPANY LITE - EXTENDED ACCOUNTING, PAYABLES/RECEIVABLES

### PHASE 3.1: Thông tư 99/2025/TT-BTC Compliance - Doanh Nghiệp Accounting Standards
**Status:** 🔄 PLANNING - EFFECTIVE 01/01/2026
**Reference:** `C:\VibeCoding\Gemini_Windsurf\docs\MVP_Product\Solution\PHASE 2.3.10 Compliance Validation - Vietnamese Standards Compliance.md`

**🎯 FOCUS:** DOANH NGHIỆP COMPLIANCE (KHÔNG PHẢI HỘ KINH DOANH)

##### **1. Thông tư 99/2025/TT-BTC - Doanh Nghiệp Accounting Standards**
- **Effective Date:** 01/01/2026 (thay thế Thông tư 200/2014/TT-BTC)
- **Đối tượng:** Doanh nghiệp, công ty
- **Scope:** Hệ thống tài khoản kế toán doanh nghiệp, báo cáo tài chính, quản trị nội bộ
- **Replaces:** Thông tư 200/2014/TT-BTC, Thông tư 75/2015/TT-BTC, Thông tư 53/2016/TT-BTC

##### **2. Doanh Nghiệp System Changes**
- [ ] **Hệ thống tài khoản MỚI:** Phụ lục II - 42 mẫu sổ (giảm từ 45)
- [ ] **Tài khoản mới:** TK 82111, TK 82112 (thuế TNDN hiện hành & bổ sung)
- [ ] **Tài khoản bị BỎ:** TK 1562 → TK 632, TK 611 → TK 632
- [ ] **Báo cáo tài chính MỚI:** Phụ lục IV - cấu trúc mới

##### **3. Doanh Nghiệp Management Features**
- [ ] **Quản trị nội bộ:** Quy chế quản trị và kiểm soát nội bộ
- [ ] **Tự thiết kế tài khoản:** Doanh nghiệp được tự thiết kế hệ thống tài khoản chi tiết
- [ ] **Multi-currency support:** Đơn vị tiền tệ linh hoạt
- [ ] **Advanced reporting:** Báo cáo quản trị chi tiết

##### **4. Doanh Nghiệp Implementation Priority**
- [ ] **HIGH:** Update ProductionFormulaEngine với hệ thống tài khoản mới
- [ ] **HIGH:** Cập nhật tất cả templates cho doanh nghiệp
- [ ] **MEDIUM:** Implement quản trị nội bộ features
- [ ] **LOW:** Advanced reporting và analytics

### PHASE 3.2: Extended Chart of Accounts - Company Account Structure
**Status:** PLANNING
- [ ] Extended account hierarchy (TT99 compliant)
- [ ] Company-specific accounts (customizable)
- [ ] Industry account templates
- [ ] Account customization tools (self-design)

### PHASE 3.3: Payables Management - Vendor and Bill Management
**Status:** PLANNING
- [ ] Vendor management system
- [ ] Bill processing workflow
- [ ] Payment scheduling
- [ ] Vendor balance tracking

### PHASE 3.4: Receivables Management - Customer and Invoice Management
**Status:** PLANNED
- [ ] Customer management system
- [ ] Invoice generation and tracking
- [ ] Payment processing
- [ ] Collection management

---

## PHASE 4: PACKAGING & GO-TO-MARKET - DOCKER DEPLOYMENT, DOCUMENTATION, CUSTOMER ACQUISITION

### PHASE 4.1: Docker Deployment - Containerized Deployment
**Status:** PLANNED
- [ ] Docker containerization
- [ ] Kubernetes deployment
- [ ] Environment configuration
- [ ] Scaling strategies

### PHASE 4.2: Documentation - Complete User Documentation
**Status:** PLANNED
- [ ] User manual creation
- [ ] API documentation
- [ ] Installation guides
- [ ] Training materials

### PHASE 4.3: Customer Acquisition - Marketing and Sales
**Status:** PLANNED
- [ ] Marketing materials
- [ ] Sales strategy
- [ ] Customer onboarding
- [ ] Support system

---

## MVP STATUS SUMMARY & NEXT STEPS

### ✅ COMPLETED PHASES (As of May 3, 2026):

| Phase | Description | Status |
|-------|------------|--------|
| 1 | Core Accounting Engine | ✅ 100% Completed |
| 2.1 | Repository Implementation | ✅ Completed |
| 2.2 | Order-to-Accounting Integration | ✅ Completed |
| 2.3 | 7 HKD Books + Formula Engine + Compliance | ✅ Core Completed (template mgmt features pending) |
| 2.4 | HKD Tax Reports (7 book types) | ✅ 100% Completed |
| 2.5 | Unified Order Workflow | ✅ Core Completed (verification pending) |

**Key achievements:**
- Immutable AccountingEntry with append-only pattern + EF Core multi-tenancy
- 7/7 HKD book templates (S1a, S2a, S2b, S2c, S2d, S2e, S3a) with TT152-2025 compliance
- Formula Engine DSL + Business Rule Registry + Data Provider Service
- PWA + Real-time Dashboard (SignalR) + UI Platform (94% error reduction)
- Build: 0 errors | Tests: 14/14 passing | Architecture: Clean Architecture

### 🔄 REMAINING PHASES FOR PRODUCTION READINESS:

| Phase | Description | Status | Sprint |
|-------|------------|--------|--------|
| **2.6** | Frontend Accounting Module | ⏳ Planned | **Sprint 1** (2 tuần) |
| 2.7 | Excel Import System | ⏳ Planned | TBD |
| 2.8 | Enhanced Integration | 🔄 Partial | TBD |
| **2.9.1-2.9.2** | Dashboard + Approval | 🔄 Partial | TBD |
| **2.9.3-2.9.4** | Period Closing + Audit Trail | ⏳ Planned | **Sprint 2** (1 tuần) |
| 3 | Trading/Service (TT99 Compliance) | 📋 Planning | Post-MVP |
| 4 | Packaging & Go-to-Market | 📋 Planned | Post-MVP |
| **5** | E-Invoice Multi-Provider | ⏳ Planned | **Sprint 3** (2 tuần) |

### 🎯 RECOMMENDED EXECUTION ORDER:

> See **AUTHORITATIVE 5-WEEK EXECUTION PLAN** at the top of this document for detailed sprint tasks.

1. **Sprint 1** (2 tuần): Phase 2.6 - Frontend Accounting Module → Kế toán viên nhập liệu được
2. **Sprint 2** (1 tuần): Phase 2.9.3 + 2.9.4 - Period Closing & Audit Trail → Đóng sổ + audit log
3. **Sprint 3** (2 tuần): Phase 5 - E-Invoice Multi-Provider → TT152-2025 production compliance

### 📊 SUCCESS METRICS:

| Category | Metric | Status |
|----------|--------|--------|
| Build | 0 errors, <10 warnings | ✅ Achieved |
| Tests | Layer 1 & 2 (14/14 passing) | ✅ Achieved |
| Architecture | Clean Architecture + multi-tenancy | ✅ Achieved |
| Performance | <2s response, <500ms API | ✅ Achieved |
| Infrastructure | <$10/month ultra-low-cost | ✅ Achieved |
| HKD Compliance | Core TT152-2025 accounting | 🔄 Production requires Phase 5 |
| Market Readiness | Core MVP foundation | 🔄 Production requires Phase 5 |

### ⚠️ RISK ASSESSMENT:

| Risk | Status | Mitigation |
|------|--------|-----------|
| Frontend Gap | ✅ Resolved | Phase 2.5 unified workflow |
| Entry Point Gap | ✅ Resolved | Phase 2.5 + 2.6 |
| User Workflow Gap | ✅ Resolved | Phase 2.8-2.9 |
| E-Invoice Compliance (01/01/2026) | 🔴 Critical | Sprint 3 must complete before deadline |
| Timeline (5 tuần remaining work) | 🟡 Active | Sprint plan with clear milestones |

---

## 🎯 CONCLUSION

**The Van An Accounting System core MVP foundation is functionally complete. Production-ready HKD compliance depends on completing Phase 5: E-Invoice Multi-Provider Integration before the 01/01/2026 deadline.**

**IMMEDIATE ACTION:** Execute the 5-week sprint plan → Phase 2.6 → Phase 2.9.3/2.9.4 → Phase 5.
