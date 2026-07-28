# **Engineering Specification**

## **Project: ShopERP Mini Migration & Accounting Decoupling**

### **Status**

**Proposed**

### **Priority**

**P0 \- Architecture Refactoring**

---

# **1\. Background**

The current ShopERP application contains both **Front Office** and **Back Office** responsibilities.

Current responsibilities include:

* POS  
* Product Management  
* Customer Management  
* Inventory  
* Loyalty  
* CRM  
* Accounting  
* Financial Reports  
* Tax  
* Journal  
* Ledger  
* Dashboard

This architecture creates several issues:

* Large application size  
* High memory consumption  
* Heavy SQLite database  
* Slow synchronization  
* Poor mobile/PWA experience  
* Tight coupling between sales and accounting

For the long-term Hybrid Local-First architecture, ShopERP must become a lightweight operational application.

Accounting must become an independent backend service.

---

# **2\. Business Goal**

Transform ShopERP into a lightweight operational PWA ("ShopERP Mini") that can be installed and used comfortably on:

* Android phones  
* Android tablets  
* iPhone  
* iPad  
* Windows desktop  
* Browser

The application must remain fully functional offline.

Accounting is **NOT** executed on the local device.

---

# **3\. High-Level Architecture**

Current architecture

ShopERP

├── POS  
├── CRM  
├── Loyalty  
├── Inventory  
├── Accounting  
├── Tax  
├── Ledger  
├── Journal  
├── Reports

Target architecture

               ShopERP Mini (PWA)

        POS  
        Product  
        Customer  
        Loyalty  
        Orders  
        Inventory  
        Offline Sync

                    │

             Domain Events

                    │

              CoreHub Backend

                    │

        Accounting Engine Service

                    │

        Journal  
        Ledger  
        Tax  
        Financial Reports  
---

# **4\. Scope**

## **In Scope**

Migration of ShopERP into ShopERP Mini.

Move Accounting responsibilities out of ShopERP.

Introduce Event-driven accounting processing.

Reduce application footprint.

Optimize for mobile PWA.

---

## **Out of Scope**

No business logic changes.

No accounting rule changes.

No database schema redesign inside Accounting.

No tax calculation redesign.

---

# **5\. Functional Responsibilities**

## **ShopERP Mini Responsibilities**

ShopERP Mini SHALL contain only operational functions.

### **Included Modules**

* Authentication  
* Authorization  
* Product Catalog  
* Categories  
* Customer Management  
* Membership  
* Loyalty  
* Voucher Redemption  
* Order Management  
* POS  
* QR Payment  
* Inventory (Operational only)  
* Receipt Printing  
* Local Notifications  
* Device Registration  
* Offline SQLite  
* Synchronization

---

### **ShopERP Mini MUST NOT contain**

Accounting Journal

General Ledger

Trial Balance

Balance Sheet

Income Statement

Cash Flow

Tax Posting

Journal Posting

Accounting Voucher

Accounting Reports

Financial Statements

Cost Allocation

Depreciation

Payroll

Asset Management

---

# **6\. Accounting Service Responsibilities**

Accounting becomes a backend-only bounded context.

Responsibilities include:

* Journal generation  
* Double-entry posting  
* General Ledger  
* Tax  
* Financial Statements  
* Cost Accounting  
* Revenue Recognition  
* Voucher Accounting  
* Reporting

No Accounting UI exists inside ShopERP Mini.

---

# **7\. Event-Driven Architecture**

ShopERP Mini must never create accounting entries directly.

Instead, ShopERP Mini publishes business events.

Example

OrderCompleted

↓

CoreHub Event Bus

↓

Accounting Worker

↓

JournalEntry

↓

LedgerPosting

↓

Financial Statements

Accounting consumes events asynchronously.

---

# **8\. Synchronization Rules**

ShopERP Mini synchronizes only operational entities.

Examples:

Synchronize

* Orders  
* Customers  
* Products  
* Inventory Transactions  
* Loyalty Transactions  
* Payment Records

Do NOT synchronize

* Journal Entries  
* Ledger  
* Trial Balance  
* Financial Statements  
* Accounting Reports

