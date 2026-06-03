# MASTER PLAN - VÃN AN ACCOUNTING MVP
## 4-WEEK DEVELOPMENT ROADMAP

---

## OVERVIEW

This Master Plan outlines the 4-week development roadmap for VÃN AN ACCOUNTING MVP, designed to deliver a complete accounting solution for both Household Businesses (HKD) and small Trading/Service Companies.

---

## BUSINESS OBJECTIVES

### Primary Goals
- **Week 3**: Ready for demo and sales to Household Businesses
- **Week 4**: Ready for demo and sales to small Companies
- **Revenue Target**: Sign lease/sale contracts by end of Week 4

### Success Metrics
- Functional accounting system with real business value
- Complete tax compliance for HKD (TT152)
- Basic financial reports for companies
- Production-ready deployment

---

## 🎯 CURRENT CONTEXT & PROGRESS

### **ARCHITECTURE: Hybrid Generic Template Architecture - Vạn An Accounting System**
**Framework:** Clean Architecture + Domain-Driven Design + Multi-tenancy
**Pattern:** Immutable AccountingEntry + Domain Services + Repository Pattern

### **CURRENT POSITION: End of Phase 2.1, Ready for Phase 2.2**
**Timeline:** Week 2 (Day 8-9) of 4-week MVP delivery
**Progress:** Phase 1 ✅ Complete, Phase 2.1 ✅ Complete, Phase 2.2 ⏳ Ready to start

### **PHASE 1 COMPLETED ✅**
- **Domain Architecture:** Pure domain entities with immutable design
- **Domain Services:** JournalEntryService, JournalTemplateService implemented
- **Repository Pattern:** IAccountingEntryRepository + Implementation
- **API Layer:** Only POST endpoints, zero-error build achieved
- **Multi-tenancy:** 100% data isolation enforced

### **PHASE 2.1 COMPLETED ✅**
- **JournalTemplateRepository:** Interface + Implementation
- **OrderRepository:** Interface + Implementation  
- **HKDBookRepository:** Interface + Implementation
- **Repository Layer:** Complete data access foundation

---

## 4-WEEK MASTER PLAN

| Week | Phase Name | Main Objective | Primary Target | Status | Key Deliverables |
|------|------------|-----------------|----------------|--------|------------------|
| 1 | Core Accounting Engine | Build immutable AccountingEntry + Domain Services | HKD + Common Foundation | **✅ COMPLETED** | - Immutable accounting core<br>- Domain Services (JournalEntry, JournalTemplate)<br>- Multi-tenancy<br>- Reversal-only pattern<br>- Zero-error build |
| 2 | Order Flow + HKD Accounting | Order flow -> Auto entries -> 4 HKD books + Sync | HKD | **🔄 IN PROGRESS** | - Repository Implementation ✅<br>- Order-to-accounting integration ⏳<br>- 4 HKD Books ⏳<br>- Offline SQLite sync ⏳<br>- HKD tax reports ⏳ |
| 3 | Company Lite + Sync | Basic company accounting + Delta offline-online sync | Trading/Service Co + HKD | **⏳ PENDING** | - Extended accounting for companies<br>- Payables/Receivables<br>- Basic financial reports<br>- Enhanced sync |
| 4 | Reports + MVP Packaging | HKD tax reports + Financial reports + Docker | All Customers | **⏳ PENDING** | - Complete tax reports<br>- Financial statements<br>- Docker deployment<br>- User manuals<br>- Sales demo kit |

---

## 📋 DETAILED 4-PHASE BREAKDOWN

### **PHASE 1: CORE ACCOUNTING ENGINE (FOUNDATION)** ✅ **COMPLETED**
**Timeline:** Week 1 (Day 1-7)
**Objective:** Build immutable accounting foundation

#### Phase 1.1: Domain Design & Value Objects ✅
- ✅ AccountingEntry Immutable Pattern (5-Layer Protection)
- ✅ Value Objects: AccountingEntryId, TenantId, AccountingBookType, AccountingPeriod
- ✅ Clean Architecture with pure domain entities

