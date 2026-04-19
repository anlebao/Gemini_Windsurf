# USE CASE & BUSINESS DESIGN DOCUMENT
## Core Accounting Engine - Week 1
**Project:** Vạn An Accounting EcoSystem  
**Module:** Core Accounting Engine (Foundation)  
**Version:** 1.3  
**Date:** 16/04/2026  
**Authors:** User + Grok  
**Status:** Approved

### 1. Mục tiêu Kinh doanh của Tuần 1

Xây dựng **nền tảng kế toán immutable** làm core engine cho toàn bộ hệ thống, đáp ứng:
- Kế toán Hộ Kinh Doanh theo Thông tư 152/2025/TT-BTC (cash basis, 4 sổ sách)
- Nền tảng mở rộng cho Kế toán Công ty TM&DV (accrual basis, công nợ, bút toán kép)
- Tuân thủ nghiêm ngặt nguyên tắc **immutable + reversal-only**
- Hỗ trợ **offline-first** mạnh mẽ + sync an toàn
- Đảm bảo trải nghiệm người dùng mượt mà (Order không bị block bởi accounting, thuế, hóa đơn điện tử)

### 2. Use Cases Chi Tiết

**UC-01: Tạo bút toán mới (Create Accounting Entry)**
- Actor: ShopERP, KhachLink, External API, Kế toán viên
- Precondition: User đã xác thực + TenantId hợp lệ
- Postcondition: Bút toán được lưu immutable, sinh tự động vào các sổ sách tương ứng
- Main Flow:
  1. Validate input (số tiền, kỳ kế toán, loại sổ, tenant...)
  2. AccountingEntryFactory tạo bút toán immutable
  3. Sinh bút toán vào 4 sổ sách HKD (nếu áp dụng)
  4. Publish Domain Event "AccountingEntryCreated"
  5. Ghi audit log

**UC-02: Đảo bút toán (Create Reversal Entry)**
- Actor: Kế toán viên / System
- Rule cứng: Chỉ được đảo, không được sửa/xóa bút toán gốc
- Main Flow:
  1. Tìm bút toán gốc
  2. Kiểm tra chưa bị đảo
  3. Tạo bút toán đảo (âm số tiền) qua Factory
  4. Liên kết hai bút toán qua ReversalEntryId
  5. Sinh lại sổ sách tương ứng

**UC-06: Place Simple Order (Entry Point cho E2E Test)**
- Actor: Tester, Postman, ShopERP UI
- Mục đích: Entry point đơn giản để test end-to-end nhanh
- Request: `{ TenantId, CustomerId?, Items: [{ProductId, Quantity, UnitPrice}] }`
- Response ngay lập tức (không chờ accounting)

**UC-04: Offline → Online Delta Sync**
- Đảm bảo idempotency và không nhân đôi bút toán

### 3. Business Rules Bắt Buộc (Golden Rules)

1. **Immutable Principle**: AccountingEntry không được sửa hoặc xóa sau khi tạo.
2. **Reversal Only**: Mọi thay đổi tài chính phải tạo bút toán đảo.
3. **4 Sổ Sách HKD** (TT152):
   - Sổ quỹ tiền mặt & ngân hàng (CashBankBook)
   - Sổ mua hàng (ExpenseBook)
   - Sổ bán hàng (RevenueBook)
   - Sổ kê khai thuế (TaxDeclarationBook)
4. **Multi-tenancy**: Mọi entity phải có TenantId + Global Query Filter.
5. **Eventual Consistency**: Order response < 2 giây, accounting xử lý async.
6. **Audit Trail**: Ghi đầy đủ CreatedAt, CreatedBy, ReversalId, Reference.

### 4. Luồng Dữ Liệu (Data Flow)

```mermaid
flowchart LR
    A[ShopERP/KhachLink] --> B[OrderService\nCreateSimpleOrder]
    B --> C[Save Order + Items\nSQLite]
    C --> D[Outbox Table\nOrderCreated Event]
    D --> E[Background Worker]
    E --> F[AccountingService\nCreate Immutable Entry]
    E --> G[HKD Book Service\nSinh 4 sổ sách]
    E --> H[Tax Sync Service]
    E --> I[E-Invoice Service + Signature]
    F --> J[PostgreSQL Central DB] 
5. Luồng Xử Lý (Processing Flow)
mermaidsequenceDiagram
    participant User
    participant ShopERP
    participant OrderService
    participant Outbox
    participant BackgroundWorker
    participant AccountingService

    User->>ShopERP: Place Order (Simple)
    ShopERP->>OrderService: CreateSimpleOrder
    OrderService->>OrderService: Save Order (SQLite)
    OrderService->>Outbox: Publish OrderCreated
    ShopERP-->>User: Return Success (ngay lập tức)

    Outbox->>BackgroundWorker: Process Outbox
    BackgroundWorker->>AccountingService: Create AccountingEntry
    BackgroundWorker->>AccountingService: Generate 4 HKD Books
6. Domain Model Design (Chi Tiết)
Value Objects:

AccountingEntryId, TenantId, AccountingBookType, AccountingPeriod, Money, AccountNumber, ReversalEntryId

Entity chính:

AccountingEntry (immutable core)
CompanyAccountingEntry (mở rộng cho công ty)

Factory:

AccountingEntryFactory.CreateRevenueEntry(), CreateExpenseEntry(), CreateReversalEntry()

Outbox Pattern:

Bảng OutboxMessages để xử lý async

7. Non-Functional Requirements

Order response time ≤ 2 giây
100% immutable cho bút toán tài chính
Hỗ trợ offline mode hoàn toàn
Audit trail đầy đủ
Ready cho E2E testing từ tuần 1