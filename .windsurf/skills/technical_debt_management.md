---
description: Technical debt management patterns - classification, marking, remediation planning
---

# Technical Debt Management Skill

## Tier Classification

### Tier 1: Critical (Fix First)
**Characteristics:**
- Ảnh hưởng data integrity hoặc business logic cốt lõi
- Security implications (tenant isolation, auth)
- Production risk cao nếu không sửa

**Examples:**
- Fallback tenant IDs
- Bypass validation rules
- Hardcoded security credentials
- Race condition workarounds

**Remediation Priority:** Immediately sau khi Baseline stable

### Tier 2: Quality (Fix Later)
**Characteristics:**
- UX inconvenience hoặc code smell
- Không ảnh hưởng data/business logic
- Technical implementation debt

**Examples:**
- JS Interop workarounds cho binding
- Temporary UI states
- Refactor candidates
- Documentation gaps

**Remediation Priority:** Sau Tier 1, trong maintenance cycles

---

## Comment Marking Pattern

### Required Elements

```csharp
// TODO: [TECH DEBT] [VẠN AN PLATFORM HARDENING]
// 1. WHAT: Mô tả workaround đang làm gì
// 2. WHY: Lý do phải dùng workaround thay vì solution triệt để
// 3. FIX: Hướng dẫn sửa triệt để
// 4. TIER: 1 (Critical) hoặc 2 (Quality)
```

### Standard Template

```csharp
// TODO: [TECH DEBT] [VẠN AN PLATFORM HARDENING]
// {brief description}
// {root cause explanation}
// {remediation plan}
// Tier: {1|2} ({category} - {priority note})
```

---

## LEDGER.md Structure

```markdown
# Technical Debt Ledger - [Module Name]

## Tier 1: Critical
| # | File | Line | Description | Remediation |
|---|------|------|-------------|-------------|
| 1 | path | 100 | Fallback tenant | Add claim in Login.cshtml.cs |

## Tier 2: Quality
| # | File | Line | Description | Remediation |
|---|------|------|-------------|-------------|
| 2 | path | 200 | JS Interop | Use proper Blazor binding |

## Review Checklist
- [ ] All Tier 1 items resolved
- [ ] All Tier 2 items resolved
- [ ] E2E tests passing
```

---

## Decision Tree

```
New Bug Fix Required?
│
├── Can fix root cause immediately?
│   ├── YES → Fix properly, no debt
│   └── NO  → Apply workaround
│       │
│       ├── Is it data/business critical?
│       │   ├── YES → Tier 1 (Critical)
│       │   └── NO  → Tier 2 (Quality)
│       │
│       └── Mark with TECH DEBT comment
│           └── Add to LEDGER.md
│
└── Done
```

---

## Integration with Other Workflows

### With /Fix_Errors
1. Apply error fix (may include workarounds)
2. Run this skill to mark debt
3. Continue with validation

### With /newfeaturebuild
- During Steps 1-6: Workarounds allowed, must be marked
- During Step 7 (Validation): Verify all debt documented

### With /playwright_validation
- Ensure tests pass with current workarounds
- Add assertions to detect if workaround breaks

---

## Anti-Patterns to Avoid

❌ **Silent workarounds** - Không comment, không document  
❌ **Permanent temporaries** - Workaround tồn tại quá lâu không kế hoạch fix  
❌ **Missing tier** - Không phân loại priority  
❌ **Orphaned ledger** - LEDGER.md không được cập nhật khi sửa  

✅ **Clear markers** - TECH DEBT comments nổi bật  
✅ **Specific plans** - Remediation plan cụ thể, actionable  
✅ **Regular review** - LEDGER.md trong code review checklist  
