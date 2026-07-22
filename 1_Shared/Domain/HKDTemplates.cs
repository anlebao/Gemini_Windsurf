namespace VanAn.Shared.Domain
{
    /// <summary>
    /// S1a_HKD Template (Không chịu thuế GTGT, không nộp thuế TNCN)
    /// For HKD Group 1 businesses
    /// </summary>
    public record S1aHKDTemplate : HKDBookTemplate
    {
        public S1aHKDTemplate()
        {
            TemplateCode = "S1a_HKD";
            TemplateName = "Sổ kế toán cho hộ kinh doanh không chịu thuế GTGT";
            TargetGroup = HKDGroup.Group1;

            Fields =
            [
                new()
                {
                    FieldName = "TotalRevenue",
                    DisplayName = "Tổng doanh thu",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""5"", ""Credit"")"
                },
                new()
                {
                    FieldName = "TotalExpense",
                    DisplayName = "Tổng chi phí",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""6"", ""Debit"")"
                },
                new()
                {
                    FieldName = "NetProfit",
                    DisplayName = "Lợi nhuận",
                    Type = FieldType.Decimal,
                    Formula = "TotalRevenue - TotalExpense"
                }
            ];
        }

        public override async Task<GenericHKDBook> CreateBookAsync(
            TenantId tenantId,
            AccountingPeriod period,
            List<JournalEntry> entries)
        {
            GenericHKDBook book = new()
            {
                TenantId = tenantId,
                Period = period,
                BookTypeCode = TemplateCode,
                Template = this,
                Entries = entries
            };

            await book.CalculateAsync();
            await book.ValidateAsync();

            return book;
        }

        public override async Task CalculateAsync(GenericHKDBook book)
        {
            await Task.CompletedTask; // Formula engine handles everything
        }

        public override async Task ValidateAsync(GenericHKDBook book)
        {
            await Task.CompletedTask;
        }

        public override async Task<string> GenerateReportAsync(GenericHKDBook book)
        {
            string report = $"SỔ KẾ TOÁN S1a_HKD - {book.Period.Year}/{book.Period.Month:D2}\n";
            report += $"Hộ kinh doanh: {book.TenantId.Value}\n";

            if (book.NumericValues.TryGetValue("TotalRevenue", out decimal revenue))
            {
                report += $"Tổng doanh thu: {revenue:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("TotalExpense", out decimal expense))
            {
                report += $"Tổng chi phí: {expense:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("NetProfit", out decimal profit))
            {
                report += $"Lợi nhuận: {profit:N0} VNĐ\n";
            }

            return await Task.FromResult(report);
        }
    }

    /// <summary>
    /// S2a_HKD Template (Nộp thuế GTGT và TNCN theo tỷ lệ % trên doanh thu)
    /// For HKD Group 2 businesses.
    /// Wave 5 (TT 152/2025/TT-BTC): 4 industry groups × 3 fields (Revenue, VatAmount, PIT per group).
    /// Tax rates per Luật Thuế GTGT/TNCN sửa đổi 2025 + ND 117/2025.
    /// </summary>
    public record S2aHKDTemplate : HKDBookTemplate
    {
        public S2aHKDTemplate()
        {
            TemplateCode = "S2a_HKD";
            TemplateName = "Sổ kế toán cho hộ kinh doanh nộp thuế GTGT và TNCN";
            TargetGroup = HKDGroup.Group2;

            // Wave 5: 4 industry groups × 3 fields. NULL IndustrySector → OtherBusiness bucket.
            Fields =
            [
                // ── Distribution (GTGT 1%, TNCN 0.5%) ──
                new() { FieldName = "Revenue_Distribution", DisplayName = "Doanh thu — Phân phối", Type = FieldType.Decimal, IsRequired = true, Formula = @"SUM_ACCOUNT_BY_INDUSTRY(""5"", ""Credit"", ""Distribution"")" },
                new() { FieldName = "VatAmount_Distribution", DisplayName = "Thuế GTGT — Phân phối", Type = FieldType.Decimal, Formula = "Revenue_Distribution * 0.01" },
                new() { FieldName = "PIT_Distribution", DisplayName = "Thuế TNCN — Phân phối", Type = FieldType.Decimal, Formula = "Revenue_Distribution * 0.005" },
                // ── ProductionTransport (GTGT 3%, TNCN 1.5%) ──
                new() { FieldName = "Revenue_ProductionTransport", DisplayName = "Doanh thu — Sản xuất, vận tải", Type = FieldType.Decimal, IsRequired = true, Formula = @"SUM_ACCOUNT_BY_INDUSTRY(""5"", ""Credit"", ""ProductionTransport"")" },
                new() { FieldName = "VatAmount_ProductionTransport", DisplayName = "Thuế GTGT — Sản xuất, vận tải", Type = FieldType.Decimal, Formula = "Revenue_ProductionTransport * 0.03" },
                new() { FieldName = "PIT_ProductionTransport", DisplayName = "Thuế TNCN — Sản xuất, vận tải", Type = FieldType.Decimal, Formula = "Revenue_ProductionTransport * 0.015" },
                // ── Service (GTGT 5%, TNCN 2%) ──
                new() { FieldName = "Revenue_Service", DisplayName = "Doanh thu — Dịch vụ", Type = FieldType.Decimal, IsRequired = true, Formula = @"SUM_ACCOUNT_BY_INDUSTRY(""5"", ""Credit"", ""Service"")" },
                new() { FieldName = "VatAmount_Service", DisplayName = "Thuế GTGT — Dịch vụ", Type = FieldType.Decimal, Formula = "Revenue_Service * 0.05" },
                new() { FieldName = "PIT_Service", DisplayName = "Thuế TNCN — Dịch vụ", Type = FieldType.Decimal, Formula = "Revenue_Service * 0.02" },
                // ── OtherBusiness (GTGT 2%, TNCN 1%) — includes NULL IndustrySector entries ──
                new() { FieldName = "Revenue_OtherBusiness", DisplayName = "Doanh thu — Hoạt động khác", Type = FieldType.Decimal, IsRequired = true, Formula = @"SUM_ACCOUNT_BY_INDUSTRY(""5"", ""Credit"", ""OtherBusiness"")" },
                new() { FieldName = "VatAmount_OtherBusiness", DisplayName = "Thuế GTGT — Hoạt động khác", Type = FieldType.Decimal, Formula = "Revenue_OtherBusiness * 0.02" },
                new() { FieldName = "PIT_OtherBusiness", DisplayName = "Thuế TNCN — Hoạt động khác", Type = FieldType.Decimal, Formula = "Revenue_OtherBusiness * 0.01" },
                // ── Totals ──
                new() { FieldName = "TotalRevenue", DisplayName = "Tổng doanh thu", Type = FieldType.Decimal, IsRequired = true, Formula = "Revenue_Distribution + Revenue_ProductionTransport + Revenue_Service + Revenue_OtherBusiness" },
                new() { FieldName = "TotalVat", DisplayName = "Tổng thuế GTGT", Type = FieldType.Decimal, Formula = "VatAmount_Distribution + VatAmount_ProductionTransport + VatAmount_Service + VatAmount_OtherBusiness" },
                new() { FieldName = "TotalPIT", DisplayName = "Tổng thuế TNCN", Type = FieldType.Decimal, Formula = "PIT_Distribution + PIT_ProductionTransport + PIT_Service + PIT_OtherBusiness" },
                new() { FieldName = "NetRevenue", DisplayName = "Doanh thu sau thuế", Type = FieldType.Decimal, Formula = "TotalRevenue - TotalVat - TotalPIT" }
            ];
        }

        public override async Task<GenericHKDBook> CreateBookAsync(
            TenantId tenantId,
            AccountingPeriod period,
            List<JournalEntry> entries)
        {
            GenericHKDBook book = new()
            {
                TenantId = tenantId,
                Period = period,
                BookTypeCode = TemplateCode,
                Template = this,
                Entries = entries
            };

            await book.CalculateAsync();
            await book.ValidateAsync();

            return book;
        }

        public override async Task CalculateAsync(GenericHKDBook book)
        {
            await Task.CompletedTask;
        }

        public override async Task ValidateAsync(GenericHKDBook book)
        {
            await Task.CompletedTask;
        }

        public override async Task<string> GenerateReportAsync(GenericHKDBook book)
        {
            string report = $"SỔ KẾ TOÁN S2a_HKD - {book.Period.Year}/{book.Period.Month:D2}\n";
            report += $"Hộ kinh doanh: {book.TenantId.Value}\n";

            foreach (string sector in new[] { "Distribution", "ProductionTransport", "Service", "OtherBusiness" })
            {
                if (book.NumericValues.TryGetValue($"Revenue_{sector}", out decimal revenue))
                {
                    report += $"  Doanh thu {sector}: {revenue:N0} VNĐ\n";
                }

                if (book.NumericValues.TryGetValue($"VatAmount_{sector}", out decimal vat))
                {
                    report += $"  Thuế GTGT {sector}: {vat:N0} VNĐ\n";
                }

                if (book.NumericValues.TryGetValue($"PIT_{sector}", out decimal pit))
                {
                    report += $"  Thuế TNCN {sector}: {pit:N0} VNĐ\n";
                }
            }

            if (book.NumericValues.TryGetValue("TotalRevenue", out decimal totalRevenue))
            {
                report += $"Tổng doanh thu: {totalRevenue:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("TotalVat", out decimal totalVat))
            {
                report += $"Tổng thuế GTGT: {totalVat:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("TotalPIT", out decimal totalPit))
            {
                report += $"Tổng thuế TNCN: {totalPit:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("NetRevenue", out decimal net))
            {
                report += $"Doanh thu sau thuế: {net:N0} VNĐ\n";
            }

            return await Task.FromResult(report);
        }
    }

    /// <summary>
    /// S2b_HKD Template (Sổ doanh thu bán hàng hóa, dịch vụ)
    /// For HKD Group 2 businesses.
    /// Wave 5 (TT 152/2025/TT-BTC): 4 industry groups × 2 fields (Revenue, VatAmount per group).
    /// Split by industry sector (NOT goods-vs-service — that was a TT 200 hallucination).
    /// </summary>
    public record S2bHKDTemplate : HKDBookTemplate
    {
        public S2bHKDTemplate()
        {
            TemplateCode = "S2b_HKD";
            TemplateName = "Sổ doanh thu bán hàng hóa, dịch vụ";
            TargetGroup = HKDGroup.Group2;

            // Wave 5: 4 industry groups × 2 fields. NULL IndustrySector → OtherBusiness bucket.
            Fields =
            [
                // ── Distribution (GTGT 1%) ──
                new() { FieldName = "Revenue_Distribution", DisplayName = "Doanh thu — Phân phối", Type = FieldType.Decimal, IsRequired = true, Formula = @"SUM_ACCOUNT_BY_INDUSTRY(""5"", ""Credit"", ""Distribution"")" },
                new() { FieldName = "VatAmount_Distribution", DisplayName = "Thuế GTGT — Phân phối", Type = FieldType.Decimal, Formula = "Revenue_Distribution * 0.01" },
                // ── ProductionTransport (GTGT 3%) ──
                new() { FieldName = "Revenue_ProductionTransport", DisplayName = "Doanh thu — Sản xuất, vận tải", Type = FieldType.Decimal, IsRequired = true, Formula = @"SUM_ACCOUNT_BY_INDUSTRY(""5"", ""Credit"", ""ProductionTransport"")" },
                new() { FieldName = "VatAmount_ProductionTransport", DisplayName = "Thuế GTGT — Sản xuất, vận tải", Type = FieldType.Decimal, Formula = "Revenue_ProductionTransport * 0.03" },
                // ── Service (GTGT 5%) ──
                new() { FieldName = "Revenue_Service", DisplayName = "Doanh thu — Dịch vụ", Type = FieldType.Decimal, IsRequired = true, Formula = @"SUM_ACCOUNT_BY_INDUSTRY(""5"", ""Credit"", ""Service"")" },
                new() { FieldName = "VatAmount_Service", DisplayName = "Thuế GTGT — Dịch vụ", Type = FieldType.Decimal, Formula = "Revenue_Service * 0.05" },
                // ── OtherBusiness (GTGT 2%) — includes NULL IndustrySector entries ──
                new() { FieldName = "Revenue_OtherBusiness", DisplayName = "Doanh thu — Hoạt động khác", Type = FieldType.Decimal, IsRequired = true, Formula = @"SUM_ACCOUNT_BY_INDUSTRY(""5"", ""Credit"", ""OtherBusiness"")" },
                new() { FieldName = "VatAmount_OtherBusiness", DisplayName = "Thuế GTGT — Hoạt động khác", Type = FieldType.Decimal, Formula = "Revenue_OtherBusiness * 0.02" },
                // ── Totals ──
                new() { FieldName = "TotalRevenue", DisplayName = "Tổng doanh thu", Type = FieldType.Decimal, IsRequired = true, Formula = "Revenue_Distribution + Revenue_ProductionTransport + Revenue_Service + Revenue_OtherBusiness" },
                new() { FieldName = "TotalVat", DisplayName = "Tổng thuế GTGT", Type = FieldType.Decimal, Formula = "VatAmount_Distribution + VatAmount_ProductionTransport + VatAmount_Service + VatAmount_OtherBusiness" }
            ];
        }

        public override async Task<GenericHKDBook> CreateBookAsync(
            TenantId tenantId,
            AccountingPeriod period,
            List<JournalEntry> entries)
        {
            GenericHKDBook book = new()
            {
                TenantId = tenantId,
                Period = period,
                BookTypeCode = TemplateCode,
                Template = this,
                Entries = entries
            };

            await book.CalculateAsync();
            await book.ValidateAsync();

            return book;
        }

        public override async Task CalculateAsync(GenericHKDBook book)
        {
            await Task.CompletedTask;
        }

        public override async Task ValidateAsync(GenericHKDBook book)
        {
            await Task.CompletedTask;
        }

        public override async Task<string> GenerateReportAsync(GenericHKDBook book)
        {
            string report = $"SỔ DOANH THU S2b_HKD - {book.Period.Year}/{book.Period.Month:D2}\n";
            report += $"Hộ kinh doanh: {book.TenantId.Value}\n";

            foreach (string sector in new[] { "Distribution", "ProductionTransport", "Service", "OtherBusiness" })
            {
                if (book.NumericValues.TryGetValue($"Revenue_{sector}", out decimal revenue))
                {
                    report += $"Doanh thu {sector}: {revenue:N0} VNĐ\n";
                }

                if (book.NumericValues.TryGetValue($"VatAmount_{sector}", out decimal vat))
                {
                    report += $"Thuế GTGT {sector}: {vat:N0} VNĐ\n";
                }
            }

            if (book.NumericValues.TryGetValue("TotalRevenue", out decimal total))
            {
                report += $"Tổng doanh thu: {total:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("TotalVat", out decimal totalVat))
            {
                report += $"Tổng thuế GTGT: {totalVat:N0} VNĐ\n";
            }

            return await Task.FromResult(report);
        }
    }

    /// <summary>
    /// S2c_HKD Template (Sổ chi tiết doanh thu, chi phí)
    /// For HKD Group 2 businesses
    /// </summary>
    public record S2cHKDTemplate : HKDBookTemplate
    {
        public S2cHKDTemplate()
        {
            TemplateCode = "S2c_HKD";
            TemplateName = "Sổ chi tiết doanh thu, chi phí";
            TargetGroup = HKDGroup.Group2;

            Fields =
            [
                new()
                {
                    FieldName = "Revenue",
                    DisplayName = "Doanh thu",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""5"", ""Credit"")"
                },
                new()
                {
                    FieldName = "CostOfGoodsSold",
                    DisplayName = "Giá vốn hàng bán",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""632"", ""Debit"")"
                },
                new()
                {
                    FieldName = "OperatingExpenses",
                    DisplayName = "Chi phí hoạt động",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""641"", ""Debit"") + SUM_ACCOUNT(""642"", ""Debit"")"
                },
                new()
                {
                    FieldName = "NetProfit",
                    DisplayName = "Lợi nhuận",
                    Type = FieldType.Decimal,
                    Formula = "Revenue - CostOfGoodsSold - OperatingExpenses"
                }
            ];
        }

        public override async Task<GenericHKDBook> CreateBookAsync(
            TenantId tenantId,
            AccountingPeriod period,
            List<JournalEntry> entries)
        {
            GenericHKDBook book = new()
            {
                TenantId = tenantId,
                Period = period,
                BookTypeCode = TemplateCode,
                Template = this,
                Entries = entries
            };

            await book.CalculateAsync();
            await book.ValidateAsync();

            return book;
        }

        public override async Task CalculateAsync(GenericHKDBook book)
        {
            await Task.CompletedTask;
        }

        public override async Task ValidateAsync(GenericHKDBook book)
        {
            await Task.CompletedTask;
        }

        public override async Task<string> GenerateReportAsync(GenericHKDBook book)
        {
            string report = $"SỔ CHI TIẾT DOANH THU, CHI PHÍ S2c_HKD - {book.Period.Year}/{book.Period.Month:D2}\n";
            report += $"Hộ kinh doanh: {book.TenantId.Value}\n";

            if (book.NumericValues.TryGetValue("Revenue", out decimal revenue))
            {
                report += $"Doanh thu: {revenue:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("CostOfGoodsSold", out decimal cogs))
            {
                report += $"Giá vốn hàng bán: {cogs:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("OperatingExpenses", out decimal expenses))
            {
                report += $"Chi phí hoạt động: {expenses:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("NetProfit", out decimal profit))
            {
                report += $"Lợi nhuận: {profit:N0} VNĐ\n";
            }

            return await Task.FromResult(report);
        }
    }

    /// <summary>
    /// S2d_HKD Template (Sổ chi tiết vật liệu, dụng cụ, sản phẩm, hàng hóa)
    /// For HKD Group 2 businesses
    /// </summary>
    public record S2dHKDTemplate : HKDBookTemplate
    {
        public S2dHKDTemplate()
        {
            TemplateCode = "S2d_HKD";
            TemplateName = "Sổ chi tiết vật liệu, dụng cụ, sản phẩm, hàng hóa";
            TargetGroup = HKDGroup.Group2;

            Fields =
            [
                new()
                {
                    FieldName = "Materials",
                    DisplayName = "Vật liệu",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""152"", ""Debit"")"
                },
                new()
                {
                    FieldName = "Tools",
                    DisplayName = "Dụng cụ",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""153"", ""Debit"")"
                },
                new()
                {
                    FieldName = "Products",
                    DisplayName = "Sản phẩm",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""155"", ""Debit"")"
                },
                new()
                {
                    FieldName = "Goods",
                    DisplayName = "Hàng hóa",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""156"", ""Debit"")"
                },
                new()
                {
                    FieldName = "TotalInventory",
                    DisplayName = "Tổng tồn kho",
                    Type = FieldType.Decimal,
                    Formula = "Materials + Tools + Products + Goods"
                }
            ];
        }

        public override async Task<GenericHKDBook> CreateBookAsync(
            TenantId tenantId,
            AccountingPeriod period,
            List<JournalEntry> entries)
        {
            GenericHKDBook book = new()
            {
                TenantId = tenantId,
                Period = period,
                BookTypeCode = TemplateCode,
                Template = this,
                Entries = entries
            };

            await book.CalculateAsync();
            await book.ValidateAsync();

            return book;
        }

        public override async Task CalculateAsync(GenericHKDBook book)
        {
            await Task.CompletedTask;
        }

        public override async Task ValidateAsync(GenericHKDBook book)
        {
            await Task.CompletedTask;
        }

        public override async Task<string> GenerateReportAsync(GenericHKDBook book)
        {
            string report = $"SỔ CHI TIẾT VẬT LIỆU, DỤNG CỤ, SẢN PHẨM, HÀNG HÓA S2d_HKD - {book.Period.Year}/{book.Period.Month:D2}\n";
            report += $"Hộ kinh doanh: {book.TenantId.Value}\n";

            if (book.NumericValues.TryGetValue("Materials", out decimal materials))
            {
                report += $"Vật liệu: {materials:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("Tools", out decimal tools))
            {
                report += $"Dụng cụ: {tools:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("Products", out decimal products))
            {
                report += $"Sản phẩm: {products:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("Goods", out decimal goods))
            {
                report += $"Hàng hóa: {goods:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("TotalInventory", out decimal total))
            {
                report += $"Tổng tồn kho: {total:N0} VNĐ\n";
            }

            return await Task.FromResult(report);
        }
    }

    /// <summary>
    /// S2e_HKD Template (Sổ chi tiết tiền)
    /// For HKD Group 2 businesses
    /// </summary>
    public record S2eHKDTemplate : HKDBookTemplate
    {
        public S2eHKDTemplate()
        {
            TemplateCode = "S2e_HKD";
            TemplateName = "Sổ chi tiết tiền";
            TargetGroup = HKDGroup.Group2;

            Fields =
            [
                new()
                {
                    FieldName = "CashOnHand",
                    DisplayName = "Tiền mặt",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""111"", ""Debit"") - SUM_ACCOUNT(""111"", ""Credit"")"
                },
                new()
                {
                    FieldName = "BankDeposits",
                    DisplayName = "Tiền gửi ngân hàng",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""112"", ""Debit"") - SUM_ACCOUNT(""112"", ""Credit"")"
                },
                new()
                {
                    FieldName = "TotalCash",
                    DisplayName = "Tổng tiền",
                    Type = FieldType.Decimal,
                    Formula = "CashOnHand + BankDeposits"
                }
            ];
        }

        public override async Task<GenericHKDBook> CreateBookAsync(
            TenantId tenantId,
            AccountingPeriod period,
            List<JournalEntry> entries)
        {
            GenericHKDBook book = new()
            {
                TenantId = tenantId,
                Period = period,
                BookTypeCode = TemplateCode,
                Template = this,
                Entries = entries
            };

            await book.CalculateAsync();
            await book.ValidateAsync();

            return book;
        }

        public override async Task CalculateAsync(GenericHKDBook book)
        {
            await Task.CompletedTask;
        }

        public override async Task ValidateAsync(GenericHKDBook book)
        {
            await Task.CompletedTask;
        }

        public override async Task<string> GenerateReportAsync(GenericHKDBook book)
        {
            string report = $"SỔ CHI TIẾT TIỀN S2e_HKD - {book.Period.Year}/{book.Period.Month:D2}\n";
            report += $"Hộ kinh doanh: {book.TenantId.Value}\n";

            if (book.NumericValues.TryGetValue("CashOnHand", out decimal cash))
            {
                report += $"Tiền mặt: {cash:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("BankDeposits", out decimal bank))
            {
                report += $"Tiền gửi ngân hàng: {bank:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("TotalCash", out decimal total))
            {
                report += $"Tổng tiền: {total:N0} VNĐ\n";
            }

            return await Task.FromResult(report);
        }
    }

    /// <summary>
    /// S3a_HKD Template (Hộ kinh doanh có hoạt động thuộc diện chịu các loại thuế khác)
    /// For HKD Group 3 businesses
    /// </summary>
    public record S3aHKDTemplate : HKDBookTemplate
    {
        public S3aHKDTemplate()
        {
            TemplateCode = "S3a_HKD";
            TemplateName = "Sổ cho hộ kinh doanh có hoạt động thuộc diện chịu các loại thuế khác";
            TargetGroup = HKDGroup.Group3;

            Fields =
            [
                new()
                {
                    FieldName = "Revenue",
                    DisplayName = "Doanh thu",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""5"", ""Credit"")"
                },
                new()
                {
                    FieldName = "SpecialTax",
                    DisplayName = "Thuế đặc biệt",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = "Revenue * 0.1"
                },
                new()
                {
                    FieldName = "OtherTax",
                    DisplayName = "Thuế khác",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = "Revenue * 0.05"
                },
                new()
                {
                    FieldName = "NetRevenue",
                    DisplayName = "Doanh thu sau thuế",
                    Type = FieldType.Decimal,
                    Formula = "Revenue - SpecialTax - OtherTax"
                }
            ];
        }

        public override async Task<GenericHKDBook> CreateBookAsync(
            TenantId tenantId,
            AccountingPeriod period,
            List<JournalEntry> entries)
        {
            GenericHKDBook book = new()
            {
                TenantId = tenantId,
                Period = period,
                BookTypeCode = TemplateCode,
                Template = this,
                Entries = entries
            };

            await book.CalculateAsync();
            await book.ValidateAsync();

            return book;
        }

        public override async Task CalculateAsync(GenericHKDBook book)
        {
            await Task.CompletedTask;
        }

        public override async Task ValidateAsync(GenericHKDBook book)
        {
            await Task.CompletedTask;
        }

        public override async Task<string> GenerateReportAsync(GenericHKDBook book)
        {
            string report = $"SỔ THUẾ KHÁC S3a_HKD - {book.Period.Year}/{book.Period.Month:D2}\n";
            report += $"Hộ kinh doanh: {book.TenantId.Value}\n";

            if (book.NumericValues.TryGetValue("Revenue", out decimal revenue))
            {
                report += $"Doanh thu: {revenue:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("SpecialTax", out decimal special))
            {
                report += $"Thuế đặc biệt: {special:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("OtherTax", out decimal other))
            {
                report += $"Thuế khác: {other:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("NetRevenue", out decimal net))
            {
                report += $"Doanh thu sau thuế: {net:N0} VNĐ\n";
            }

            return await Task.FromResult(report);
        }
    }
}
