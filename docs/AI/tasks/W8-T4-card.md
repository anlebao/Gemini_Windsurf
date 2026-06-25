# TASK CARD: PRODUCTION_HYGIENE - WAVE8 - Update Dashboard to Sitemap with Links

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Update VanAn_Dashboard.html thành sitemap với links đến các modules
- **Nghiệp vụ áp dụng:** UI enhancement - convert dashboard thành production sitemap

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `UI update workflow`
- **Execution Mode:** FIX_ONLY

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `VanAn_Dashboard.html` (update thành sitemap)
  - `docs/AI/tasks/PRODUCTION_HYGIENE_master_plan.md` (cập nhật status)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa authentication logic (đã làm trong W8-T2, W8-T3)
  - KHÔNG sửa module URLs
  - Chỉ update UI thành sitemap

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Sitemap Structure:** Links đến KhachLink, ShopERP, Account, EInvoice, LoyaltyReward
- [ ] **UI Consistency:** Follow existing UI Platform patterns
- [ ] **Responsive Design:** Mobile-friendly sitemap
- [ ] **Accessibility:** WCAG compliant
- [ ] **Security:** Ensure sitemap respects authorization

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** Dashboard updated thành sitemap
- [ ] **SC2:** Links đến KhachLink, ShopERP, Account, EInvoice, LoyaltyReward added
- [ ] **SC3:** UI consistent with existing patterns
- [ ] **SC4:** Responsive design implemented
- [ ] **SC5:** Authorization respected (hide/show based on roles)
- [ ] **SC6:** PRODUCTION_HYGIENE_master_plan.md updated với W8-T4 status = ✅ DONE

**Implementation Date:** 2026-06-24
**Branch:** feature/wave8-upgrade-dashboard

## 6. ACTIVE SKILLS (MAX 3)
- `pattern-based-fixing` — Follow existing UI patterns
- `build-error-analysis` — Verify UI works correctly

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 2
- **Verified Facts:**
  - Fact 1: System has multiple modules (KhachLink, ShopERP, etc.)
  - Fact 2: Dashboard exists as HTML file
- **Assumptions:**
  - Module URLs are known
- **Open Questions:**
  - Q1: Module URLs là gì?
  - Q2: Icon cho từng module là gì?
- **Recommended Action:** Update dashboard thành sitemap với proper links

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| VanAn_Dashboard.html (update sitemap) | Dashboard UI changed | Maintain existing style, add sitemap features |

## 9. TDD & E2E TESTING STRATEGY
- **Verification Strategy:** Manual smoke test for sitemap
- **Test boundary:**
  - Unit tests: N/A
  - Integration tests: N/A
  - E2E tests: N/A

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES

### Chiến lược thực thi: JIT Planning + Pure Execution

Update dashboard thành sitemap, verify links work correctly.

### Micro-phase breakdown cho WAVE8 - Update to Sitemap

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Investigate module URLs | Check URLs for KhachLink, ShopERP, Account, EInvoice, LoyaltyReward |
| **S2** | Design sitemap UI | Design sitemap layout with icons and links |
| **S3** | Implement sitemap UI | Update VanAn_Dashboard.html with sitemap |
| **S4** | Add authorization-based visibility | Hide/show modules based on user roles |
| **S5** | Test and update documentation | Manual smoke test, update master plan status |

### Rules
- Follow existing UI Platform patterns
- Ensure responsive design
- Update documentation immediately after completion

## 11. ESTIMATED EFFORT
- Medium effort - UI update
- 4 sessions theo JIT Planning
- **BLOCKER:** Không có blockers
