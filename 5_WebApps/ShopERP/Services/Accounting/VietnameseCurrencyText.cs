using System.Text;

namespace VanAn.ShopERP.Services.Accounting;

/// <summary>
/// TT 71 Phase 4b (S3) — Helper chuyển số tiền VNĐ → chữ ("Viết bằng chữ" trên phiếu thu/chi 01-TT/02-TT).
/// Ví dụ: 1.234.567 → "Một triệu hai trăm ba mươi tư nghìn năm trăm sáu mươi bảy đồng".
/// Hỗ trợ đến hàng tỷ; số lẻ làm tròn về nguyên đồng (voucher thực tế là số nguyên VNĐ).
/// </summary>
public static class VietnameseCurrencyText
{
    private static readonly string[] Ones =
        { "", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín" };

    private static readonly string[] Tens =
        { "", "mười", "hai mươi", "ba mươi", "bốn mươi", "năm mươi", "sáu mươi", "bảy mươi", "tám mươi", "chín mươi" };

    public static string ToWords(decimal amount)
    {
        long n = (long)Math.Round(Math.Abs(amount), 0, MidpointRounding.AwayFromZero);
        string text = ReadGroups(n);
        if (amount < 0)
        {
            text = "âm " + text;
        }

        if (text.Length > 0)
        {
            text = char.ToUpperInvariant(text[0]) + text[1..];
        }

        return $"{text} đồng";
    }

    private static string ReadGroups(long n)
    {
        if (n == 0)
        {
            return "không";
        }

        long[] divisors = { 1_000_000_000, 1_000_000, 1_000, 1 };
        string[] names = { "tỷ", "triệu", "nghìn", string.Empty };

        var result = new List<string>();
        bool anyPrev = false;
        for (int i = 0; i < 4; i++)
        {
            long g = (n / divisors[i]) % 1000;
            if (g == 0)
            {
                // Nhóm 0 ở giữa chỉ đọc "không X" nếu còn nhóm khác 0 phía sau
                // (1.000.005 → "một triệu không nghìn..."; 1.000.000.000 → "một tỷ")
                if (anyPrev && i < 3 && n % divisors[i] > 0)
                {
                    result.Add($"không {names[i]}");
                }

                continue;
            }

            string groupText = ReadThreeDigits((int)g, useZeroHundred: anyPrev);
            result.Add(names[i].Length == 0 ? groupText : $"{groupText} {names[i]}");
            anyPrev = true;
        }

        return string.Join(" ", result);
    }

    private static string ReadThreeDigits(int g, bool useZeroHundred)
    {
        int hundreds = g / 100;
        int tens = (g % 100) / 10;
        int ones = g % 10;

        var parts = new List<string>();
        if (hundreds > 0)
        {
            parts.Add($"{Ones[hundreds]} trăm");
        }
        else if (useZeroHundred)
        {
            parts.Add("không trăm");
        }

        if (tens == 0 && ones == 0)
        {
            return string.Join(" ", parts);
        }

        if (tens == 0)
        {
            // "linh" chỉ khi có chữ số hàng trăm HOẶC nhóm cao hơn đã đọc (105 → "một trăm linh năm";
            // 1005 → "một nghìn không trăm linh năm"; riêng 5 → "năm")
            parts.Add(hundreds > 0 || useZeroHundred ? LinhText(ones) : Ones[ones]);
        }
        else if (tens == 1)
        {
            parts.Add(ones == 0 ? "mười" : $"mười {OnesAfterTen(ones, tens)}");
        }
        else
        {
            parts.Add(ones == 0 ? Tens[tens] : $"{Tens[tens]} {OnesAfterTen(ones, tens)}");
        }

        return string.Join(" ", parts);
    }

    private static string LinhText(int ones) => ones switch
    {
        1 => "linh một",
        4 => "linh tư",
        _ => $"linh {Ones[ones]}"
    };

    private static string OnesAfterTen(int ones, int tens) => ones switch
    {
        // 11 = "mười một" (không phải "mười mốt") · 21 = "hai mươi mốt" · 24 = "hai mươi tư" · 25 = "hai mươi lăm"
        1 when tens > 1 => "mốt",
        4 when tens > 1 => "tư",
        5 => "lăm",
        _ => Ones[ones]
    };
}
