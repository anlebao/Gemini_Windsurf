# Hướng dẫn Sử dụng AI Context Management System

## Tổng quan

Context Management System giúp AI duy trì memory giữa các session bằng cách tự động load và update project context.

## Files Đã Tạo

1. **`.devin/rules/session_initialization.rules.md`** - Rules và command shortcuts
2. **`.devin/skills/session-initialization.md`** - Protocol load context
3. **`.devin/skills/session-update-state.md`** - Protocol update state

---

## Cách Sử Dụng

### 1. Bắt Đầu Session Mới

**Khi mở session mới cho implementation tasks:**

```
Bạn: skill session-initialization
```

**Hoặc AI tự động đọc (nếu được cấu hình):**
- AI đọc `PROJECT_CONTEXT.md` → hiểu architecture, ADRs, tech stack
- AI đọc `project_state.md` → hiểu current objective, next actions
- AI report context summary

**Output mong đợi:**
```
✅ Context Loaded

Current Objective: [từ project_state.md]
Active Mode: [ANALYZE/IMPLEMENT/FIX_ONLY/REVIEW_ONLY]
Current Branch: [từ git]
Key Constraints: [từ PROJECT_CONTEXT.md và .windsurfrules]
Next Actions: [từ project_state.md]
```

---

### 2. Trong Session

**Khi context đầy hoặc cần save progress:**

```
Bạn: /update-state
```

**Hoặc:**

```
Bạn: update-state
```

**AI sẽ:**
1. Đọc `project_state.md` hiện tại
2. Phân tích git diff để hiểu changes
3. Update các sections:
   - Section 2: Current Objective (nếu hoàn thành)
   - Section 3: Completed items
   - Section 4: Next Actions
   - Section 11: Last Updated + branch
4. Report summary

**Output mong đợi:**
```
✅ project_state.md Updated

Current Objective: [updated status]
Completed Items Added: [count]
Next Actions Updated: [count]
Last Updated: [timestamp]
Current Branch: [branch]

Files Modified in Session:
- [file1]
- [file2]
```

---

### 3. Kết Thúc Session Quan Trọng

**Sau khi hoàn thành feature/bug fix:**

```
Bạn: skill session-update-state
```

**AI sẽ:**
1. Analyze changes (git status, diff, log)
2. Update project_state.md với completed work
3. Move completed objective to Section 3
4. Update next actions
5. Update timestamp và branch

---

## Auto-Update Triggers

AI sẽ tự động đề nghị update state khi:

### Threshold-based:
- **5+ files modified** trong session
- **30+ minutes elapsed** trong session
- **Context length > 50%** limit
- **Completed workflow phase**

### Context Overflow:
- Khi context **> 80%** limit
- Trước khi context trở nên unmanageable

**AI sẽ đề nghị:**
```
⚠️ Context Warning: Session has accumulated significant changes.

Recommendation: Update project_state.md to save progress?
Type "yes" to invoke session-update-state, or "no" to continue.
```

---

## Context Overflow Protocol

Khi context sắp đầy:

```
1. STOP current work
2. INVOKE session-update-state
3. CLEAR non-essential context
4. CONTINUE với fresh context
5. REPORT summary
```

**Priority:** State update > continuing work khi context critical

---

## Workflow Đề Xuất

### Normal Flow:
```
Start Session → skill session-initialization
                ↓
            Do Work
                ↓
End Session → skill session-update-state
```

### Context Overflow Flow:
```
Start Session → skill session-initialization
                ↓
            Do Work
                ↓
[AI detects: 5+ files modified]
→ Auto-invoke session-update-state
→ Save progress to project_state.md
→ Clear non-essential context
→ Continue with fresh context
```

### User-Triggered:
```
User: /update-state
→ AI invokes session-update-state
→ Updates project_state.md
→ Reports summary
```

---

## Khi Nào Skip Context Loading

**Skip cho:**
- Simple read-only operations (file reading, grep, basic exploration)
- User explicitly requests: "skip context loading"
- Quick documentation lookups unrelated to implementation

---

## Maintenance

### Giữ project_state.md Updated:

**Sau mỗi session quan trọng:**
1. Invoke `session-update-state`
2. Review changes
3. Verify accuracy

**Manual update nếu cần:**
- Edit `docs/AI/project_state.md` trực tiếp
- Follow format từ Section 0 (Maintenance Rules)

### Update Skills/Rules:

**Khi cần thay đổi:**
- Edit corresponding `.devin/skills/` hoặc `.devin/rules/` files
- Test với session thực tế
- Verify AI hiểu và follow protocol

---

## Troubleshooting

### Skill không tìm thấy:

**Problem:** `skill session-initialization` báo "not found"

**Solution:** Skill tool không tìm thấy skills trong `.devin/skills/`. Manual invoke bằng cách:
1. Đọc skill file
2. Follow protocol thủ công
3. Hoặc request user configure skill discovery

### Encoding issues với project_state.md:

**Problem:** Edit tool fail do encoding/whitespace

**Solution:** Use Python script approach:
```python
# Read with error handling
with open('path/to/project_state.md', 'r', encoding='utf-8', errors='ignore') as f:
    content = f.read()

# Process and write back
with open('path/to/project_state.md', 'w', encoding='utf-8') as f:
    f.write(content)
```

### Context không load:

**Problem:** AI không tự động load context

**Solution:** 
1. Manual invoke: "Đọc PROJECT_CONTEXT.md và project_state.md"
2. Hoặc configure auto-load trong rules
3. Verify file paths đúng

---

## Best Practices

1. **Luôn load context** khi bắt đầu implementation task
2. **Update state** sau mỗi completed milestone
3. **Review changes** trước khi commit project_state.md
4. **Keep concise** - mỗi fact ghi đúng 1 nơi
5. **Ground truth first** - verify với codebase trước khi ghi
6. **Stamp every edit** - luôn update Section 11

---

## Examples

### Example 1: Bắt đầu session mới
```
Bạn: skill session-initialization
AI: ✅ Context Loaded
     Current Objective: AI Context Management System - ✅ COMPLETED (2026-06-16)
     Active Mode: NORMAL
     Current Branch: chore/fix-gitignore-dockerignore
     Key Constraints: Domain purity, Accounting immutability, UI Platform mandatory
     Next Actions: Integration Test Verification, KhachLink Integration
```

### Example 2: Update state giữa session
```
Bạn: /update-state
AI: ✅ project_state.md Updated
     Current Objective: AI Context Management System - ✅ COMPLETED (2026-06-16)
     Completed Items Added: 1
     Next Actions Updated: 0
     Last Updated: 2026-06-16
     Current Branch: chore/fix-gitignore-dockerignore
```

### Example 3: Context overflow
```
AI: ⚠️ Context Warning: 5+ files modified in session.
     Recommendation: Update project_state.md to save progress?
     
Bạn: yes
AI: ✅ project_state.md Updated
     [Summary report...]
     Context cleared. Ready to continue.
```

---

## Files Reference

- **Context Files:**
  - `docs/knowledge-base/00-core/PROJECT_CONTEXT.md` - Project overview, architecture, ADRs
  - `docs/AI/project_state.md` - Current objective, status, next actions

- **Configuration Files:**
  - `.devin/rules/session_initialization.rules.md` - Rules và triggers
  - `.devin/skills/session-initialization.md` - Load protocol
  - `.devin/skills/session-update-state.md` - Update protocol

- **Global Rules:**
  - `.devin/rules/.windsurfrules` - Core governance

---

*Version: 1.0*
*Last Updated: June 16, 2026*
*Status: Active*
