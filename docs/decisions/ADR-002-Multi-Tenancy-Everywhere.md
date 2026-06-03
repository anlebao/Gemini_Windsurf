# ADR-002: Multi-Tenancy Everywhere

## Status

**Approved** (2026-06-01)

## Context

ShopERP là SaaS platform phục vụ nhiều quán/nhà hàng (tenants). Yêu cầu:

- **Data isolation**: Không tenant nào thấy data của tenant khác
- **Flexible business models**: Hỗ trợ cả Company và Household Business (HKD)
- **VAT compliance**: Mỗi tenant có VAT config riêng theo loại hình
- **Scalability**: Thêm tenant mới không ảnh hưởng existing tenants

Có 3 strategies multi-tenancy:
1. **Database-per-tenant**: Mỗi tenant database riêng
2. **Schema-per-tenant**: Shared database, schema riêng
3. **Row-level security**: Shared everything, TenantId column

## Decision

Sử dụng **Row-level security với TenantId mandatory trên mọi entity**.

```csharp
// Base entity pattern
public abstract class BaseEntity
{
    public Guid Id { get; protected set; }
    public TenantId TenantId { get; protected set; } // Required!
    // ... audit fields
}

// All entities inherit
public class Order : BaseEntity { }
public class Product : BaseEntity { }
public class AccountingEntry : BaseEntity { }
```

### Key Constraints

1. **TenantId is NOT nullable** - No global/shared data
2. **Query filter** - EF Core global filter: `.HasQueryFilter(e => e.TenantId == currentTenantId)`
3. **API validation** - Mọi request phải có TenantId (header hoặc JWT claim)
4. **Service layer** - `IMustHaveTenant` interface cho enforcement

### Tenant Types

```csharp
public enum BusinessType
{
    Company = 1,           // Doanh nghiệp
    HouseholdBusiness = 2  // Hộ kinh doanh
}

public enum HKDGroup
{
    Group1 = 1,  // Không chịu thuế GTGT
    Group2 = 2,  // Nộp thuế GTGT và TNCN
    Group3 = 3   // Chịu các loại thuế khác
}
```

## Consequences

### Positive

- [x] **Simple operations**: Single database, single schema
- [x] **Cross-tenant analytics**: Dễ dàng aggregate data (với permission)
- [x] **Cost efficient**: Shared infrastructure
- [x] **Easy onboarding**: New tenant = insert row, không cần provision database
- [x] **Backup simplicity**: Backup một database duy nhất

### Negative

- [ ] **Blast radius**: Bug có thể ảnh hưởng nhiều tenants
- [ ] **Query complexity**: Phải luôn filter by TenantId
- [ ] **Testing rigor**: Phải test cross-tenant data leakage
- [ ] **Performance**: Large table cần indexing strategy tốt

### Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| Data leakage | Critical | Automated tests, query filters, code review |
| Tenant isolation breach | Critical | Architecture tests, guard-check.ps1 |
| Noisy neighbor | Medium | Resource limits per tenant (future) |
| Migration complexity | Low | Tools để migrate tenant to dedicated (future) |

## Alternatives Considered

| Option | Pros | Cons | Decision |
|--------|------|------|----------|
| Database-per-tenant | Complete isolation | Complex ops, expensive | Rejected |
| Schema-per-tenant | Good isolation | Schema migration complexity | Rejected |
| Row-level security | Simple, scalable | Requires discipline | **Selected** |

## Implementation

- [x] `TenantId` value object
- [x] `BaseEntity` with mandatory TenantId
- [x] `IMustHaveTenant` interface
- [x] EF Core global query filters
- [x] Tenant resolution middleware
- [x] Architecture tests cho tenant isolation
- [ ] Tenant-specific resource limits (future)
- [ ] Tenant migration tools (future)

## Code Example

```csharp
// Domain enforcement
public interface IMustHaveTenant
{
    TenantId TenantId { get; }
}

// Service layer enforcement
public class OrderService
{
    public async Task<Order> GetOrderAsync(Guid orderId, TenantId tenantId)
    {
        // Query automatically filtered by TenantId
        return await _dbContext.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId);
    }
}

// Test enforcement
[Fact]
public async Task CannotAccessOtherTenantData()
{
    var tenantA = TenantId.New();
    var tenantB = TenantId.New();
    
    var order = await _service.GetOrderAsync(orderId, tenantA);
    
    // Should fail or return null for tenantB
}
```

## References

- `.windsurf/rules/.windsurfrules` - Lines về domain purity
- `.windsurf/skills/domain-integrity-validation.md`
- `1_Shared/Domain.cs` - Tenant implementation
- `6_Tests/VanAn.Architecture.Tests/` - Isolation tests

## Related

- ADR-001: SQLite local cũng cần TenantId
- ADR-003: Accounting entries per-tenant
- ADR-004: UI components tenant-aware

## Notes

- **Proposed by**: AI Assistant
- **Approved by**: User (implicit via roadmap approval)
- **Date**: 2026-06-01
- **Review cycle**: 6 months
- **Hard stop**: AI MUST refuse any Domain change that removes TenantId
