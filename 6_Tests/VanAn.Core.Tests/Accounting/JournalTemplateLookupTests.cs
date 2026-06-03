using Xunit;
using VanAn.CoreHub.Services;

namespace VanAn.Core.Tests.Accounting
{
    public class JournalTemplateLookupTests
    {
        [Fact]
        public void FindMatchingTemplate_ShouldReturnTemplate_WhenKeywordMatches()
        {
            // Arrange
            List<JournalTemplateItem> templates =
            [
                new JournalTemplateItem { Keyword = "bán hàng", Description = "Doanh thu bán hàng sản phẩm X" },
                new JournalTemplateItem { Keyword = "dịch vụ", Description = "Doanh thu cung cấp dịch vụ" },
                new JournalTemplateItem { Keyword = "vật liệu", Description = "Chi phí mua vật liệu sản xuất" }
            ];

            string userInput = "bán hàng";

            // Act
            JournalTemplateItem? matched = JournalTemplateLookupService.FindMatchingTemplate(userInput, templates);

            // Assert
            Assert.NotNull(matched);
            Assert.Equal("Doanh thu bán hàng sản phẩm X", matched.Description);
        }

        [Fact]
        public void FindMatchingTemplate_ShouldReturnNull_WhenNoMatch()
        {
            // Arrange
            List<JournalTemplateItem> templates =
            [
                new JournalTemplateItem { Keyword = "bán hàng", Description = "Doanh thu bán hàng" }
            ];

            string userInput = "khác";

            // Act
            JournalTemplateItem? matched = JournalTemplateLookupService.FindMatchingTemplate(userInput, templates);

            // Assert
            Assert.Null(matched);
        }

        [Fact]
        public void GetSuggestions_ShouldReturnMultipleMatches_WhenPartialKeyword()
        {
            // Arrange
            List<JournalTemplateItem> templates =
            [
                new JournalTemplateItem { Keyword = "bán hàng", Description = "Doanh thu bán hàng" },
                new JournalTemplateItem { Keyword = "bán lẻ", Description = "Doanh thu bán lẻ" },
                new JournalTemplateItem { Keyword = "bán buôn", Description = "Doanh thu bán buôn" }
            ];

            string userInput = "bán";

            // Act
            List<JournalTemplateItem> suggestions = JournalTemplateLookupService.GetSuggestions(userInput, templates);

            // Assert
            Assert.Equal(3, suggestions.Count);
        }
    }
}
