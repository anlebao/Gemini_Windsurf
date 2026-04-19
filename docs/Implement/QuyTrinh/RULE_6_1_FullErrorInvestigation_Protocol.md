# RULE 6.1 - FULL ERROR & WARNING INVESTIGATION PROTOCOL

**OFFICIAL DIRECTIVE - IMMEDIATE EFFECT**

---

## **Directive Information**

| **Field** | **Value** |
|-----------|-----------|
| **Directive ID** | Directive-20260418-FullErrorInvestigation-v1 |
| **Date** | April 18, 2026 |
| **Issued by** | Bão 1n - Project Owner (V1n An Ecosystem) |
| **Recipient** | Windsurf (AI Developer) |
| **Status** | MANDATORY - EFFECTIVE IMMEDIATELY |
| **Version** | v1.0 |

---

## **Purpose**

To prevent slow incremental fixing, repeated regressions (e.g., 560 â 480 errors), and inefficiency. The project requires fast, stable progress to support CS4B (Accounting & Tax Services for Organizations) and CS4C (Point System for V1n An Residents).

---

## **Mandatory Procedure for Every Error/Warning Report**

### **1. Full Investigation (MANDATORY)**

Always run the command to capture **ALL** errors and warnings:

```powershell
Select-String "error.*CS|warning.*CS" current_status.txt
```

**PROHIBITED:**
- Never use `Select-Object -First 5` or any command that limits the number of results
- Never investigate or fix only the first 5 errors
- Never truncate error output

### **2. Error Grouping & Analysis (MANDATORY)**

Use `Group-Object + Sort-Object Count -Descending` to group all errors:

```powershell
# Group errors by file and count frequency
Select-String "error.*CS" current_status.txt | ForEach-Object { $_ -replace ":.*", "" } | Group-Object | Sort-Object Count -Descending

# Group warnings by file and count frequency  
Select-String "warning.*CS" current_status.txt | ForEach-Object { $_ -replace ":.*", "" } | Group-Object | Sort-Object Count -Descending
```

**Required Reporting:**
- Total number of Errors
- Total number of Warnings
- Top 10 most frequent errors (with file name, line number, and description)
- Top 10 most frequent warnings (with file name, line number, and description)

### **3. Root Cause Analysis (MANDATORY)**

Identify the root cause(s) of all errors collectively:
- Analyze relationships and cascading effects between errors
- Identify common patterns and shared dependencies
- Determine if errors are related to:
  - Namespace issues
  - Type mismatches
  - Missing dependencies
  - Interface changes
  - Domain model inconsistencies

### **4. Proposed Solution (MANDATORY)**

Provide a comprehensive solution that addresses **ALL** errors and warnings together:
- Present a complete Merged Detailed Coding Plan or Recovery Plan for the entire set of issues
- Include step-by-step implementation strategy
- Consider impact analysis and dependencies
- Provide rollback strategy if needed

### **5. Validation (MANDATORY)**

After implementation, always run a full build and report:
- New total number of errors
- New total number of warnings
- Comparison with previous counts
- Any remaining issues requiring attention

---

## **Consequence of Violation**

Violation of this rule will be considered a breach of **WINDSURF RULES v5.0 - Rule 6 (No Bypass Allowed)** and may require:
- Full rollback of changes
- Re-evaluation of implementation approach
- Mandatory review and approval process for future changes

---

## **Integration with WINDSURF RULES v5.0**

This rule becomes **RULE 6.1** and is integrated into the existing WINDSURF RULES v5.0 framework:

```
## RULE 6: NO BYPASS ALLOWED (ZERO TOLERANCE)
- No stub files, no commenting out DbSets, no manual Version= in .csproj
- No #pragma disable, no <GenerateAssemblyInfo>false, no SuppressMessage
- **RULE 6.1: FULL ERROR & WARNING INVESTIGATION PROTOCOL (MANDATORY)**
  - Always investigate ALL errors and warnings at once
  - Never limit error investigation to partial results
  - Provide comprehensive analysis and solutions
```

---

## **Implementation Examples**

