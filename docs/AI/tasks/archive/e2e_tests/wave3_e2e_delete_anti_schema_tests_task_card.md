# TASK CARD: E2E Cleanup - Wave 3 - Delete Anti-Schema Tests (Pattern G1)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Xóa 5 test cases hallucinate API response schema — chúng sẽ FAIL chắc chắn nếu chạy (schema mismatch, fabricated URL)
- **Nghiệp vụ áp dụng:** E2E test integrity — test phải match API response thật
- **Status:** PENDING — Planning & Approval
- **Branch:** `feature/e2e-cleanup-wave3-delete-anti-schema-tests`
- **Estimated Sessions:** 1

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT
- **Current Phase:** Wave 3 of 8
- **Dependency:** Wave 2 merged

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/e2e_test_cleanup_master_plan.md` (READ)
- `2_Gateway/Controllers/VoiceCommandController.cs` (READ — confirm schema `{ Success: bool }`)
- `6_Testing/e2e-tests/voice-command.spec.ts` (UPDATE — xóa 4 test)
- `6_Testing/e2e-tests/i18n.spec.ts` (UPDATE — xóa 1 test)

### Boundary Rules (Nghiêm cấm)
- KHÔNG sửa code C# — VoiceCommandController đã đúng, test sai
- KHÔNG fix test bằng cách sửa assertion để match schema — test đã verify FAKE, xóa luôn
- KHÔNG xóa test có thể pass (`TC_Voice_Flow`, `TC_i18n_Switch/Vietnamese/Fallback`)
- KHÔNG tạo test mới thay thế — out of scope (cần implement feature thật)

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Schema Verification:** VoiceCommandController L78 return `Ok(new { Success = commandResult })` — KHÔNG có `Command`/`Executed` fields
- [ ] **Fabricated URL:** `tts-api.example.com` là RFC 2606 reserved domain — API thật trả `/audio/speech.mp3` (L110)
- [ ] **Content-Language Header:** LocalizationMiddleware L45 set header — `TC_i18n_Switch/Vietnamese/Fallback` CÓ THỂ pass → giữ lại
- [ ] **Parse Check:** `npx playwright test --list` pass sau mỗi xóa

---

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `TC_Voice_TextCommand` đã xóa khỏi `voice-command.spec.ts`
- [ ] **SC2:** `TC_Voice_TTS` đã xóa
- [ ] **SC3:** `TC_Voice_StatusUpdate` đã xóa
- [ ] **SC4:** `TC_Voice_AudioStorage` đã xóa (try/catch swallow, không verify thực)
- [ ] **SC5:** `TC_i18n_VoiceLanguage` đã xóa khỏi `i18n.spec.ts`
- [ ] **SC6:** 0 reference đến `tts-api.example.com` còn lại trong codebase
- [ ] **SC7:** 0 reference đến `result.Command.CommandText` / `result.Command.CommandType` còn lại
- [ ] **SC8:** `TC_Voice_Flow` còn lại (sẽ fix Wave 6)
- [ ] **SC9:** `TC_i18n_Switch`, `TC_i18n_Vietnamese`, `TC_i18n_Fallback`, `TC_i18n_ProductNames` còn lại
- [ ] **SC10:** `npx playwright test --list` pass

---

## 6. ACTIVE SKILLS (MAX 3)
- `pattern-based-fixing` — Xóa test theo schema mismatch pattern
- `build-error-analysis` — Fix TS parse error nếu có

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 5
- **Verified Facts:**
  - Fact 1: VoiceCommandController.cs L78 `return Ok(new { Success = commandResult })` — chỉ 1 field
  - Fact 2: VoiceCommandController.cs L110 `string audioUrl = "/audio/speech.mp3"` — hardcoded dummy, không phải `tts-api.example.com`
  - Fact 3: VoiceCommandController.cs L181 `CleanupExpiredAudioFiles` return `CleanupResult { CleanedFiles, TotalExpired, Timestamp }` — struct OK nhưng test try/catch swallow
  - Fact 4: `tts-api.example.com` chỉ xuất hiện trong `voice-command.spec.ts` L169 (grep confirmed)
  - Fact 5: LocalizationMiddleware.cs L45 `context.Response.Headers.ContentLanguage = culture` — header CÓ set
- **Assumptions:**
  - `TC_i18n_Switch/Vietnamese/Fallback` có thể pass (Content-Language header set) — giữ lại
  - `TC_i18n_ProductNames` silent skip — sẽ fix Wave 6, giữ lại
- **Open Questions:**
  - Q1: `TC_Voice_AudioStorage` struct khớp nhưng try/catch swallow — xóa hay fix? (Recommend: xóa — try/catch che giấu failure, không có giá trị test)
- **Recommended Action:** PROCEED — xóa 5 test đã verify fake

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `voice-command.spec.ts` | Giảm từ 5 test → 1 test (`TC_Voice_Flow`) | Positive — xóa test vô giá trị |
| `i18n.spec.ts` | Giảm từ 5 test → 4 test | Positive — xóa test schema mismatch |

---

## 9. TDD & TESTING STRATEGY
- **Parse check:** `npx playwright test --list` sau mỗi xóa
- **Runtime check:** Skip
- **Verification:** `npx playwright test --list` pass

---

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược: Delete-Only
1. Đọc `voice-command.spec.ts` → identify 4 test blocks cần xóa
2. Xóa từng `test(...)` block (từ `test('TC_Voice_...` đến `});` đóng)
3. Verify `npx playwright test --list` pass
4. Lặp lại với `i18n.spec.ts` (1 test)

### Micro-phase breakdown cho Wave 3

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | - Confirm 4 test blocks trong voice-command (line ranges)<br>- Confirm 1 test block trong i18n<br>- Chốt: giữ `TC_Voice_Flow` + 4 test i18n còn lại | - Xóa 4 test trong voice-command.spec.ts<br>- Xóa 1 test trong i18n.spec.ts<br>- Run `npx playwright test --list`<br>- Commit |

### Rules
- Xóa toàn bộ `test(...)` block — từ `test('TC_...` đến `});` đóng
- KHÔNG xóa `import` statements nếu còn dùng
- Verify parse sau mỗi file

---

## 11. ESTIMATED EFFORT
- 0.5 session (5 test xóa, thao tác đơn giản)
- **BLOCKER:** None
