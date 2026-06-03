# REORGANIZED PHASE 2.3 PLAN - DYNAMIC HKD ARCHITECTURE PRIORITY
**Strategy:** Implement Dynamic HKD Book Architecture v3.0 first, then complete remaining phases
**Timeline:** 3-4 weeks (reorganized for maximum impact)
**Priority:** Production-ready dynamic system first

---

## STRATEGIC RATIONALE

### **WHY PRIORITIZE DYNAMIC HKD ARCHITECTURE?**

1. **Foundation First**: Formula Engine is core dependency for all HKD book generation
2. **Production Ready**: Dynamic HKD Architecture v3.0 has 9.5/10 architecture score
3. **Business Impact**: Enables template editing for accountants (key MVP feature)
4. **Technical Debt**: Eliminates need for static hardcoded book generation
5. **Scalability**: Supports 1000+ concurrent users from day 1

### **DEPENDENCY ANALYSIS:**
```
Dynamic HKD Architecture (Phase 2.3.5+)
    |
    |-- Formula Engine (2.3.5) - CORE DEPENDENCY
    |-- Data Provider (2.3.6) - NEEDED FOR DATA
    |-- Template System (2.3.8) - NEEDED FOR TEMPLATES
    |-- Business Rules (2.3.7) - NEEDED FOR COMPLIANCE
    |
    |-- THEN: Order Service Tests (2.3.3) - CAN USE REAL ENGINE
    |-- THEN: HKD Book Tests (2.3.4) - CAN TEST REAL GENERATION
    |-- THEN: Performance (2.3.9) - OPTIMIZE REAL SYSTEM
    |-- THEN: Compliance (2.3.10) - VALIDATE REAL SYSTEM
```

---

## REORGANIZED EXECUTION PLAN

### **WEEK 1: DYNAMIC HKD CORE FOUNDATION (Days 1-5)**

#### **DAY 1: PHASE 2.3.5.1 - FORMULA ENGINE CORE**
**Priority:** CRITICAL (Foundation for everything else)

**Tasks:**
- [ ] Create `ProductionFormulaEngine` with FINAL DSL syntax
- [ ] Implement DSL parser for `SUM_ACCOUNT("5*", "Credit")`
- [ ] Add expression evaluation engine
- [ ] Create variable context management
- [ ] Implement error handling for invalid formulas

**Files to Create:**
- `3_CoreHub/Services/Formula/ProductionFormulaEngine.cs`
- `3_CoreHub/Services/Formula/IFormulaEngine.cs`
- `6_Tests/VanAn.Core.Tests/Formula/ProductionFormulaEngineTests.cs`

**Success Criteria:**
- [ ] DSL syntax `SUM_ACCOUNT("5*", "Credit")` works
- [ ] Dependencies extracted correctly
- [ ] All unit tests pass
- [ ] Error handling implemented

#### **DAY 2: PHASE 2.3.5.2 - FORMULA FUNCTIONS**
**Priority:** CRITICAL (Business logic implementation)

**Tasks:**
- [ ] Implement SUM_ACCOUNT function
- [ ] Implement BALANCE_ACCOUNT function
- [ ] Implement PERCENTAGE function
- [ ] Implement RATIO function
- [ ] Add formula validation

**Files to Update:**
- `3_CoreHub/Services/Formula/ProductionFormulaEngine.cs`
- `6_Tests/VanAn.Core.Tests/Formula/ProductionFormulaEngineTests.cs`

#### **DAY 3: PHASE 2.3.6.1 - DATA PROVIDER CORE**
**Priority:** HIGH (Data access for formula engine)

**Tasks:**
- [ ] Create `DataProviderContext` record
- [ ] Implement `ScopedDataProvider` with context
- [ ] Add multi-tenant data isolation
- [ ] Add period-based data aggregation
- [ ] Implement caching strategy

**Files to Create:**
- `1_Shared/Domain/DataProviderContext.cs`
- `3_CoreHub/Services/Data/ScopedDataProvider.cs`
- `3_CoreHub/Services/Data/IDataProvider.cs`

#### **DAY 4: PHASE 2.3.6.2 - DATA AGGREGATION METHODS**
**Priority:** HIGH (Data processing for formulas)

**Tasks:**
- [ ] Create account balance aggregation
- [ ] Create transaction sum aggregation
- [ ] Create period comparison aggregation
- [ ] Create multi-period aggregation
- [ ] Add performance optimization

**Files to Update:**
- `3_CoreHub/Services/Data/ScopedDataProvider.cs`
- `6_Tests/VanAn.Core.Tests/Data/ScopedDataProviderTests.cs`

#### **DAY 5: PHASE 2.3.8.1 - TEMPLATE MANAGEMENT CORE**
**Priority:** HIGH (Template system for dynamic books)

