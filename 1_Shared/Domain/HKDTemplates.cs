namespace VanAn.Shared.Domain
{
    /// <summary>
    /// S1a_HKD Template (KhÃ´ng chá»‹u thuáº¿ GTGT, khÃ´ng ná»™p thuáº¿ TNCN)
    /// For HKD Group 1 businesses
    /// </summary>
    public record S1aHKDTemplate : HKDBookTemplate
    {
        public S1aHKDTemplate()
        {
            TemplateCode = "S1a_HKD";
            TemplateName = "Sá»• káº¿ toÃ¡n cho há»™ kinh doanh khÃ´ng chá»‹u thuáº¿ GTGT";
            TargetGroup = HKDGroup.Group1;

            Fields =
            [
                new()
                {
                    FieldName = "TotalRevenue",
                    DisplayName = "Tá»•ng doanh thu",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""5"", ""Credit"")"
                },
                new()
                {
                    FieldName = "TotalExpense",
                    DisplayName = "Tá»•ng chi phÃ­",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""6"", ""Debit"")"
                },
                new()
                {
                    FieldName = "NetProfit",
                    DisplayName = "Lá»£i nhuáº­n",
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
            string report = $"Sá»” Káº¾ TOÃN S1a_HKD - {book.Period.Year}/{book.Period.Month:D2}\n";
            report += $"Há»™ kinh doanh: {book.TenantId.Value}\n";

            if (book.NumericValues.TryGetValue("TotalRevenue", out decimal revenue))
            {
                report += $"Tá»•ng doanh thu: {revenue:N0} VNÄ\n";
            }

            if (book.NumericValues.TryGetValue("TotalExpense", out decimal expense))
            {
                report += $"Tá»•ng chi phÃ­: {expense:N0} VNÄ\n";
            }

            if (book.NumericValues.TryGetValue("NetProfit", out decimal profit))
            {
                report += $"Lá»£i nhuáº­n: {profit:N0} VNÄ\n";
            }

            return await Task.FromResult(report);
        }
    }

    /// <summary>
    /// S2a_HKD Template (Ná»™p thuáº¿ GTGT vÃ  TNCN theo tá»· lá»‡ % trÃªn doanh thu)
    /// For HKD Group 2 businesses.
    /// Wave 5 (TT 152/2025/TT-BTC): 4 industry groups Ã— 3 fields (Revenue, VatAmount, PIT per group).
    /// Tax rates per Luáº­t Thuáº¿ GTGT/TNCN sá»­a Ä‘á»•i 2025 + ND 117/2025.
    /// </summary>
    public record S2aHKDTemplate : HKDBookTemplate
    {
        public S2aHKDTemplate()
        {
            TemplateCode = "S2a_HKD";
            TemplateName = "Sá»• káº¿ toÃ¡n cho há»™ kinh doanh ná»™p thuáº¿ GTGT vÃ  TNCN";
            TargetGroup = HKDGroup.Group2;

            // Wave 5: 4 industry groups Ã— 3 fields. NULL IndustrySector â†’ OtherBusiness bucket.
            Fields =
            [
                // â”€â”€ Distribution (GTGT 1%, TNCN 0.5%) â”€â”€
                new() { FieldName = "Revenue_Distribution", DisplayName = "Doanh thu â€” PhÃ¢n phá»‘i", Type = FieldType.Decimal, IsRequired = true, Formula = @"SUM_ACCOUNT_BY_INDUSTRY(""5"", ""Credit"", ""Distribution"")" },
                new() { FieldName = "VatAmount_Distribution", DisplayName = "Thuáº¿ GTGT â€” PhÃ¢n phá»‘i", Type = FieldType.Decimal, Formula = "Revenue_Distribution * 0.01" },
                new() { FieldName = "PIT_Distribution", DisplayName = "Thuáº¿ TNCN â€” PhÃ¢n phá»‘i", Type = FieldType.Decimal, Formula = "Revenue_Distribution * 0.005" },
                // â”€â”€ ProductionTransport (GTGT 3%, TNCN 1.5%) â”€â”€
                new() { FieldName = "Revenue_ProductionTransport", DisplayName = "Doanh thu â€” Sáº£n xuáº¥t, váº­n táº£i", Type = FieldType.Decimal, IsRequired = true, Formula = @"SUM_ACCOUNT_BY_INDUSTRY(""5"", ""Credit"", ""ProductionTransport"")" },
                new() { FieldName = "VatAmount_ProductionTransport", DisplayName = "Thuáº¿ GTGT â€” Sáº£n xuáº¥t, váº­n táº£i", Type = FieldType.Decimal, Formula = "Revenue_ProductionTransport * 0.03" },
                new() { FieldName = "PIT_ProductionTransport", DisplayName = "Thuáº¿ TNCN â€” Sáº£n xuáº¥t, váº­n táº£i", Type = FieldType.Decimal, Formula = "Revenue_ProductionTransport * 0.015" },
                // â”€â”€ Service (GTGT 5%, TNCN 2%) â”€â”€
                new() { FieldName = "Revenue_Service", DisplayName = "Doanh thu â€” Dá»‹ch vá»¥", Type = FieldType.Decimal, IsRequired = true, Formula = @"SUM_ACCOUNT_BY_INDUSTRY(""5"", ""Credit"", ""Service"")" },
                new() { FieldName = "VatAmount_Service", DisplayName = "Thuáº¿ GTGT â€” Dá»‹ch vá»¥", Type = FieldType.Decimal, Formula = "Revenue_Service * 0.05" },
                new() { FieldName = "PIT_Service", DisplayName = "Thuáº¿ TNCN â€” Dá»‹ch vá»¥", Type = FieldType.Decimal, Formula = "Revenue_Service * 0.02" },
                // â”€â”€ OtherBusiness (GTGT 2%, TNCN 1%) â€” includes NULL IndustrySector entries â”€â”€
                new() { FieldName = "Revenue_OtherBusiness", DisplayName = "Doanh thu â€” Hoáº¡t Ä‘á»™ng khÃ¡c", Type = FieldType.Decimal, IsRequired = true, Formula = @"SUM_ACCOUNT_BY_INDUSTRY(""5"", ""Credit"", ""OtherBusiness"")" },
                new() { FieldName = "VatAmount_OtherBusiness", DisplayName = "Thuáº¿ GTGT â€” Hoáº¡t Ä‘á»™ng khÃ¡c", Type = FieldType.Decimal, Formula = "Revenue_OtherBusiness * 0.02" },
                new() { FieldName = "PIT_OtherBusiness", DisplayName = "Thuáº¿ TNCN â€” Hoáº¡t Ä‘á»™ng khÃ¡c", Type = FieldType.Decimal, Formula = "Revenue_OtherBusiness * 0.01" },
                // â”€â”€ Totals â”€â”€
                new() { FieldName = "TotalRevenue", DisplayName = "Tá»•ng doanh thu", Type = FieldType.Decimal, IsRequired = true, Formula = "Revenue_Distribution + Revenue_ProductionTransport + Revenue_Service + Revenue_OtherBusiness" },
                new() { FieldName = "TotalVat", DisplayName = "Tá»•ng thuáº¿ GTGT", Type = FieldType.Decimal, Formula = "VatAmount_Distribution + VatAmount_ProductionTransport + VatAmount_Service + VatAmount_OtherBusiness" },
                new() { FieldName = "TotalPIT", DisplayName = "Tá»•ng thuáº¿ TNCN", Type = FieldType.Decimal, Formula = "PIT_Distribution + PIT_ProductionTransport + PIT_Service + PIT_OtherBusiness" },
                new() { FieldName = "NetRevenue", DisplayName = "Doanh thu sau thuáº¿", Type = FieldType.Decimal, Formula = "TotalRevenue - TotalVat - TotalPIT" }
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
            string report = $"Sá»” Káº¾ TOÃN S2a_HKD - {book.Period.Year}/{book.Period.Month:D2}\n";
            report += $"Há»™ kinh doanh: {book.TenantId.Value}\n";

            foreach (string sector in new[] { "Distribution", "ProductionTransport", "Service", "OtherBusiness" })
            {
                if (book.NumericValues.TryGetValue($"Revenue_{sector}", out decimal revenue))
                {
                    report += $"  Doanh thu {sector}: {revenue:N0} VNÄ\n";
                }

                if (book.NumericValues.TryGetValue($"VatAmount_{sector}", out decimal vat))
                {
                    report += $"  Thuáº¿ GTGT {sector}: {vat:N0} VNÄ\n";
                }

                if (book.NumericValues.TryGetValue($"PIT_{sector}", out decimal pit))
                {
                    report += $"  Thuáº¿ TNCN {sector}: {pit:N0} VNÄ\n";
                }
            }

            if (book.NumericValues.TryGetValue("TotalRevenue", out decimal totalRevenue))
            {
                report += $"Tá»•ng doanh thu: {totalRevenue:N0} VNÄ\n";
            }

            if (book.NumericValues.TryGetValue("TotalVat", out decimal totalVat))
            {
                report += $"Tá»•ng thuáº¿ GTGT: {totalVat:N0} VNÄ\n";
            }

            if (book.NumericValues.TryGetValue("TotalPIT", out decimal totalPit))
            {
                report += $"Tá»•ng thuáº¿ TNCN: {totalPit:N0} VNÄ\n";
            }

            if (book.NumericValues.TryGetValue("NetRevenue", out decimal net))
            {
                report += $"Doanh thu sau thuáº¿: {net:N0} VNÄ\n";
            }

            return await Task.FromResult(report);
        }
    }

    /// <summary>
    /// S2b_HKD Template (Sá»• doanh thu bÃ¡n hÃ ng hÃ³a, dá»‹ch vá»¥)
    /// For HKD Group 2 businesses.
    /// Wave 5 (TT 152/2025/TT-BTC): 4 industry groups Ã— 2 fields (Revenue, VatAmount per group).
    /// Split by industry sector (NOT goods-vs-service â€” that was a TT 200 hallucination).
    /// </summary>
    public record S2bHKDTemplate : HKDBookTemplate
    {
        public S2bHKDTemplate()
        {
            TemplateCode = "S2b_HKD";
            TemplateName = "Sá»• doanh thu bÃ¡n hÃ ng hÃ³a, dá»‹ch vá»¥";
            TargetGroup = HKDGroup.Group2;

            // Wave 5: 4 industry groups Ã— 2 fields. NULL IndustrySector â†’ OtherBusiness bucket.
            Fields =
            [
                // â”€â”€ Distribution (GTGT 1%) â”€â”€
                new() { FieldName = "Revenue_Distribution", DisplayName = "Doanh thu â€” PhÃ¢n phá»‘i", Type = FieldType.Decimal, IsRequired = true, Formula = @"SUM_ACCOUNT_BY_INDUSTRY(""5"", ""Credit"", ""Distribution"")" },
                new() { FieldName = "VatAmount_Distribution", DisplayName = "Thuáº¿ GTGT â€” PhÃ¢n phá»‘i", Type = FieldType.Decimal, Formula = "Revenue_Distribution * 0.01" },
                // â”€â”€ ProductionTransport (GTGT 3%) â”€â”€
                new() { FieldName = "Revenue_ProductionTransport", DisplayName = "Doanh thu â€” Sáº£n xuáº¥t, váº­n táº£i", Type = FieldType.Decimal, IsRequired = true, Formula = @"SUM_ACCOUNT_BY_INDUSTRY(""5"", ""Credit"", ""ProductionTransport"")" },
                new() { FieldName = "VatAmount_ProductionTransport", DisplayName = "Thuáº¿ GTGT â€” Sáº£n xuáº¥t, váº­n táº£i", Type = FieldType.Decimal, Formula = "Revenue_ProductionTransport * 0.03" },
                // â”€â”€ Service (GTGT 5%) â”€â”€
                new() { FieldName = "Revenue_Service", DisplayName = "Doanh thu â€” Dá»‹ch vá»¥", Type = FieldType.Decimal, IsRequired = true, Formula = @"SUM_ACCOUNT_BY_INDUSTRY(""5"", ""Credit"", ""Service"")" },
                new() { FieldName = "VatAmount_Service", DisplayName = "Thuáº¿ GTGT â€” Dá»‹ch vá»¥", Type = FieldType.Decimal, Formula = "Revenue_Service * 0.05" },
                // â”€â”€ OtherBusiness (GTGT 2%) â€” includes NULL IndustrySector entries â”€â”€
                new() { FieldName = "Revenue_OtherBusiness", DisplayName = "Doanh thu â€” Hoáº¡t Ä‘á»™ng khÃ¡c", Type = FieldType.Decimal, IsRequired = true, Formula = @"SUM_ACCOUNT_BY_INDUSTRY(""5"", ""Credit"", ""OtherBusiness"")" },
                new() { FieldName = "VatAmount_OtherBusiness", DisplayName = "Thuáº¿ GTGT â€” Hoáº¡t Ä‘á»™ng khÃ¡c", Type = FieldType.Decimal, Formula = "Revenue_OtherBusiness * 0.02" },
                // â”€â”€ Totals â”€â”€
                new() { FieldName = "TotalRevenue", DisplayName = "Tá»•ng doanh thu", Type = FieldType.Decimal, IsRequired = true, Formula = "Revenue_Distribution + Revenue_ProductionTransport + Revenue_Service + Revenue_OtherBusiness" },
                new() { FieldName = "TotalVat", DisplayName = "Tá»•ng thuáº¿ GTGT", Type = FieldType.Decimal, Formula = "VatAmount_Distribution + VatAmount_ProductionTransport + VatAmount_Service + VatAmount_OtherBusiness" }
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
            string report = $"Sá»” DOANH THU S2b_HKD - {book.Period.Year}/{book.Period.Month:D2}\n";
            report += $"Há»™ kinh doanh: {book.TenantId.Value}\n";

            foreach (string sector in new[] { "Distribution", "ProductionTransport", "Service", "OtherBusiness" })
            {
                if (book.NumericValues.TryGetValue($"Revenue_{sector}", out decimal revenue))
                {
                    report += $"Doanh thu {sector}: {revenue:N0} VNÄ\n";
                }

                if (book.NumericValues.TryGetValue($"VatAmount_{sector}", out decimal vat))
                {
                    report += $"Thuáº¿ GTGT {sector}: {vat:N0} VNÄ\n";
                }
            }

            if (book.NumericValues.TryGetValue("TotalRevenue", out decimal total))
            {
                report += $"Tá»•ng doanh thu: {total:N0} VNÄ\n";
            }

            if (book.NumericValues.TryGetValue("TotalVat", out decimal totalVat))
            {
                report += $"Tá»•ng thuáº¿ GTGT: {totalVat:N0} VNÄ\n";
            }

            return await Task.FromResult(report);
        }
    }

    /// <summary>
    /// S2c_HKD Template (Sá»• chi tiáº¿t doanh thu, chi phÃ­)
    /// For HKD Group 2 businesses
    /// </summary>
    public record S2cHKDTemplate : HKDBookTemplate
    {
        public S2cHKDTemplate()
        {
            TemplateCode = "S2c_HKD";
            TemplateName = "Sá»• chi tiáº¿t doanh thu, chi phÃ­";
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
                    DisplayName = "GiÃ¡ vá»‘n hÃ ng bÃ¡n",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""632"", ""Debit"")"
                },
                new()
                {
                    FieldName = "OperatingExpenses",
                    DisplayName = "Chi phÃ­ hoáº¡t Ä‘á»™ng",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""641"", ""Debit"") + SUM_ACCOUNT(""642"", ""Debit"")"
                },
                new()
                {
                    FieldName = "NetProfit",
                    DisplayName = "Lá»£i nhuáº­n",
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
            string report = $"Sá»” CHI TIáº¾T DOANH THU, CHI PHÃ S2c_HKD - {book.Period.Year}/{book.Period.Month:D2}\n";
            report += $"Há»™ kinh doanh: {book.TenantId.Value}\n";

            if (book.NumericValues.TryGetValue("Revenue", out decimal revenue))
            {
                report += $"Doanh thu: {revenue:N0} VNÄ\n";
            }

            if (book.NumericValues.TryGetValue("CostOfGoodsSold", out decimal cogs))
            {
                report += $"GiÃ¡ vá»‘n hÃ ng bÃ¡n: {cogs:N0} VNÄ\n";
            }

            if (book.NumericValues.TryGetValue("OperatingExpenses", out decimal expenses))
            {
                report += $"Chi phÃ­ hoáº¡t Ä‘á»™ng: {expenses:N0} VNÄ\n";
            }

            if (book.NumericValues.TryGetValue("NetProfit", out decimal profit))
            {
                report += $"Lá»£i nhuáº­n: {profit:N0} VNÄ\n";
            }

            return await Task.FromResult(report);
        }
    }

    /// <summary>
    /// S2d_HKD Template (Sá»• chi tiáº¿t váº­t liá»‡u, dá»¥ng cá»¥, sáº£n pháº©m, hÃ ng hÃ³a)
    /// For HKD Group 2 businesses
    /// </summary>
    public record S2dHKDTemplate : HKDBookTemplate
    {
        public S2dHKDTemplate()
        {
            TemplateCode = "S2d_HKD";
            TemplateName = "Sá»• chi tiáº¿t váº­t liá»‡u, dá»¥ng cá»¥, sáº£n pháº©m, hÃ ng hÃ³a";
            TargetGroup = HKDGroup.Group2;

            Fields =
            [
                new()
                {
                    FieldName = "Materials",
                    DisplayName = "Váº­t liá»‡u",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""152"", ""Debit"")"
                },
                new()
                {
                    FieldName = "Tools",
                    DisplayName = "Dá»¥ng cá»¥",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""153"", ""Debit"")"
                },
                new()
                {
                    FieldName = "Products",
                    DisplayName = "Sáº£n pháº©m",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""155"", ""Debit"")"
                },
                new()
                {
                    FieldName = "Goods",
                    DisplayName = "HÃ ng hÃ³a",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""156"", ""Debit"")"
                },
                new()
                {
                    FieldName = "TotalInventory",
                    DisplayName = "Tá»•ng tá»“n kho",
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
            string report = $"Sá»” CHI TIáº¾T Váº¬T LIá»†U, Dá»¤NG Cá»¤, Sáº¢N PHáº¨M, HÃ€NG HÃ“A S2d_HKD - {book.Period.Year}/{book.Period.Month:D2}\n";
            report += $"Há»™ kinh doanh: {book.TenantId.Value}\n";

            if (book.NumericValues.TryGetValue("Materials", out decimal materials))
            {
                report += $"Váº­t liá»‡u: {materials:N0} VNÄ\n";
            }

            if (book.NumericValues.TryGetValue("Tools", out decimal tools))
            {
                report += $"Dá»¥ng cá»¥: {tools:N0} VNÄ\n";
            }

            if (book.NumericValues.TryGetValue("Products", out decimal products))
            {
                report += $"Sáº£n pháº©m: {products:N0} VNÄ\n";
            }

            if (book.NumericValues.TryGetValue("Goods", out decimal goods))
            {
                report += $"HÃ ng hÃ³a: {goods:N0} VNÄ\n";
            }

            if (book.NumericValues.TryGetValue("TotalInventory", out decimal total))
            {
                report += $"Tá»•ng tá»“n kho: {total:N0} VNÄ\n";
            }

            return await Task.FromResult(report);
        }
    }

    /// <summary>
    /// S2e_HKD Template (Sá»• chi tiáº¿t tiá»n)
    /// For HKD Group 2 businesses
    /// </summary>
    public record S2eHKDTemplate : HKDBookTemplate
    {
        public S2eHKDTemplate()
        {
            TemplateCode = "S2e_HKD";
            TemplateName = "Sá»• chi tiáº¿t tiá»n";
            TargetGroup = HKDGroup.Group2;

            Fields =
            [
                new()
                {
                    FieldName = "CashOnHand",
                    DisplayName = "Tiá»n máº·t",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""111"", ""Debit"") - SUM_ACCOUNT(""111"", ""Credit"")"
                },
                new()
                {
                    FieldName = "BankDeposits",
                    DisplayName = "Tiá»n gá»­i ngÃ¢n hÃ ng",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""112"", ""Debit"") - SUM_ACCOUNT(""112"", ""Credit"")"
                },
                new()
                {
                    FieldName = "TotalCash",
                    DisplayName = "Tá»•ng tiá»n",
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
            string report = $"Sá»” CHI TIáº¾T TIá»€N S2e_HKD - {book.Period.Year}/{book.Period.Month:D2}\n";
            report += $"Há»™ kinh doanh: {book.TenantId.Value}\n";

            if (book.NumericValues.TryGetValue("CashOnHand", out decimal cash))
            {
                report += $"Tiá»n máº·t: {cash:N0} VNÄ\n";
            }

            if (book.NumericValues.TryGetValue("BankDeposits", out decimal bank))
            {
                report += $"Tiá»n gá»­i ngÃ¢n hÃ ng: {bank:N0} VNÄ\n";
            }

            if (book.NumericValues.TryGetValue("TotalCash", out decimal total))
            {
                report += $"Tá»•ng tiá»n: {total:N0} VNÄ\n";
            }

            return await Task.FromResult(report);
        }
    }

    /// <summary>
    /// S3a_HKD Template (Há»™ kinh doanh cÃ³ hoáº¡t Ä‘á»™ng thuá»™c diá»‡n chá»‹u cÃ¡c loáº¡i thuáº¿ khÃ¡c)
    /// For HKD Group 3 businesses
    /// </summary>
    public record S3aHKDTemplate : HKDBookTemplate
    {
        public S3aHKDTemplate()
        {
            TemplateCode = "S3a_HKD";
            TemplateName = "Sá»• cho há»™ kinh doanh cÃ³ hoáº¡t Ä‘á»™ng thuá»™c diá»‡n chá»‹u cÃ¡c loáº¡i thuáº¿ khÃ¡c";
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
                    DisplayName = "Thuáº¿ Ä‘áº·c biá»‡t",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = "Revenue * 0.1"
                },
                new()
                {
                    FieldName = "OtherTax",
                    DisplayName = "Thuáº¿ khÃ¡c",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = "Revenue * 0.05"
                },
                new()
                {
                    FieldName = "NetRevenue",
                    DisplayName = "Doanh thu sau thuáº¿",
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
            string report = $"Sá»” THUáº¾ KHÃC S3a_HKD - {book.Period.Year}/{book.Period.Month:D2}\n";
            report += $"Há»™ kinh doanh: {book.TenantId.Value}\n";

            if (book.NumericValues.TryGetValue("Revenue", out decimal revenue))
            {
                report += $"Doanh thu: {revenue:N0} VNÄ\n";
            }

            if (book.NumericValues.TryGetValue("SpecialTax", out decimal special))
            {
                report += $"Thuáº¿ Ä‘áº·c biá»‡t: {special:N0} VNÄ\n";
            }

            if (book.NumericValues.TryGetValue("OtherTax", out decimal other))
            {
                report += $"Thuáº¿ khÃ¡c: {other:N0} VNÄ\n";
            }

            if (book.NumericValues.TryGetValue("NetRevenue", out decimal net))
            {
                report += $"Doanh thu sau thuáº¿: {net:N0} VNÄ\n";
            }

            return await Task.FromResult(report);
        }
    }
}