Accounting data remains inside CoreHub.

---

# **9\. Offline Requirements**

ShopERP Mini must remain fully operational offline.

Offline operations include:

* Create Orders  
* Receive Payments  
* Issue Receipts  
* Redeem Loyalty  
* Inventory deduction  
* Customer lookup

Accounting processing is NOT required offline.

---

# **10\. Dashboard Changes**

ShopERP Mini Dashboard displays only operational KPIs.

Allowed KPIs

Today's Sales

Today's Orders

Average Bill

Customer Count

Pending Orders

Inventory Alerts

Loyalty Statistics

Accounting KPIs are removed.

Examples removed

Net Profit

Balance Sheet

Trial Balance

General Ledger

Tax Report

---

# **11\. Reporting**

Operational reports remain.

Examples

Daily Sales

Product Ranking

Top Customers

Cashier Performance

Accounting reports move to Accounting Service.

Examples

Income Statement

Balance Sheet

Cash Flow

General Ledger

Journal

Tax Reports

---

# **12\. Database**

SQLite database should contain only operational data.

Target reduction:

Remove accounting tables from local SQLite.

Examples

Remove

JournalEntries

LedgerEntries

AccountingVouchers

FinancialStatements

TrialBalance

GeneralLedger

TaxPosting

Keep

Orders

Products

Customers

Inventory

Payments

Loyalty

Settings

SyncQueue

---

# **13\. Performance Targets**

Application Size

Target:

Reduce application package size by at least 40%.

Memory

Target:

Idle memory consumption below 120 MB.

Startup

Target:

Cold startup below 2 seconds on a mid-range Android device.

Synchronization

Target:

Reduce synchronization payload by at least 60%.

---

# **14\. PWA Requirements**

ShopERP Mini SHALL be installable as a Progressive Web App.

Requirements

* Responsive UI  
* Touch-first design  
* Offline cache  
* Install prompt  
* App icon  
* Splash screen  
* Background synchronization  
* Local notifications  
* Camera support  
* QR scanning

Desktop layout remains supported.

---

# **15\. Event Contracts**

The following events become the source of truth.

Examples

OrderCreated

OrderCompleted

OrderCancelled

PaymentReceived

InventoryAdjusted

CustomerRegistered

LoyaltyRedeemed

Accounting consumes these events.

Accounting never reads UI state.

Accounting never depends on Blazor components.

---

# **16\. Migration Strategy**

Phase 1

Separate Accounting UI.

Phase 2

Separate Accounting Services.

Phase 3

Replace direct Accounting calls with Domain Events.

Phase 4

Remove Accounting from ShopERP.

Phase 5

Optimize ShopERP Mini for mobile PWA.

Phase 6

Performance testing.

---

# **17\. Acceptance Criteria**

The migration is complete when:

* ShopERP contains no Accounting UI.  
* ShopERP contains no Accounting business logic.  
* ShopERP contains no Journal/Ledger posting.  
* Accounting is fully event-driven.  
* ShopERP operates completely offline without Accounting.  
* ShopERP installs successfully as a PWA on Android/iOS/Desktop.  
* SQLite contains only operational data.  
* Existing business workflows continue to function without regression.  
* Accounting reports are generated exclusively by the Accounting Service after synchronization.

---

## **Engineering Principles (Non-Negotiable)**

1. **Strict Bounded Contexts**: ShopERP Mini (Operational) and Accounting (Financial) are independent bounded contexts.  
2. **Event-Driven Integration**: Communication between contexts occurs only through immutable domain events; no direct service or UI dependencies are allowed.  
3. **Offline First**: Operational workflows must never depend on the availability of the Accounting Service or network connectivity.  
4. **Single Source of Truth**: ShopERP Mini owns operational data; the Accounting Service owns financial data derived from operational events.  
5. **Backward Compatibility**: Existing sales, inventory, loyalty, and synchronization workflows must continue to function throughout the migration.  
6. **No Business Rule Regression**: The migration is architectural only; financial calculations and accounting rules must produce identical results to the current implementation.

