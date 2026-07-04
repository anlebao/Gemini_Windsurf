using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Services;

/// <summary>
/// W3 FIX-7: Tests for HkdToEnterpriseAccountMapper.
/// Covers task card verification checkboxes 6-7.
/// </summary>
public class HkdToEnterpriseAccountMapperTests
{
    private readonly HkdToEnterpriseAccountMapper _mapper = new();

    // W3-MP1: Task card checkbox 6
    [Theory]
    [InlineData(AccountingStandard.TT133_2016)]
    [InlineData(AccountingStandard.TT99_2025)]
    public void W3_MP1_MapToEnterpriseAccount_Revenue_Returns511(AccountingStandard standard)
    {
        string code = _mapper.MapToEnterpriseAccount("Revenue", standard);
        Assert.Equal("511", code);
    }

    // W3-MP2: Task card checkbox 7
    [Theory]
    [InlineData(AccountingStandard.TT133_2016)]
    [InlineData(AccountingStandard.TT99_2025)]
    public void W3_MP2_MapToEnterpriseAccount_Depreciation_Returns214(AccountingStandard standard)
    {
        string code = _mapper.MapToEnterpriseAccount("Depreciation", standard);
        Assert.Equal("214", code);
    }

    // W3-MP3: Unknown key throws KeyNotFoundException
    [Fact]
    public void W3_MP3_MapToEnterpriseAccount_UnknownKey_ThrowsKeyNotFound()
    {
        Assert.Throws<KeyNotFoundException>(() =>
            _mapper.MapToEnterpriseAccount("UnknownKey", AccountingStandard.TT133_2016));
    }
}
