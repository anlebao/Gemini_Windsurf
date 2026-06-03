# ADR-004: UI Platform Mandatory

## Status

**Approved** (2026-06-01)

## Context

ShopERP có nhiều UI components rải rác, dẫn đến:

- **Inconsistent UX**: Mỗi trang có style khác nhau
- **Maintenance burden**: Fix bug phải sửa nhiều nơi
- **Accessibility issues**: Không đảm bảo WCAG compliance
- **Mobile responsiveness**: Khó đảm bảo consistent trên devices
- **AI coding efficiency**: AI tốn thời gian quyết định "dùng component nào"

Cần một **UI Platform** standardized để:
- Đảm bảo consistency
- Speed up development
- Enable AI to generate UI confidently
- Support accessibility out-of-the-box

## Decision

**Tất cả UI phải sử dụng `VanAn.UI.Platform`. Không custom CSS, không inline styles.**

```
UI.Platform/
├── Components/
│   ├── Atomic/          # VanAButton, VanAInput, VanALabel
│   ├── Base/            # VanACard, VanAModal, VanATable
│   └── Composite/       # VanAForm, VanAGrid, VanAChart
├── Models/
│   ├── ComponentModels.cs
│   └── DynamicFormModel.cs
└── Core/
    ├── Base/
    └── Interfaces/
```

### Component Hierarchy

1. **Atomic Components**: Button, Input, Label, Select
   - Không business logic
   - Pure presentation
   - Configurable qua parameters

2. **Base Components**: Card, Modal, Table, Alert
   - Layout containers
   - Common patterns
   - Built-in accessibility

3. **Composite Components**: Form, Grid, Chart
   - Business logic nhẹ
   - Connect với domain models
   - Dynamic form support

### Usage Rules

```razor
<!-- ✅ CORRECT: Use UI Platform -->
<VanAButton 
    Variant="Primary" 
    Size="Medium"
    OnClick="@HandleSubmit"
    Disabled="@isLoading">
    Save Order
</VanAButton>

<VanACard Title="Order Summary">
    <VanATable Items="@orders" Columns="@orderColumns" />
</VanACard>

<!-- ❌ WRONG: Custom HTML/CSS -->
<button class="my-custom-btn" style="background: blue;">Save</button>
<div class="card custom-card">...</div>
```

### Styling Strategy

- **Theme variables**: CSS custom properties qua `ThemeTypeExtensions`
- **Bootstrap adapter**: `VanAn.UI.Platform` wrap Bootstrap
- **No inline styles**: Styles qua component parameters
- **Responsive**: Grid system built-in

## Consequences

### Positive

- [x] **Consistency**: Mọi trang có cùng look & feel
- [x] **Faster development**: Pick components, không design from scratch
- [x] **Accessibility**: WCAG compliance built-in
- [x] **Mobile ready**: Responsive by default
- [x] **AI efficiency**: AI biết chính xác dùng component nào
- [x] **Easy updates**: Thay đổi theme = update Platform

### Negative

- [ ] **Learning curve**: Team cần học Platform API
- [ ] **Constraint creativity**: Không tùy ý custom design
- [ ] **Platform maintenance**: Cần maintain UI.Platform project
- [ ] **Edge cases**: Có thể thiếu component cho specific needs

### Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| Missing component | Medium | Extend Platform, không bypass |
| Resistance to change | Medium | Training, migration guide |
| Platform bugs | High | Unit tests cho components |
| Performance | Low | Lazy loading, virtualization |

## Alternatives Considered

| Option | Pros | Cons | Decision |
|--------|------|------|----------|
| Free-form CSS | Unlimited creativity | Inconsistent, maintenance nightmare | Rejected |
| Third-party UI lib | Rich components | Vendor lock-in, không fit domain | Rejected |
| Tailwind only | Modern, utility-first | Still inconsistent, learning curve | Rejected |
| Custom UI Platform | Perfect fit, controlled | Initial investment | **Selected** |

## Implementation

- [x] `VanAn.UI.Platform` project structure
- [x] Atomic components (Button, Input, Label, Select)
- [x] Base components (Card, Modal, Table, Alert)
- [x] Composite components (Form, Grid)
- [x] Theme system (light/dark)
- [x] Bootstrap adapter
- [ ] All remaining custom CSS migration
- [ ] Component documentation
- [ ] Visual regression tests

## Migration Path

```
Phase 1: All NEW features use UI Platform ✅
Phase 2: Refactor HIGH-TRAFFIC pages to UI Platform
Phase 3: Refactor REMAINING pages
Phase 4: Remove custom CSS files
```

### Code Example

```csharp
// Theme configuration
public enum ThemeType
{
    Light,
    Dark,
    HighContrast
}

// Component usage
<VanAForm Model="@orderModel" OnSubmit="@HandleSubmit">
    <VanAFormField 
        Label="Customer Name"
        @bind-Value="@orderModel.CustomerName"
        Validation="@ValidateName" />
    
    <VanAFormField 
        Label="Amount"
        Type="Number"
        @bind-Value="@orderModel.Amount" />
    
    <VanAButtonGroup>
        <VanAButton Variant="Secondary" OnClick="@Cancel">Cancel</VanAButton>
        <VanAButton Variant="Primary" Type="Submit">Save</VanAButton>
    </VanAButtonGroup>
</VanAForm>
```

## References

- `UI.Platform/` - Implementation
- `docs/LuuY_SuDungUI_Platform.txt` - Usage notes
- `docs/UI_Platform_Implementation_Guide.md` - Guide
- `.windsurf/skills/ui-platform-migration.md`
- `.windsurf/skills/ui-platform-compliance-review.md`

## Related

- ADR-001: Offline-first UI cần responsive
- ADR-002: Tenant-aware themes
- ADR-003: Accounting UI phải respect immutability
- ADR-005: Playwright selectors dựa trên UI Platform attributes

## Notes

- **Proposed by**: AI Assistant
- **Approved by**: User (implicit via roadmap approval)
- **Date**: 2026-06-01
- **Review cycle**: 6 months
- **Hard stop**: AI MUST refuse tạo custom CSS, redirect to UI Platform
- **Exception process**: Nếu thực sự cần component mới, extend Platform