### **Correct Approach (MANDATORY):**

```powershell
# Step 1: Capture ALL errors
Select-String "error.*CS" current_status.txt > all_errors.txt
Select-String "warning.*CS" current_status.txt > all_warnings.txt

# Step 2: Group and analyze
Select-String "error.*CS" current_status.txt | ForEach-Object { $_ -replace ":.*", "" } | Group-Object | Sort-Object Count -Descending | Select-Object -First 10

# Step 3: Report comprehensive analysis
# Total: 480 errors, 12 warnings
# Top errors:
# 1. CS1061 (120 errors) - 'object' does not contain 'Should'
# 2. CS0117 (85 errors) - 'AccountingEntryFactory' does not contain definition
# 3. CS1729 (75 errors) - 'Money' constructor issues
# 4. CS8602 (45 errors) - Dereference of possibly null reference
# 5. CS8633 (35 errors) - Nullability constraints mismatch

# Step 4: Implement comprehensive solution
# Step 5: Validate full build
```

### **Prohibited Approach (VIOLATION):**

```powershell
# VIOLATION - Never do this:
Select-String "error.*CS" current_status.txt | Select-Object -First 5

# VIOLATION - Never do this:
# Reporting only 5 errors when there are 480 total
```

---

## **Quality Standards**

### **Minimum Requirements for Error Reports:**

1. **Complete Error Capture:** 100% of errors must be captured and analyzed
2. **Comprehensive Grouping:** Errors must be grouped by type, file, and frequency
3. **Root Cause Analysis:** Must identify underlying causes, not just symptoms
4. **Holistic Solutions:** Must address all errors systematically
5. **Full Validation:** Must verify complete resolution

### **Success Metrics:**

- Error reduction rate: Target >90% reduction per implementation cycle
- Regression prevention: Zero new errors introduced
- Resolution time: Complete analysis and implementation within single session
- Quality assurance: Full build validation with 0 errors, 0 warnings

---

## **Emergency Procedures**

### **If Error Count > 400:**

1. **Immediate Assessment:** Determine if critical regression occurred
2. **Strategic Recovery:** Consider rollback to stable state
3. **Incremental Stabilization:** Fix errors in logical groups
4. **Continuous Validation:** Monitor error count after each fix

### **If Error Count Increases:**

1. **Stop Implementation:** Halt current changes
2. **Root Cause Analysis:** Identify what caused the increase
3. **Rollback Strategy:** Revert to last stable state
4. **Revised Approach:** Develop new implementation strategy

---

## **Documentation Requirements**

All error investigation sessions must be documented with:

1. **Session Summary:** Date, time, error counts
2. **Analysis Report:** Complete error grouping and root cause analysis
3. **Implementation Plan:** Detailed step-by-step solution
4. **Validation Results:** Before/after error counts and build status
5. **Lessons Learned:** Insights for future error prevention

---

## **Approval Process**

### **For Error Counts > 200:**

1. **Pre-Implementation Review:** Present analysis and solution plan
2. **Stakeholder Approval:** Get explicit approval before implementation
3. **Progress Monitoring:** Regular updates during implementation
4. **Post-Implementation Review:** Validate complete resolution

### **For Critical Regressions (>100 error increase):**

1. **Immediate Stop:** Halt all current changes
2. **Emergency Review:** Full investigation of regression cause
3. **Recovery Plan:** Comprehensive rollback and recovery strategy
4. **Enhanced Oversight:** Additional review processes for future changes

---

## **Contact Information**

**Questions about this directive:**
- **Project Owner:** Bão 1n
- **Effective Date:** April 18, 2026
- **Review Date:** Monthly or as needed

**Implementation Support:**
- Reference this document for all error investigation procedures
- Update WINDSURF RULES v5.0 to include RULE 6.1
- Apply to all current and future error investigation tasks

---

**This directive is effective immediately and applies to all error investigation and fixing activities within the V1n An Ecosystem project.**

---

*Document Version: v1.0*  
*Last Updated: April 18, 2026*  
*Next Review: May 18, 2026*
