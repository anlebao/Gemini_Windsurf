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

## 4-WEEK MASTER PLAN

| Week | Phase Name | Main Objective | Primary Target | Status | Key Deliverables |
|------|------------|-----------------|----------------|--------|------------------|
| 1 | Core Accounting Engine | Build immutable AccountingEntry + 4 HKD books | HKD + Common Foundation | **Will Start** | - Immutable accounting core<br>- 4 HKD tax books<br>- Multi-tenancy<br>- Reversal-only pattern |
| 2 | Order Flow + HKD Accounting | Order flow -> Auto entries -> 4 HKD books + Sync | HKD | - | - Order-to-accounting integration<br>- Offline SQLite sync<br>- Real-time SignalR<br>- HKD tax reports |
| 3 | Company Lite + Sync | Basic company accounting + Delta offline-online sync | Trading/Service Co + HKD | - | - Extended accounting for companies<br>- Payables/Receivables<br>- Basic financial reports<br>- Enhanced sync |
| 4 | Reports + MVP Packaging | HKD tax reports + Financial reports + Docker | All Customers | - | - Complete tax reports<br>- Financial statements<br>- Docker deployment<br>- User manuals<br>- Sales demo kit |

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