#### Phase 1.2: Repository Pattern ✅
- ✅ IAccountingEntryRepository + Implementation
- ✅ Only Add/Get operations (no Update/Delete)
- ✅ Multi-tenancy enforcement

#### Phase 1.3: Domain Services ✅
- ✅ JournalEntryService (validation, balance checking)
- ✅ JournalTemplateService (parameter replacement)
- ✅ Business logic moved from domain entities

#### Phase 1.4: API Layer ✅
- ✅ Only POST endpoints (no PUT/DELETE)
- ✅ Immutable design compliance
- ✅ Zero-error build achieved

---

### **PHASE 2: ORDER FLOW + HKD ACCOUNTING** 🔄 **IN PROGRESS**
**Timeline:** Week 2 (Day 8-14)
**Objective:** Integrate accounting into sales flow

#### Phase 2.1: Repository Implementation ✅ **100% COMPLETED**
**Timeline:** Day 8-9 (Completed)
**Objective:** Complete data access foundation

#### Completed Repositories
```csharp
// ✅ JournalTemplateRepository + Implementation
public class JournalTemplateRepository : IJournalTemplateRepository
{
    // Template CRUD operations
    // Parameter replacement support
    // Multi-tenant isolation
    // Build Status: 0 errors
}

// ✅ OrderRepository + Implementation  
public class OrderRepository : IOrderRepository
{
    // Order CRUD operations
    // Date range queries
    // Status filtering
    // Build Status: 0 errors
}

// ✅ HKDBookRepository + Implementation
public class HKDBookRepository : IHKDBookRepository
{
    // 4 HKD books management
    // Book-specific queries
    // Summary reporting
    // Build Status: 0 errors
}
```

#### Phase 2.1 Achievements
- ✅ **Database Context Updated**: Added JournalTemplates and JournalEntries DbSets
- ✅ **Domain Model Updated**: AccountingBookType enum with 4 HKD books
- ✅ **Type Conversion Fixed**: TenantId and OrderStatusId comparison issues resolved
- ✅ **Dependency Injection**: All repositories registered and working
- ✅ **Build Validation**: 0 errors, guard check passed

#### Phase 2.2: Order to Accounting Integration ✅ **100% COMPLETED**
**Timeline:** Day 8-9 (Completed)
**Objective:** Auto accounting entry generation from orders

#### Completed Features
```csharp
// ✅ OrderService with accounting integration
public async Task<Order> CreateOrderAsync(Order order, Guid tenantId)
{
    // 1. Create order using repository
    var newOrder = await _orderRepository.AddAsync(order);
    
    // 2. Generate accounting entries
    await GenerateAccountingEntriesAsync(newOrder, tenant);
    
    return newOrder;
}

// ✅ Auto accounting entry generation
var revenueEntry = await _accountingService.CreateRevenueEntryAsync(
    tenantId, period, order.TotalPrice, $"Doanh thu bán hàng #{order.Id}");

// ✅ COGS calculation (70% of revenue for MVP)
var cogsEntry = await _accountingService.CreateExpenseEntryAsync(
    tenantId, period, cogsAmount, $"Giá vốn hàng bán #{order.Id}");

// ✅ HKD book integration
await _hkdBookRepository.AddToBookAsync(journalEntry, AccountingBookType.GeneralJournal);
await _hkdBookRepository.AddToBookAsync(journalEntry, AccountingBookType.GeneralLedger);
```

#### Phase 2.2 Achievements
- ✅ **OrderService Enhanced**: Accounting integration built-in
- ✅ **Auto Entry Generation**: Revenue and COGS entries created automatically
- ✅ **Vietnamese Compliance**: Standard account numbers (511, 111, 632, 156)
- ✅ **HKD Book Integration**: General Journal and General Ledger updates
- ✅ **Dependency Injection**: IOrderService registered and working
- ✅ **Build Validation**: 0 errors, guard check passed

