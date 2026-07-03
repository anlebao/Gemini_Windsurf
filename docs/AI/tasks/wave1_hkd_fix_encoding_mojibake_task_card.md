# TASK CARD: HKD Book Fix - Wave 1 - Fix UTF-8 Mojibake in `Services/Template/TemplateFactory.cs`

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Sửa chuỗi tiếng Việt bị hỏng encoding (UTF-8 → Latin-1 → UTF-8) trong `S1aHKDTemplateImpl` + `S2aHKDTemplateImpl` — header báo cáo sẽ là ký tự rác nếu không fix trước khi wire DI (Wave 3)
- **Nghiệp vụ áp dụng:** Encoding hygiene — block Wave 3 wire DI
- **Status:** PENDING — Planning & Approval
- **Branch:** `feature/hkd-fix-wave1-encoding-mojibake`
- **Estimated Sessions:** 0.5

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (IMPLEMENT phase — mechanical fix)
- **Execution Mode:** IMPLEMENT
- **Current Phase:** Wave 1 of 9
- **Dependency:** Wave 0 (pre-flight verification)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/hkd_book_accounting_fix_master_plan.md` (READ)
- `3_CoreHub/Services/Template/TemplateFactory.cs` (UPDATE — fix mojibake S1a + S2a TemplateImpl)

### Boundary Rules (Nghiêm cấm)
- KHÔNG sửa logic template — chỉ sửa string literal (TemplateName, DisplayName, GenerateReportAsync header)
- KHÔNG sửa S2b-S3a TemplateImpl (đã OK — chỉ verify)
- KHÔNG sửa `1_Shared/Domain/HKDTemplates.cs` (Domain layer — governance)
- KHÔNG thay đổi `Fields`/`Formula`/`Calculations` — chỉ sửa display strings
- KHÔNG thêm/bỏ using statements

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Encoding Purity:** Sau fix, grep `Ã|Â|á»|áº|á»|Ä` trong file → 0 matches
- [ ] **No Logic Change:** KHÔNG thay đổi `Formula`, `FieldName`, `TemplateCode`, `TargetGroup`
- [ ] **Build Check:** `dotnet build VanAn.sln` Release pass sau fix
- [ ] **String Match:** Sửa đúng chuỗi — compare với `1_Shared/Domain/HKDTemplates.cs` (bản Domain đã OK) để lấy correct Vietnamese

---

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** 0 mojibake string còn lại trong `Services/Template/TemplateFactory.cs` (grep `Ã|Â|á»|áº|á»|Ä` — no matches)
- [ ] **SC2:** `S1aHKDTemplateImpl.TemplateName` = "Sổ kế toán cho hộ kinh doanh không chịu thuế GTGT"
- [ ] **SC3:** `S2aHKDTemplateImpl.TemplateName` = "Sổ kế toán cho hộ kinh doanh nộp thuế GTGT và TNCN"
- [ ] **SC4:** `S1aHKDTemplateImpl.GenerateReportAsync` header = "SỔ KẾ TOÁN S1a_HKD"
- [ ] **SC5:** `S2aHKDTemplateImpl.GenerateReportAsync` header = "SỔ KẾ TOÁN S2a_HKD"
- [ ] **SC6:** DisplayName fields đúng tiếng Việt ("Tổng doanh thu", "Tổng chi phí", "Lợi nhuận", "Tiền thuế GTGT", "Thuế TNCN", "Doanh thu sau thuế")
- [ ] **SC7:** "VNĐ" thay "VNÄ" trong tất cả report strings
- [ ] **SC8:** `dotnet build VanAn.sln` Release — 0 errors
- [ ] **SC9:** guard-check.ps1 PASSED

---

## 6. ACTIVE SKILLS (MAX 3)
- `pattern-based-fixing` — Áp dụng cùng pattern fix cho S1a + S2a
- `build-error-analysis` — Fix build error nếu có

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 5
- **Verified Facts:**
  - Fact 1: `S1aHKDTemplateImpl` (L114-175) có mojibake: `"Sá» ké toÃ¡n cho há» kinh doanh khÃ´ng chá»u thuÃ© GTGT"`, `"Tá»ng doanh thu"`, `"Tá»ng chi phÃ­"`, `"Lá»i nhuáºn"`, `"VNÄ"`
  - Fact 2: `S2aHKDTemplateImpl` (L177-249) có mojibake: `"Sá» ké toÃ¡n cho há» kinh doanh ná»p thuÃ© GTGT vÃ  TNCN"`, `"Tiá»n thuÃ© GTGT"`, `"ThuÃ© TNCN"`, `"Doanh thu sau thuÃ©"`
  - Fact 3: `S2bHKDTemplateImpl` (L251-313) đã OK — `"Số doanh thu bán hàng hóa, dịch vụ"`, `"VNĐ"` (verify)
  - Fact 4: `1_Shared/Domain/HKDTemplates.cs` (S1a/S2a) đã OK — dùng làm reference cho correct Vietnamese
  - Fact 5: Mojibake pattern: UTF-8 bytes bị decode Latin-1 rồi encode UTF-8 (double-encoding)
- **Assumptions:**
  - S2c-S3a TemplateImpl đã OK (verify khi đọc)
- **Open Questions:**
  - Q1: Có tool auto-fix mojibake không? (Manual edit an toàn hơn cho 2 template)
- **Recommended Action:** PROCEED — risk thấp, chỉ sửa string literal

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `Services/Template/TemplateFactory.cs` (S1a + S2a TemplateImpl) | Header báo cáo sẽ hiển thị đúng tiếng Việt thay vì ký tự rác | Compare với Domain HKDTemplates.cs để lấy correct strings |

---

## 9. TDD & TESTING STRATEGY
- **Unit tests:** N/A — mechanical string fix
- **Integration tests:** N/A
- **E2E tests:** N/A
- **Verification:** `dotnet build VanAn.sln` Release pass + grep mojibake 0 matches

---

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: Manual edit per template
1. Đọc `1_Shared/Domain/HKDTemplates.cs` S1a (L7-95) → copy correct Vietnamese strings
2. Edit `Services/Template/TemplateFactory.cs` S1aHKDTemplateImpl (L114-175) → replace mojibake with correct strings
3. Đọc Domain S2a (L101-200) → copy correct strings
4. Edit TemplateFactory S2aHKDTemplateImpl (L177-249) → replace
5. Verify S2b-S3a (L251-658) — grep mojibake, confirm OK
6. Build + grep verify

### Micro-phase breakdown

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | - Đọc Domain HKDTemplates.cs S1a + S2a để lấy correct strings<br>- Chốt: list mojibake strings cần sửa per template | - Edit S1aHKDTemplateImpl (TemplateName, DisplayName x3, GenerateReportAsync header + 3 value labels + VNĐ)<br>- Edit S2aHKDTemplateImpl (TemplateName, DisplayName x4, GenerateReportAsync header + 4 value labels + VNĐ)<br>- Verify S2b-S3a (grep)<br>- Run `dotnet build VanAn.sln` Release<br>- Grep mojibake verify<br>- Commit |

### Rules
- 1 template tại 1 thời điểm — fix xong verify build trước khi sang template tiếp
- KHÔNG dùng `replace_all` blind — đọc context để không sửa S2b-S3a (đã OK)
- Compare từng string với Domain HKDTemplates.cs để đảm bảo correct

---

## 11. ESTIMATED EFFORT
- 0.5 session (2 template, ~15 string replacements, thao tác cơ học)
- **BLOCKER:** None — risk thấp nhất trong 9 waves (sau Wave 0)
- **PARALLEL:** Có thể làm cùng session với Wave 0 (cả 2 non-code/low-risk, độc lập)
