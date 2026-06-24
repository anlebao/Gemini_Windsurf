# TASK CARD: PRODUCTION_HYGIENE - WAVE13 - Audit Hardcoded Data

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Audit tất cả files với hardcoded data hoặc TODO comments
- **Nghiệp vụ áp dụng:** Data audit - identify hardcoded data that needs replacement

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `Data audit workflow`
- **Execution Mode:** INVESTIGATE_ONLY

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - Tất cả files trong repository (grep search only)
  - `docs/AI/tasks/PRODUCTION_HYGIENE_master_plan.md` (cập nhật status)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa code trong task này (đó là task W13-T2, W13-T4)
  - KHÔNG sửa configuration files
  - Chỉ audit và báo cáo

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Comprehensive Audit:** Grep search cho hardcoded data patterns
- [ ] **TODO Comments:** Search cho TODO comments về API calls
- [ ] **Template Data:** Identify template data acceptable for production
- [ ] **Documentation:** Document audit findings
- [ ] **Prioritization:** Prioritize by impact and complexity

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** Grep search hoàn thành cho hardcoded data patterns
- [ ] **SC2:** Grep search hoàn thành cho TODO comments
- [ ] **SC3:** Danh sách files với hardcoded data documented
- [ ] **SC4:** Danh sách files với TODO comments documented
- [ ] **SC5:** Prioritization list created (high/medium/low priority)
- [ ] **SC6:** PRODUCTION_HYGIENE_master_plan.md updated với W13-T1 status = ✅ DONE

**Implementation Date:** 2026-06-24
**Branch:** feature/wave13-replace-hardcoded-data

## 6. ACTIVE SKILLS (MAX 3)
- `pattern-based-fixing` — Identify hardcoded data patterns
- `domain-integrity-validation` — Verify data flow patterns
- `build-error-analysis` — Analyze potential impacts

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 2
- **Verified Facts:**
  - Fact 1: KhachLink Home.razor có TODO comment "Replace with actual API call"
  - Fact 2: OnboardingController có template data
- **Assumptions:**
  - Có thể có nhiều files với hardcoded data
- **Open Questions:**
  - Q1: Có bao nhiêu files với hardcoded data?
  - Q2: Template data trong OnboardingController acceptable không?
- **Recommended Action:** Comprehensive grep search for hardcoded data and TODO comments

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| N/A (audit only) | Không có reverse impact | N/A |

## 9. TDD & E2E TESTING STRATEGY
- **Audit Strategy:** Grep search for patterns like hardcoded arrays, TODO comments
- **Test boundary:**
  - Unit tests: N/A
  - Integration tests: N/A
  - E2E tests: N/A

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Comprehensive grep search for hardcoded data and TODO comments.

### Micro-phase breakdown cho WAVE13 - Audit Hardcoded Data

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Grep search for hardcoded data patterns | Search for hardcoded arrays, sample data, mock data |
| **S2** | Grep search for TODO comments | Search for TODO comments about API calls, data loading |
| **S3** | Analyze and prioritize findings | Create prioritized list of files to fix |
| **S4** | Update documentation | Update master plan status |

### Rules
- Search comprehensively across all file types
- Document both hardcoded data and TODO comments
- Prioritize by impact (production critical vs demo only)

## 11. ESTIMATED EFFORT
- Medium effort - comprehensive grep search + analysis
- 3 sessions theo JIT Planning
- **BLOCKER:** Không có blockers
