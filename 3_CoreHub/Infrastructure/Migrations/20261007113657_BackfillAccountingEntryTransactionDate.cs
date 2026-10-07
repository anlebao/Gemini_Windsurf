using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillAccountingEntryTransactionDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NHẬP LIỆU & SỔ SÁCH P1 (#3 — ngày nghiệp vụ): backfill data fix.
            // TransactionDate cột mới (AddAccountingEntryTransactionDate) gán default 01/01/0001
            // cho các rows tồn tại TRƯỚC khi tính năng ra mắt — ngày nghiệp vụ thật không còn lưu.
            // Gán TransactionDate = CreatedAt (thời điểm tạo là xấp xỉ duy nhất còn lại) để các
            // phiếu cũ vẫn hiển thị trong Lịch sử giao dịch / Số dư tài khoản sau khi chuyển sang
            // lọc TransactionDate (nếu không — các phiếu cũ biến mất khỏi báo cáo theo kỳ).
            // Schema-neutral (chỉ UPDATE) — không thêm/bớt cột.
            _ = migrationBuilder.Sql(
                "UPDATE \"AccountingEntries\" SET \"TransactionDate\" = \"CreatedAt\" WHERE \"TransactionDate\" = '0001-01-01 00:00:00';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Không thể hoàn tác an toàn (mất dữ liệu gốc) — no-op.
        }
    }
}
