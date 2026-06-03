using Bunit;
using Xunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using VanAn.UI.Platform.Components.Composite;
using VanAn.UI.Platform.Core.Interfaces;
using VanAn.UI.Platform.Adapters;

namespace VanAn.Core.Tests.Accounting
{
    public class DynamicFormExtensionTests : TestContext
    {
        public DynamicFormExtensionTests()
        {
            Services.AddSingleton<ICssAdapter, BootstrapAdapter>();
        }

        [Fact]
        public void DynamicForm_ShouldRenderDateInput_WhenFieldTypeIsDate()
        {
            // Arrange
            List<FormField> fields =
            [
                new FormField { Id = "date", Label = "Ngày", Type = FieldType.Date }
            ];

            // Act
            IRenderedComponent<DynamicForm> cut = RenderComponent<DynamicForm>(parameters => parameters
                .Add(p => p.FieldDefinitions, fields)
                .Add(p => p.OnSubmit, EventCallback<FormData>.Empty));

            // Assert
            AngleSharp.Dom.IElement input = cut.Find("input[type='date']");
            Assert.NotNull(input);
            Assert.Equal("date", input.GetAttribute("id"));
        }

        [Fact]
        public void DynamicForm_ShouldRenderCurrencyInput_WhenFieldTypeIsCurrency()
        {
            // Arrange
            List<FormField> fields =
            [
                new FormField { Id = "amount", Label = "Số tiền", Type = FieldType.Currency }
            ];

            // Act
            IRenderedComponent<DynamicForm> cut = RenderComponent<DynamicForm>(parameters => parameters
                .Add(p => p.FieldDefinitions, fields)
                .Add(p => p.OnSubmit, EventCallback<FormData>.Empty));

            // Assert
            AngleSharp.Dom.IElement input = cut.Find("input[type='number']");
            Assert.NotNull(input);
            Assert.Equal("0.01", input.GetAttribute("step"));
            Assert.Equal("0", input.GetAttribute("min"));
        }

        [Fact]
        public void DynamicForm_ShouldRenderTextArea_WhenFieldTypeIsTextArea()
        {
            // Arrange
            List<FormField> fields =
            [
                new FormField { Id = "description", Label = "Diễn giải", Type = FieldType.TextArea }
            ];

            // Act
            IRenderedComponent<DynamicForm> cut = RenderComponent<DynamicForm>(parameters => parameters
                .Add(p => p.FieldDefinitions, fields)
                .Add(p => p.OnSubmit, EventCallback<FormData>.Empty));

            // Assert
            Assert.NotNull(cut.Find("textarea#description"));
        }

        [Fact]
        public void DynamicForm_ShouldRenderAllAccountingFields_WithCorrectTypes()
        {
            // Arrange — Full RevenueEntry field set
            List<FormField> fields =
            [
                new FormField { Id = "date",        Label = "Ngày",          Type = FieldType.Date },
                new FormField { Id = "amount",      Label = "Số tiền",        Type = FieldType.Currency },
                new FormField
                {
                    Id = "account",
                    Label = "Tài khoản",
                    Type = FieldType.Select,
                    Options =
                    [
                        new FieldOption { Value = "101", Label = "Tiền mặt" },
                        new FieldOption { Value = "102", Label = "Tiền gửi ngân hàng" }
                    ]
                },
                new FormField { Id = "description", Label = "Diễn giải",      Type = FieldType.TextArea },
                new FormField { Id = "reference",   Label = "Số chứng từ",    Type = FieldType.Text }
            ];

            // Act
            IRenderedComponent<DynamicForm> cut = RenderComponent<DynamicForm>(parameters => parameters
                .Add(p => p.FieldDefinitions, fields)
                .Add(p => p.OnSubmit, EventCallback<FormData>.Empty));

            // Assert
            Assert.NotNull(cut.Find("input[type='date']"));
            Assert.NotNull(cut.Find("input[type='number']"));
            Assert.NotNull(cut.Find("select#account"));
            Assert.NotNull(cut.Find("textarea#description"));
            Assert.NotNull(cut.Find("input[type='text']#reference"));
        }
    }
}
