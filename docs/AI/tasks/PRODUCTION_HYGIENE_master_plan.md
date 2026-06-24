# MASTER IMPLEMENTATION PLAN — Production Hygiene & Architecture Cleanup
# VanAn Ecosystem — Real Production Readiness

**Created:** 2026-06-24
**Last Updated:** 2026-06-24
**Current Status:** PLANNING — Branch `feature/wave7-prod-hardening` (Wave 7 IN PROGRESS)
**Branch strategy:** feature branch per wave → PR → merge vào `main`
**Execution principle:** Wave-by-wave sequential. Wave N không bắt đầu khi Wave N-1 chưa pass exit criteria. Mỗi wave là 1 PR độc lập.

---

## 0. EXECUTION RULES

### Session protocol
1. **Đọc `docs/AI/project_state.md` + task card của wave đang active TRƯỚC KHI viết bất kỳ dòng code nào.**
2. **Chạy `dotnet build VanAn.sln` trước khi bắt đầu và sau khi kết thúc session — 0 errors bắt buộc.**
3. **Chỉ sửa files nằm trong "Files được phép" của task card đang active — không drift sang module khác.**
4. **Sau mỗi micro-phase: commit intermediate, ghi rõ `[WaveX-SY]` trong commit message.**
5. **Nếu phát sinh compile error > 5: STOP, ghi vào investigation_log.md, hỏi user trước khi tiếp tục.**

### Branch protocol
```
main
    └── feature/wave7-prod-hardening     (Wave 7 — IN PROGRESS)
    └── feature/wave8-cleanup-dashboard   (Wave 8 — NEW)
    └── feature/wave9-cleanup-controller  (Wave 9 — NEW)
    └── feature/wave10-cleanup-interfaces (Wave 10 — NEW)
    └── feature/wave11-cleanup-tests      (Wave 11 — NEW)
```
- Mỗi wave tạo branch từ `main` (sau khi wave trước đã merge).
- KHÔNG merge wave sau khi wave trước chưa pass exit criteria.
- PR description phải link task card tương ứng.
- Squash merge để giữ lịch sử sạch.

### Hard rules (không violate)
- **Domain Layer Protection:** KHÔNG sửa `1_Shared/Domain.cs` để fix cleanup issues.
- **AccountingEntry Immutability:** Không ảnh hưởng tới immutable accounting entries trong bất kỳ wave nào.
- **Multi-tenancy:** Mọi thay đổi phải preserve `TenantId` filtering. Không bypass global query filter.
- **Architecture test phải PASS:** `6_Tests/VanAn.Architecture.Tests` phải green sau mỗi wave.
- **guard-check.ps1 phải PASS:** Chạy trước mỗi PR.

---

## 1. WAVE 8 — Cleanup Dashboard Security

**Branch:** `feature/wave8-cleanup-dashboard`
**Estimated sessions:** 2
**Priority:** 🔴 CRITICAL — Public dashboard với infrastructure control
**Conflict risk:** LOW — Chỉ sửa static HTML file, không đụng core flow
**Depends on:** Wave 7 (prod hardening) complete

### Vấn đề cụ thể cần fix
- `VanAn_Dashboard.html`: Zero authentication, public access
- `VanAn_Dashboard.html`: Hardcoded base IP `localhost`
- `VanAn_Dashboard.html`: Simulated Docker commands, build checks
- `VanAn_Dashboard.html`: Không có production security controls

### Quyết định kiến trúc
- **Option 1 (Khuyên dùng):** Xóa hoàn toàn `VanAn_Dashboard.html` — không cần thiết cho production
- **Option 2:** Move sang internal admin panel với proper authentication nếu cần
- **Decision:** Xóa file vì nó là development-only tool, không có production value

