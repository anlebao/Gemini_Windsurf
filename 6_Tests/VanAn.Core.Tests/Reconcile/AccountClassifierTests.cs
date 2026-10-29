using FluentAssertions;
using VanAn.CoreHub.Services.Reconcile;
using Xunit;

namespace VanAn.Core.Tests.Reconcile;

/// <summary>
/// P0 — AccountClassifier (tra soát số liệu kế toán, SRS G7): phân loại tài khoản
/// cho R3/R4/R7/R10. Case: T9 (911 kết chuyển → Temporary → R4 Warning) + T13
/// (malformed/rỗng → Unknown → R4 Warning) + phân nhóm đúng + HTX 512/612.
/// </summary>
public class AccountClassifierTests
{
    // ── IsValid (regex ^[1-9]\d{0,2}$) ──────────────────────────────────────
    [Theory]
    [InlineData("1")]
    [InlineData("111")]
    [InlineData("911")]
    [InlineData("999")]
    [InlineData("3331")]
    public void IsValid_ValidCodes_True(string code)
        => AccountClassifier.IsValid(code).Should().BeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("01")]
    [InlineData("11111")]
    [InlineData("ABC")]
    [InlineData("11A")]
    [InlineData(" 511")]
    [InlineData("511 ")]
    public void IsValid_InvalidOrMalformed_False(string code)
        => AccountClassifier.IsValid(code).Should().BeFalse();

    [Fact]
    public void IsValid_Null_False()
        => AccountClassifier.IsValid(null).Should().BeFalse();

    // ── Classify (phân nhóm theo prefix) ────────────────────────────────────
    [Theory]
    [InlineData("111", AccountGroup.Asset)]   // tiền mặt
    [InlineData("214", AccountGroup.Asset)]   // contra (hao mòn TSCĐ) — VẪN nhóm Asset (G7: không special-case)
    [InlineData("2294", AccountGroup.Asset)]  // contra (dự phòng giảm giá HTK)
    [InlineData("331", AccountGroup.Liability)]
    [InlineData("3387", AccountGroup.Liability)] // cọc nhận trước
    [InlineData("421", AccountGroup.Equity)]
    [InlineData("511", AccountGroup.Revenue)]
    [InlineData("512", AccountGroup.Revenue)] // HTX nội bộ
    [InlineData("711", AccountGroup.Revenue)]
    [InlineData("632", AccountGroup.Expense)]
    [InlineData("612", AccountGroup.Expense)] // HTX nội bộ
    [InlineData("642", AccountGroup.Expense)]
    [InlineData("811", AccountGroup.Expense)]
    public void Classify_ValidCodes_GroupByPrefix(string code, AccountGroup expected)
        => AccountClassifier.Classify(code).Should().Be(expected);

    // T9 — 911 kết chuyển (hiện production chưa dùng — nhưng phải phân loại đúng)
    [Theory]
    [InlineData("911", AccountGroup.Temporary)]
    [InlineData("999", AccountGroup.Temporary)]
    public void Classify_NineHundred_Codes_Temporary(string code, AccountGroup expected)
        => AccountClassifier.Classify(code).Should().Be(expected);

    // T13 — malformed/rỗng → Unknown
    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("01")]
    [InlineData("11111")]
    [InlineData("ABC")]
    public void Classify_Malformed_Unknown(string code)
        => AccountClassifier.Classify(code).Should().Be(AccountGroup.Unknown);

    [Fact]
    public void Classify_Null_Unknown()
        => AccountClassifier.Classify(null).Should().Be(AccountGroup.Unknown);

    // ── IsRecognizedForBalanceIdentity (R4: 1-8 hợp lệ; 9xx/Unknown → review) ─
    [Theory]
    [InlineData("111", true)]
    [InlineData("214", true)]   // contra vẫn được tính (G7)
    [InlineData("331", true)]
    [InlineData("421", true)]
    [InlineData("511", true)]
    [InlineData("632", true)]
    [InlineData("911", false)]  // T9 — 9xx kết chuyển → R4 Warning
    [InlineData("", false)]     // T13 — rỗng → R4 Warning
    [InlineData("ABC", false)]  // T13 — malformed → R4 Warning
    public void IsRecognizedForBalanceIdentity_MatchesGroups(string code, bool expected)
        => AccountClassifier.IsRecognizedForBalanceIdentity(code).Should().Be(expected);

    // ── Describe (suggestion) ───────────────────────────────────────────────
    [Fact]
    public void Describe_Temporary_911_HintsExpandNet()
        => AccountClassifier.Describe("911").Should().Contain("911 Xác định KQKD")
            .And.Contain("mở rộng NET gộp 9xx");

    [Fact]
    public void Describe_Unknown_Malformed_HintsDataCheck()
        => AccountClassifier.Describe("ABC").Should().Contain("không hợp lệ");

    [Fact]
    public void Describe_Valid_ReturnsGroup()
        => AccountClassifier.Describe("511").Should().Contain("Doanh thu");
}