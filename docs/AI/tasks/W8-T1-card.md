# TASK CARD: PRODUCTION_HYGIENE - WAVE8 - Design Sitemap Structure with Authentication

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Design sitemap structure với authentication cho VanAn_Dashboard.html
- **Nghiệp vụ áp dụng:** Security enhancement - upgrade dashboard thành production-ready sitemap

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `Design workflow`
- **Execution Mode:** ANALYZE

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `VanAn_Dashboard.html` (read only - analyze current structure)
  - `docs/AI/tasks/PRODUCTION_HYGIENE_master_plan.md` (cập nhật status)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa dashboard trong task này (đó là task W8-T4)
  - KHÔNG sửa authentication logic trong task này (đó là task W8-T2, W8-T3)
  - Chỉ design và document

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Sitemap Structure:** Design links đến KhachLink, ShopERP, Account, EInvoice, LoyaltyReward
- [ ] **Authentication Flow:** Design login page integration với ShopERP JWT
- [ ] **Authorization Model:** Design role-based access control cho từng module
- [ ] **UX Consistency:** Design consistent với existing UI Platform patterns
- [ ] **Security First:** Ensure proper session management and token handling

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** Sitemap structure documented
- [ ] **SC2:** Authentication flow designed
- [ ] **SC3:** Authorization model documented
- [ ] **SC4:** UI/UX design documented
- [ ] **SC5:** PRODUCTION_HYGIENE_master_plan.md updated với W8-T1 status = ✅ DONE

**Implementation Date:** 2026-06-24
**Branch:** feature/wave8-upgrade-dashboard

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Verify design consistent with architecture
- `pattern-based-fixing` — Follow existing authentication patterns

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 3
- **Verified Facts:**
  - Fact 1: VanAn_Dashboard.html exists as development tool
  - Fact 2: ShopERP has JWT authentication (Wave 0 completed)
  - Fact 3: System has RBAC (Waves 4-6 completed)
- **Assumptions:**
  - Can integrate with existing ShopERP authentication
  - Can use existing RBAC roles
- **Open Questions:**
  - Q1: Authentication flow should be redirect or modal?
  - Q2: Which roles should access which modules?
- **Recommended Action:** Design sitemap structure with authentication integration

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| N/A (design only) | Không có reverse impact | N/A |

## 9. TDD & E2E TESTING STRATEGY
- **Design Strategy:** Document structure, flow, and authorization model
- **Test boundary:**
  - Unit tests: N/A
  - Integration tests: N/A
  - E2E tests: N/A

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Analyze current dashboard, design sitemap structure, document authentication flow.

### Micro-phase breakdown cho WAVE8 - Design Sitemap

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Analyze current dashboard structure | Read VanAn_Dashboard.html, understand current UI |
| **S2** | Design sitemap structure | Document links to KhachLink, ShopERP, Account, EInvoice, LoyaltyReward |
| **S3** | Design authentication flow | Document login page integration with ShopERP JWT |
| **S4** | Design authorization model | Document role-based access control for each module |
| **S5** | Update documentation | Update master plan status |

### Rules
- Design before implementation
- Follow existing authentication patterns
- Document all decisions clearly

## 11. ESTIMATED EFFORT
- Medium effort - design and documentation
- 3 sessions theo JIT Planning
- **BLOCKER:** Không có blockers