### Tasks
| # | Task ID | Task | Depends on | Task card | Status |
|---|---|---|---|---|---|
| 1 | W8-T1 | Xóa `VanAn_Dashboard.html` — development-only dashboard không phù hợp production | — | [W8-T1-card.md](#) | 📋 TODO |
| 2 | W8-T2 | Verify không có references đến `VanAn_Dashboard.html` trong codebase | W8-T1 | [W8-T2-card.md](#) | 📋 TODO |
| 3 | W8-T3 | Update documentation nếu có references đến dashboard | W8-T2 | [W8-T3-card.md](#) | 📋 TODO |

### Entry criteria (Wave 8)
- [ ] Branch `feature/wave8-cleanup-dashboard` tạo từ `main` mới nhất
- [ ] `dotnet build VanAn.sln` → 0 errors trên branch hiện tại
- [ ] Architecture tests: 7/7 PASS

### Exit criteria (Wave 8) — TẤT CẢ phải PASS trước khi merge
- [ ] `dotnet build VanAn.sln` → 0 errors, 0 warnings mới
- [ ] `guard-check.ps1` → PASS
- [ ] `VanAn.Architecture.Tests`: 7/7 PASS
- [ ] `VanAn.Integration.Tests`: không có test nào bị break thêm
- [ ] Verify: `VanAn_Dashboard.html` đã xóa khỏi repository
- [ ] Verify: Không có broken references trong codebase

### Why first
- Security critical issue — public infrastructure control
- Simple fix với low risk
- Clear trước khi tackle complex controller refactoring

---

## 2. WAVE 9 — Cleanup Orphan Controller

**Branch:** `feature/wave9-cleanup-controller`
**Estimated sessions:** 3
**Priority:** 🔴 CRITICAL — Security bypass + architecture violation
**Conflict risk:** HIGH — Đụng `ShopERP/Controllers/`, integration tests
**Depends on:** Wave 8 (dashboard cleanup) complete

### Vấn đề cụ thể cần fix
- `ShopERP/Controllers/CustomersController.cs`: `[AllowAnonymous]` trên tất cả endpoints
- `ShopERP/Controllers/CustomersController.cs`: Bypass service layer (DbContext trực tiếp)
- `ShopERP/Controllers/CustomersController.cs`: Không được production sử dụng
- `CustomerApiIntegrationTests`: Test ShopERP trực tiếp thay vì qua Gateway

### Quyết định kiến trúc
- **Decision:** Xóa `ShopERP/Controllers/CustomersController.cs` hoàn toàn
- **Rationale:** Controller là orphan code, KhachLink dùng Gateway, architecture violation
- **Tests:** Refactor `CustomerApiIntegrationTests` để test Gateway endpoints hoặc xóa nếu không cần

### Tasks
| # | Task ID | Task | Depends on | Task card | Status |
|---|---|---|---|---|---|
| 4 | W9-T1 | Xóa `ShopERP/Controllers/CustomersController.cs` | — | [W9-T1-card.md](#) | 📋 TODO |
| 5 | W9-T2 | Refactor `CustomerApiIntegrationTests` — test Gateway endpoints hoặc xóa tests | W9-T1 | [W9-T2-card.md](#) | 📋 TODO |
| 6 | W9-T3 | Verify không có references đến CustomersController trong codebase | W9-T1 | [W9-T3-card.md](#) | 📋 TODO |
| 7 | W9-T4 | Update `project_state.md` — remove backlog item, add to history | W9-T3 | [W9-T4-card.md](#) | 📋 TODO |

### Entry criteria (Wave 9)
- [ ] Wave 8 merged + `dotnet build` → 0 errors
- [ ] Branch `feature/wave9-cleanup-controller` tạo từ updated `main`
- [ ] Architecture tests: 7/7 PASS

### Exit criteria (Wave 9) — TẤT CẢ phải PASS
- [ ] `dotnet build VanAn.sln` → 0 errors, 0 warnings mới
- [ ] `guard-check.ps1` → PASS
- [ ] `VanAn.Architecture.Tests`: 7/7 PASS
- [ ] `VanAn.Integration.Tests`: tests updated hoặc removed, không có break
- [ ] Verify: `CustomersController.cs` đã xóa
- [ ] Verify: Không có broken references

### Why second
- High complexity với integration tests
- Need careful test refactoring
- Security critical but less urgent than public dashboard

---

## 3. WAVE 10 — Cleanup Duplicate Interfaces

**Branch:** `feature/wave10-cleanup-interfaces`
**Estimated sessions:** 2
**Priority:** 🟡 HIGH — Technical debt, confusion
**Conflict risk:** LOW — Chỉ xóa unused files, không đụng core flow
**Depends on:** Wave 9 (controller cleanup) complete

### Vấn đề cụ thể cần fix
- `ShopERP/Services/ISocialCampaignService.cs`: Duplicate interface, không được sử dụng
- `ShopERP/Services/ILoyaltyRewardsService.cs`: Duplicate interface, không được sử dụng
- CoreHub có interface và implementation, ShopERP có duplicate không dùng

### Quyết định kiến trúc
- **Decision:** Xóa cả 2 duplicate interface files
- **Rationale:** CoreHub interfaces là source of truth, ShopERP duplicates gây confusion
- **Impact:** Không có production impact vì không được sử dụng

### Tasks
| # | Task ID | Task | Depends on | Task card | Status |
|---|---|---|---|---|---|
| 8 | W10-T1 | Xóa `ShopERP/Services/ISocialCampaignService.cs` | — | [W10-T1-card.md](#) | 📋 TODO |
| 9 | W10-T2 | Xóa `ShopERP/Services/ILoyaltyRewardsService.cs` | — | [W10-T2-card.md](#) | 📋 TODO |
| 10 | W10-T3 | Verify không có references đến duplicate interfaces | W10-T1, W10-T2 | [W10-T3-card.md](#) | 📋 TODO |
| 11 | W10-T4 | Update `Program.cs` — remove DI registrations nếu có (verify) | W10-T3 | [W10-T4-card.md](#) | 📋 TODO |

### Entry criteria (Wave 10)
- [ ] Wave 9 merged + `dotnet build` → 0 errors
- [ ] Branch `feature/wave10-cleanup-interfaces` tạo từ updated `main`
- [ ] Architecture tests: 7/7 PASS

### Exit criteria (Wave 10) — TẤT CẢ phải PASS
- [ ] `dotnet build VanAn.sln` → 0 errors, 0 warnings mới
- [ ] `guard-check.ps1` → PASS
- [ ] `VanAn.Architecture.Tests`: 7/7 PASS
- [ ] `VanAn.Integration.Tests`: không có test nào bị break
- [ ] Verify: Duplicate interfaces đã xóa
- [ ] Verify: Không có broken references

### Why third
- Low risk, high value cleanup
- Reduces confusion and maintenance burden
- Quick win after complex controller cleanup

---

## 4. WAVE 11 — Cleanup Invalid Framework Files

**Branch:** `feature/wave11-cleanup-invalid-files`
**Estimated sessions:** 2
**Priority:** 🟡 HIGH — Broken code, cannot run
**Conflict risk:** LOW — Chỉ xóa/fix broken files, không đụng core flow
**Depends on:** Wave 10 (interface cleanup) complete

### Vấn đề cụ thể cần fix
- `SocialCampaignManager.cshtml`: Mix Razor Pages + Blazor syntax (invalid)
- `SocialCampaignManager.cshtml`: Hardcoded empty data, broken @code block
- `KhachLink/wwwroot/index.html`: Demo cũ, không được sử dụng
- `KhachLink/wwwroot/demoIndex.html`: Renamed demo, vẫn không cần

### Quyết định kiến trúc
- **Decision:** Xóa `SocialCampaignManager.cshtml` — cannot run, invalid syntax
- **Decision:** Xóa `KhachLink/wwwroot/index.html` và `demoIndex.html` — demo cũ
- **Rationale:** Files không thể chạy hoặc không được sử dụng, only add confusion

### Tasks
| # | Task ID | Task | Depends on | Task card | Status |
|---|---|---|---|---|---|
| 12 | W11-T1 | Xóa `ShopERP/Pages/SocialCampaignManager.cshtml` | — | [W11-T1-card.md](#) | 📋 TODO |
| 13 | W11-T2 | Xóa `KhachLink/wwwroot/index.html` | — | [W11-T2-card.md](#) | 📋 TODO |
| 14 | W11-T3 | Xóa `KhachLink/wwwroot/demoIndex.html` | — | [W11-T3-card.md](#) | 📋 TODO |
| 15 | W11-T4 | Verify không có references đến deleted files | W11-T1, W11-T2, W11-T3 | [W11-T4-card.md](#) | 📋 TODO |

### Entry criteria (Wave 11)
- [ ] Wave 10 merged + `dotnet build` → 0 errors
- [ ] Branch `feature/wave11-cleanup-invalid-files` tạo từ updated `main`
- [ ] Architecture tests: 7/7 PASS

### Exit criteria (Wave 11) — TẤT CẢ phải PASS
- [ ] `dotnet build VanAn.sln` → 0 errors, 0 warnings mới
- [ ] `guard-check.ps1` → PASS
- [ ] `VanAn.Architecture.Tests`: 7/7 PASS
- [ ] `VanAn.Integration.Tests`: không có test nào bị break
- [ ] Verify: Invalid files đã xóa
- [ ] Verify: Không có broken references

### Why fourth
- Cleanup broken code that cannot run
- Remove demo files that add confusion
- Low risk, high value cleanup

---

## 5. WAVE 12 — Fix API Authorization

**Branch:** `feature/wave12-api-authorization`
**Estimated sessions:** 3
**Priority:** 🔴 CRITICAL — Public API endpoints without auth
**Conflict risk:** MEDIUM — Đụng API endpoints, authentication flow
**Depends on:** Wave 11 (file cleanup) complete

### Vấn đề cụ thể cần fix
- `KhachLink/Pages/VoiceNote.razor`: `POST /api/orders/voice-note` không có authorization
- Gateway endpoints: Verify all API endpoints have proper authorization
- ShopERP endpoints: Verify authorization patterns consistent

### Quyết định kiến trúc
- **Decision:** Add `[Authorize]` attribute hoặc policy-based authorization cho voice note endpoint
- **Decision:** Audit all API endpoints for missing authorization
- **Pattern:** Gateway endpoints use `[Authorize(Policy = "RequireTenantAccess")]`, ShopERP use `[Authorize]`

### Tasks
| # | Task ID | Task | Depends on | Task card | Status |
|---|---|---|---|---|---|
| 16 | W12-T1 | Audit tất cả API endpoints trong Gateway cho authorization | — | [W12-T1-card.md](#) | 📋 TODO |
| 17 | W12-T2 | Audit tất cả API endpoints trong ShopERP cho authorization | W12-T1 | [W12-T2-card.md](#) | 📋 TODO |
| 18 | W12-T3 | Add `[Authorize]` hoặc policy-based auth cho voice note endpoint | W12-T2 | [W12-T3-card.md](#) | 📋 TODO |
| 19 | W12-T4 | Fix bất kỳ endpoints thiếu authorization theo audit results | W12-T3 | [W12-T4-card.md](#) | 📋 TODO |
| 20 | W12-T5 | Viết integration tests cho authorization enforcement | W12-T4 | [W12-T5-card.md](#) | 📋 TODO |

### Entry criteria (Wave 12)
- [ ] Wave 11 merged + `dotnet build` → 0 errors
- [ ] Branch `feature/wave12-api-authorization` tạo từ updated `main`
- [ ] Architecture tests: 7/7 PASS

### Exit criteria (Wave 12) — TẤT CẢ phải PASS
- [ ] `dotnet build VanAn.sln` → 0 errors, 0 warnings mới
- [ ] `guard-check.ps1` → PASS
- [ ] `VanAn.Architecture.Tests`: 7/7 PASS
- [ ] `VanAn.Integration.Tests`: authorization tests PASS
- [ ] Manual smoke: Verify voice note endpoint requires auth
- [ ] Manual smoke: Verify other protected endpoints reject unauthorized requests

### Why fifth
- Security critical but requires careful audit
- Need to understand existing auth patterns before fixing
- Higher complexity than simple file deletions

---

## 6. WAVE 13 — Replace Hardcoded Data

**Branch:** `feature/wave13-replace-hardcoded-data`
**Estimated sessions:** 4
**Priority:** 🟡 HIGH — Demo data, not production ready
**Conflict risk:** MEDIUM — Đụng UI components, data loading logic
**Depends on:** Wave 12 (authorization) complete

### Vấn đề cụ thể cần fix
- `KhachLink/Pages/Home.razor`: TODO comment "Replace with actual API call"
- Các file khác với hardcoded sample data (nếu có)
- Template data trong OnboardingController (có thể acceptable)

### Quyết định kiến trúc
- **Decision:** Implement real API calls cho KhachLink Home.razor
- **Decision:** Keep template data in OnboardingController (acceptable for onboarding)
- **Pattern:** Use existing CoreHub services via Gateway HTTP clients

### Tasks
| # | Task ID | Task | Depends on | Task card | Status |
|---|---|---|---|---|---|
| 21 | W13-T1 | Audit tất cả files với hardcoded data hoặc TODO comments | — | [W13-T1-card.md](#) | 📋 TODO |
| 22 | W13-T2 | Implement real API call cho KhachLink Home.razor products | W13-T1 | [W13-T2-card.md](#) | 📋 TODO |
| 23 | W13-T3 | Verify template data trong OnboardingController acceptable for production | W13-T1 | [W13-T3-card.md](#) | 📋 TODO |
| 24 | W13-T4 | Replace hardcoded data với real API calls (nếu cần) | W13-T2, W13-T3 | [W13-T4-card.md](#) | 📋 TODO |
| 25 | W13-T5 | Viết integration tests cho data loading | W13-T4 | [W13-T5-card.md](#) | 📋 TODO |

### Entry criteria (Wave 13)
- [ ] Wave 12 merged + `dotnet build` → 0 errors
- [ ] Branch `feature/wave13-replace-hardcoded-data` tạo từ updated `main`
- [ ] Architecture tests: 7/7 PASS

### Exit criteria (Wave 13) — TẤT CẢ phải PASS
- [ ] `dotnet build VanAn.sln` → 0 errors, 0 warnings mới
- [ ] `guard-check.ps1` → PASS
- [ ] `VanAn.Architecture.Tests`: 7/7 PASS
- [ ] `VanAn.Integration.Tests`: data loading tests PASS
- [ ] Manual smoke: Verify KhachLink Home loads real data
- [ ] Verify: Không còn TODO comments về API calls

### Why sixth
- Requires understanding data flow and API patterns
- Higher complexity, needs careful implementation
- Security must be in place first

---

## 7. SUMMARY & EXIT CRITERIA FOR ALL WAVES

### Overall Success Criteria
- [ ] Tất cả waves (8-13) merged vào `main`
- [ ] `dotnet build VanAn.sln` → 0 errors, 0 warnings
- [ ] `guard-check.ps1` → PASS
- [ ] `VanAn.Architecture.Tests`: 7/7 PASS
- [ ] `VanAn.Integration.Tests`: không có test bị break
- [ ] Manual smoke test: Verify security, data loading, API authorization
- [ ] Documentation updated: `project_state.md`, architecture docs

### Risk Mitigation
- **Low Risk Waves:** 8, 10, 11 (file deletions only)
- **Medium Risk Waves:** 9, 13 (test refactoring, data implementation)
- **High Risk Waves:** 12 (authorization changes)

### Rollback Plan
- Mỗi wave là independent PR → có thể rollback individual waves
- Nếu wave fail exit criteria → investigate → fix → retry hoặc skip wave
- Critical waves (8, 9, 12) must pass before production deployment

---

## 8. MAINTENANCE LOG

* **2026-06-24:** Plan created based on production hygiene analysis from chat session
* **Issues identified:** 10 major issues across security, architecture, data, testing
* **Planned waves:** 6 waves (8-13) to address all issues systematically
* **Estimated total sessions:** 16 sessions across 6 waves
* **Priority order:** Security → Architecture → Data → Testing