**Tasks:**
- [ ] Create `HKDBookTemplate` model
- [ ] Implement `TemplateManager` service
- [ ] Add template versioning system
- [ ] Create template validation rules
- [ ] Add template inheritance

**Files to Create:**
- `1_Shared/Domain/HKDBookTemplate.cs`
- `3_CoreHub/Services/Template/TemplateManager.cs`
- `6_Tests/VanAn.Core.Tests/Template/TemplateManagerTests.cs`

---

### **WEEK 2: DYNAMIC HKD ADVANCED FEATURES (Days 6-10)**

#### **DAY 6: PHASE 2.3.8.2 - DEFAULT TEMPLATES**
**Priority:** HIGH (Ready-to-use templates)

**Tasks:**
- [ ] Create General Journal template
- [ ] Create Ledger template
- [ ] Create Detailed Ledger template
- [ ] Create Trial Balance template
- [ ] Add template validation tests

**Files to Create:**
- `3_CoreHub/Services/Template/Templates/GeneralJournalTemplate.cs`
- `3_CoreHub/Services/Template/Templates/LedgerTemplate.cs`
- `3_CoreHub/Services/Template/Templates/DetailedLedgerTemplate.cs`
- `3_CoreHub/Services/Template/Templates/TrialBalanceTemplate.cs`

#### **DAY 7: PHASE 2.3.7.1 - BUSINESS RULE REGISTRY**
**Priority:** HIGH (Vietnamese compliance)

**Tasks:**
- [ ] Create `BusinessRuleRegistry` core
- [ ] Implement rule evaluation engine
- [ ] Add rule versioning system
- [ ] Implement rule inheritance
- [ ] Create rule storage and retrieval

**Files to Create:**
- `3_CoreHub/Services/BusinessRules/BusinessRuleRegistry.cs`
- `3_CoreHub/Services/BusinessRules/IBusinessRuleRegistry.cs`
- `6_Tests/VanAn.Core.Tests/BusinessRules/BusinessRuleRegistryTests.cs`

#### **DAY 8: PHASE 2.3.7.2 - VIETNAMESE ACCOUNTING RULES**
**Priority:** HIGH (Compliance requirements)

**Tasks:**
- [ ] Implement double-entry bookkeeping rules
- [ ] Add VAT calculation rules
- [ ] Create period closing rules
- [ ] Add compliance validation rules
- [ ] Create Vietnamese accounting standards

**Files to Create:**
- `3_CoreHub/Services/BusinessRules/VietnameseAccountingRules.cs`
- `3_CoreHub/Services/BusinessRules/VATCalculationRules.cs`
- `3_CoreHub/Services/BusinessRules/PeriodClosingRules.cs`

#### **DAY 9: PHASE 2.3.5.3 - COMPILED EXPRESSION ENGINE**
**Priority:** MEDIUM (Performance optimization)

**Tasks:**
- [ ] Create `CompiledExpressionEngine`
- [ ] Implement NCalc integration
- [ ] Add expression caching
- [ ] Create performance benchmarks
- [ ] Validate compiled performance

**Files to Create:**
- `3_CoreHub/Services/Formula/CompiledExpressionEngine.cs`
- `6_Tests/VanAn.Core.Tests/Formula/CompiledExpressionEngineTests.cs`
- `6_Tests/VanAn.Core.Tests/Performance/ExpressionPerformanceTests.cs`

#### **DAY 10: PHASE 2.3.9.1 - CACHING LAYER**
**Priority:** MEDIUM (Performance optimization)

**Tasks:**
- [ ] Create `MemoryTemplateCache`
- [ ] Implement `BookResultCache`
- [ ] Add cache invalidation
- [ ] Create cache performance tests
- [ ] Validate cache hit rates

**Files to Create:**
- `3_CoreHub/Services/Cache/MemoryTemplateCache.cs`
- `3_CoreHub/Services/Cache/BookResultCache.cs`
- `6_Tests/VanAn.Core.Tests/Cache/CachePerformanceTests.cs`

---

### **WEEK 3: INTEGRATION & TESTING (Days 11-15)**

#### **DAY 11: PHASE 2.3.3 - ORDER SERVICE INTEGRATION TESTS**
**Priority:** HIGH (Now using real formula engine)

**Tasks:**
- [ ] Create OrderServiceIntegrationTests.cs with real formula engine
- [ ] Test order-to-accounting flow with dynamic calculations
- [ ] Test business rule validation with real rules
- [ ] Test multi-tenant order processing
- [ ] Test error handling with real system

**Files to Create:**
- `6_Tests/VanAn.Core.Tests/Services/OrderServiceIntegrationTests.cs`
- `6_Tests/VanAn.Core.Tests/Integration/OrderToAccountingFlowTests.cs`

