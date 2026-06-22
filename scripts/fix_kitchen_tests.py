"""
Fix KitchenServiceTests.cs:
For tests that create shopId = Guid.NewGuid(), change to use ActiveTenantId instead.
The global query filter filters by CurrentTenantId (= ActiveTenantId from TestTenantProvider),
so data must be seeded with the same TenantId to be visible through the filter.
"""

filepath = r'C:\VibeCoding\Gemini_Windsurf\6_Tests\VanAn.Core.Tests\KitchenServiceTests.cs'
with open(filepath, 'rb') as f:
    raw = f.read()
content = raw.decode('utf-8-sig')

# Test 1: GetGroupedItems_Should_GroupIdenticalProducts_FromDifferentOrders
# Replace: Guid shopId = Guid.NewGuid();
#          Guid customerId = Guid.NewGuid();
# With: Guid shopId = ActiveTenantId; // Use active tenant so global filter shows this data
#       Guid customerId = Guid.NewGuid();
old1 = (
    '            // Arrange\r\n'
    '            Guid shopId = Guid.NewGuid();\r\n'
    '            Guid customerId = Guid.NewGuid();\r\n'
    '\r\n'
    '            // Create shop\r\n'
    '            TenantId shopTenantId = new(shopId);'
)
new1 = (
    '            // Arrange\r\n'
    '            // Use ActiveTenantId so data is visible through the global multi-tenancy query filter.\r\n'
    '            Guid shopId = ActiveTenantId;\r\n'
    '            Guid customerId = Guid.NewGuid();\r\n'
    '\r\n'
    '            // Create shop\r\n'
    '            TenantId shopTenantId = new(shopId);'
)

count1 = content.count(old1)
content = content.replace(old1, new1)

with open(filepath, 'wb') as f:
    f.write(content.encode('utf-8'))

with open(r'C:\VibeCoding\Gemini_Windsurf\scripts\kitchen_test_result.txt', 'w', encoding='utf-8') as log:
    log.write(f'Test1 (GroupIdenticalProducts): replaced {count1}x\n')
    log.write(f'Remaining Guid.NewGuid() for shopId: {content.count("Guid shopId = Guid.NewGuid()")}\n')