#### Phase 2.3: 4 HKD Books Implementation 🔄 **NEXT**
- General Journal (Sổ Nhật ký chung) - Enhanced implementation
- General Ledger (Sổ Cái) - Enhanced implementation  
- Detailed Ledger (Sổ Chi tiết) - Full implementation
- Trial Balance (Sổ Tổng hợp) - Full implementation

#### Phase 2.3: 4 HKD Books Implementation ⏳ **PENDING**
- General Journal (Sổ Nhật ký chung)
- General Ledger (Sổ Cái)
- Detailed Ledger (Sổ Chi tiết)
- Trial Balance (Sổ Tổng hợp)

#### Phase 2.4: Offline SQLite Sync ⏳ **PENDING**
- Local-first accounting database
- Delta sync implementation
- Real-time SignalR updates

#### Phase 2.5: HKD Tax Reports ⏳ **PENDING**
- VAT report generation
- Personal income tax reports
- Excel/PDF export functionality

---

### **PHASE 3: TRADING/SERVICE COMPANY LITE** ⏳ **PENDING**
**Timeline:** Week 3 (Day 15-21)
**Objective:** Extend accounting for companies

#### Phase 3.1: Extended AccountingEntry ⏳ **PENDING**
- Accrual basis accounting
- AccountNumber value objects
- Double-entry bookkeeping
- Chart of Accounts for Vietnamese standards

#### Phase 3.2: Payables & Receivables ⏳ **PENDING**
- Accounts Receivable management
- Accounts Payable management
- Customer credit management
- Vendor management

#### Phase 3.3: Financial Statements ⏳ **PENDING**
- Balance Sheet (Bảng cân đối kế toán)
- Income Statement (Báo cáo kết quả hoạt động kinh doanh)
- Cash Flow Statement (Báo cáo lưu chuyển tiền tệ)

---

### **PHASE 4: PACKAGING & GO-TO-MARKET** ⏳ **PENDING**
**Timeline:** Week 4 (Day 22-28)
**Objective:** Prepare for deployment and sales

#### Phase 4.1: Docker Deployment ⏳ **PENDING**
- Docker Compose setup
- PostgreSQL production configuration
- Multi-container orchestration

#### Phase 4.2: Documentation & Training ⏳ **PENDING**
- User manuals
- API documentation
- Training materials
- Sales demo kit

#### Phase 4.3: Customer Acquisition ⏳ **PENDING**
- Demo environment
- Customer onboarding
- Support system
- First contract signing

---

## TARGET CUSTOMERS & USE CASES

### Week 1-2: Household Businesses (HKD)
**Target Profile:**
- Small shops, cafes, restaurants
- Revenue: 50M - 2B VND/year
- Staff: 1-10 employees
- Accounting needs: Simple tax compliance

**Key Use Cases:**
- Daily sales recording
- Expense tracking
- Monthly tax reporting (GTGT, TNCN)
- Basic inventory management

### Week 3-4: Small Companies
**Target Profile:**
- Trading companies, service providers
- Revenue: 2B - 20B VND/year
- Staff: 10-50 employees
- Accounting needs: Double-entry, financial statements

**Key Use Cases:**
- Accounts receivable/payable
- Double-entry bookkeeping
- Financial statements
- Tax compliance

---

## TECHNICAL ARCHITECTURE

### Core Components
- **Immutable Accounting Engine**: 5-layer protection
- **Multi-tenancy**: Complete data isolation
- **Offline-First**: SQLite + Delta sync
- **Real-time**: SignalR + KitchenHub
- **Reports**: Excel/PDF generation

### Technology Stack
- **Backend**: .NET 8, EF Core, PostgreSQL
- **Frontend**: Razor Pages, SignalR
- **Mobile**: Progressive Web App
- **Deployment**: Docker Compose
- **Quality**: Roslyn Analyzers, TDD