#### **DAY 12: PHASE 2.3.4 - HKD BOOKS GENERATION TESTS**
**Priority:** HIGH (Now testing real generation)

**Tasks:**
- [ ] Create HKDBookGenerationTests.cs with real templates
- [ ] Test 4 HKD book types with dynamic templates
- [ ] Test template-based calculations
- [ ] Test multi-tenancy HKD book generation
- [ ] Create performance benchmarks

**Files to Create:**
- `6_Tests/VanAn.Core.Tests/HKD/HKDBookGenerationTests.cs`
- `6_Tests/VanAn.Core.Tests/Performance/HKDBookPerformanceTests.cs`

#### **DAY 13: PHASE 2.3.9.2 - PERFORMANCE OPTIMIZATION**
**Priority:** MEDIUM (Optimize real system)

**Tasks:**
- [ ] Implement batch processing for large datasets
- [ ] Add parallel calculation execution
- [ ] Optimize memory usage
- [ ] Add I/O optimization
- [ ] Create load testing scenarios

**Files to Update:**
- `3_CoreHub/Services/Formula/ProductionFormulaEngine.cs`
- `3_CoreHub/Services/Data/ScopedDataProvider.cs`

#### **DAY 14: PHASE 2.3.10 - COMPLIANCE VALIDATION**
**Priority:** MEDIUM (Validate real compliance)

**Tasks:**
- [ ] Implement Thông tu 200/2014/TT-BTC compliance
- [ ] Add Thông tu 152/2025/TT-BTC compliance
- [ ] Create tax authority reporting formats
- [ ] Add audit trail requirements
- [ ] Create compliance documentation

**Files to Create:**
- `3_CoreHub/Services/Compliance/VietnameseComplianceFramework.cs`
- `6_Tests/VanAn.Core.Tests/Compliance/ComplianceTests.cs`

#### **DAY 15: FINAL INTEGRATION & VALIDATION**
**Priority:** CRITICAL (Production readiness)

**Tasks:**
- [ ] Create end-to-end integration tests
- [ ] Validate complete system functionality
- [ ] Test with real tenant data
- [ ] Validate performance benchmarks
- [ ] Create production deployment checklist

**Files to Create:**
- `6_Tests/VanAn.Core.Tests/Integration/DynamicHKDSystemIntegrationTests.cs`
- `docs/Deployment/DynamicHKDProductionChecklist.md`

---

## UPDATED SUCCESS CRITERIA

### **WEEK 1 CRITERIA (Core Foundation):**
- [ ] Formula Engine with FINAL DSL syntax working
- [ ] Data Provider with multi-tenant isolation
- [ ] Template Management system functional
- [ ] All core components integrated

### **WEEK 2 CRITERIA (Advanced Features):**
- [ ] All 4 HKD book templates working
- [ ] Vietnamese business rules implemented
- [ ] Compiled expression engine working
- [ ] Caching layer implemented

### **WEEK 3 CRITERIA (Production Ready):**
- [ ] Order Service integration tests passing
- [ ] HKD book generation tests passing
- [ ] Performance benchmarks met
- [ ] Compliance validation passed
- [ ] End-to-end system working

---

## RISK MITIGATION

### **HIGH RISK MITIGATION:**
1. **Formula Engine Complexity**: Implement incrementally with extensive testing
2. **Performance Issues**: Early performance testing and optimization
3. **Compliance Gaps**: Continuous compliance validation

### **DAILY VALIDATION:**
- Daily build validation
- Component integration testing
- Performance monitoring
- Compliance checking

---

## BENEFITS OF REORGANIZED PLAN

### **IMMEDIATE BENEFITS:**
1. **Foundation First**: Core dependencies built before testing
2. **Real Testing**: Tests use actual production components
3. **Early Validation**: Dynamic system validated early
4. **Better Integration**: Components designed to work together

### **LONG-TERM BENEFITS:**
1. **Production Ready**: System built for scale from day 1
2. **Maintainable**: Clean architecture with proper dependencies
3. **Extensible**: Template system allows easy additions
4. **Compliant**: Vietnamese standards built-in

---

## NEXT STEPS

### **IMMEDIATE ACTION (TODAY):**
1. **Start Phase 2.3.5.1**: Implement Formula Engine Core
2. **Set up development environment**: Ensure NCalc and dependencies
3. **Create project structure**: Set up folders for new components
4. **Begin implementation**: ProductionFormulaEngine with FINAL DSL

### **THIS WEEK:**
1. **Complete Week 1**: Core foundation implementation
2. **Validate integration**: Ensure components work together
3. **Begin Week 2**: Advanced features implementation
4. **Continuous testing**: Daily build and validation

---

**This reorganized plan prioritizes the Dynamic HKD Book Architecture v3.0 implementation first, ensuring a solid foundation for all subsequent development and testing.**
