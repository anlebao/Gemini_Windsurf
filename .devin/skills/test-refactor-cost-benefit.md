---
description: Assess cost-benefit of test refactor options
---

# Test Refactor Cost-Benefit Skill

## Purpose
Evaluate test refactor options (Simplify vs Rewrite vs New) and assess impact on production code, time, effort, and risks.

## When to Use
- Deciding between test refactor approaches
- Evaluating whether to refactor production code for testability
- Assessing test strategy changes
- Planning test infrastructure improvements

## Key Activities

### 1. Option Analysis
Evaluate three main options:

**Simplify Tests:**
- Reduce test scope to essential assertions
- Remove duplicate coverage
- Focus on smoke tests
- Keep production code unchanged

**Rewrite Tests:**
- Complete test restructure
- Apply new test patterns
- Improve test architecture
- May require production code changes

**Create New Tests:**
- Add test coverage for gaps
- Implement new test types
- Extend test infrastructure
- Production code changes if needed

### 2. Cost Assessment
**Time Estimates:**
- Simplify: 1-2 hours (quick wins)
- Rewrite: 4-8 hours (significant effort)
- New: 2-6 hours (depends on scope)

**Effort Factors:**
- Test complexity
- Number of tests affected
- Production code changes required
- Infrastructure setup needed
- Documentation updates

**Maintenance Cost:**
- Long-term test maintenance
- Test flakiness risk
- Update frequency required
- Team knowledge transfer

### 3. Benefit Assessment
**Simplify Benefits:**
- Fast implementation
- Low risk
- Immediate value
- Easy to maintain
- No production code changes

**Rewrite Benefits:**
- Better test architecture
- Improved coverage
- Long-term maintainability
- Better test patterns
- Higher confidence

**New Test Benefits:**
- Fills coverage gaps
- Reduces risk
- Improves quality
- Enables new features
- Better documentation

### 4. Risk Assessment
**Simplify Risks:**
- Reduced test coverage
- May miss edge cases
- Limited business logic verification
- Dependency on other test types

**Rewrite Risks**
- Production code changes needed
- Time investment
- May introduce new bugs
- Team learning curve
- Temporary test instability

**New Test Risks:**
- Infrastructure setup
- Test flakiness
- Maintenance burden
- False sense of security
- Opportunity cost

### 5. Decision Framework
**Choose Simplify When:**
- Tests are failing due to infrastructure issues
- Duplicate coverage exists with other test types
- Maintenance cost exceeds value
- Production code refactor not justified
- Quick win needed
- Other test types cover business logic

**Choose Rewrite When:**
- Tests no longer match current business logic
- Test architecture needs improvement
- Better test patterns available
- Clear business value in improved tests
- Team has capacity
- Long-term investment justified

**Choose New When:**
- New features need test coverage
- Critical business logic lacks tests
- Risk areas identified
- User requests specific test coverage
- Compliance requirements
- Quality gates failing

### 6. Production Code Impact
Assess whether production code changes are justified:
- Is the test value high enough to justify refactor?
- Can tests be improved without production changes?
- Will production changes introduce risk?
- Is there a better alternative (e.g., test doubles, adapters)?
- What is the ROI of production code changes?

## Deliverables
- Cost-benefit analysis document
- Risk assessment
- Recommended approach with justification
- Production code impact analysis
- Implementation estimate

## Decision Template

```
Option: [Simplify/Rewrite/New]
Cost: [Time estimate, effort factors]
Benefits: [List of benefits]
Risks: [List of risks]
Production Code Impact: [Yes/No, details]
Recommendation: [Justify choice]
```

## Related Workflows
- `test-refactor-workflow.md` - Primary workflow for test refactor tasks
- `newfeaturebuild.md` - For new feature test planning

## Related Skills
- `test-strategy-planning` - For test strategy definition
- `pattern-based-fixing` - For implementing test patterns
- `test-system-upgrade` - For test system upgrades