---

## COMPETITIVE ADVANTAGES

### Technical Advantages
1. **Immutable Accounting**: 100% audit trail
2. **Offline-First**: Works without internet
3. **Multi-tenancy**: Scales to 1000+ shops
4. **Real-time**: Live order processing
5. **Tax Compliance**: Built-in Vietnamese tax rules

### Business Advantages
1. **Quick Setup**: Deploy in 1 hour
2. **Mobile-First**: Works on phones/tablets
3. **Affordable**: 50% cheaper than competitors
4. **Vietnamese**: Localized for VN market
5. **Integration**: KitchenHub + POS integration

---

## RISK MITIGATION

### Technical Risks
| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Performance issues | Medium | High | Load testing, optimization |
| Data corruption | Low | Critical | Immutable design, backups |
| Sync conflicts | Medium | Medium | Conflict resolution strategy |
| Security breach | Low | Critical | Multi-tenancy, encryption |

### Business Risks
| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Market adoption | Medium | High | Free trial, demos |
| Competition | High | Medium | Faster time-to-market |
| Regulatory changes | Low | Medium | Flexible architecture |
| Customer churn | Medium | High | Continuous feedback |

---

## RESOURCE PLANNING

### Team Structure
- **User**: Business Owner, Requirements, Testing
- **Grok**: Business Architecture, Use Cases, Review
- **Windsurf**: Technical Implementation, Code Quality

### Time Allocation
- **Week 1**: 40 hours (Core Engine)
- **Week 2**: 40 hours (Order Flow)
- **Week 3**: 40 hours (Company Features)
- **Week 4**: 40 hours (Packaging)
- **Total**: 160 hours development time

---

## QUALITY STANDARDS

### Code Quality
- **Guard Check**: 95%+ pass rate
- **Test Coverage**: 80%+ Unit, 60%+ Integration
- **Build Time**: <2 minutes
- **Zero Critical Bugs**: Production-ready

### Business Quality
- **User Acceptance**: 100% requirements met
- **Tax Compliance**: 100% TT152 compliance
- **Performance**: <2 second response time
- **Documentation**: Complete user guides

---

## SUCCESS CRITERIA

### Week 1 Success
- [ ] Immutable AccountingEntry implemented
- [ ] 4 HKD books auto-generated
- [ ] Multi-tenancy working
- [ ] Basic tests passing

### Week 2 Success
- [ ] Order flow integrated
- [ ] Offline sync working
- [ ] HKD tax reports generated
- [ ] Real-time features working

### Week 3 Success
- [ ] Company accounting working
- [ ] Payables/Receivables implemented
- [ ] Financial reports generated
- [ ] Enhanced sync working

### Week 4 Success
- [ ] Complete tax compliance
- [ ] Docker deployment ready
- [ ] User manuals complete
- [ ] Sales demo kit ready
- [ ] First customer signed

---

## NEXT STEPS

### Immediate Actions (Week 1)
1. **Domain Design**: AccountingEntry immutable pattern
2. **Value Objects**: AccountingEntryId, BookType, Period
3. **EF Core Setup**: Entities, ValueConverters
4. **Repository Pattern**: Immutable operations only
5. **Multi-tenancy**: Tenant isolation

### Preparation Tasks
1. **Environment Setup**: Development, staging, production
2. **CI/CD Pipeline**: Automated testing, deployment
3. **Monitoring**: Logging, performance metrics
4. **Documentation**: API docs, user guides
5. **Sales Materials**: Demo scripts, pricing

---

## VERSION HISTORY

| Version | Date | Changes | Author |
|---------|------|---------|--------|
| 1.0 | 16/04/2026 | Initial Master Plan | Windsurf + User |
| 1.1 | - | - | - |

---

*This Master Plan is the foundation for successful MVP delivery. All team members must understand and commit to these objectives and timelines.*
