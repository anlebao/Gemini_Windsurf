using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.Atrs.Infra.Migrations
{
    /// <inheritdoc />
    public partial class FixRowVersionUint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op: RowVersion map tới system column xmin (kiểu xid, PG quản lý sẵn).
            // Không có user column nào để alter — xmin tồn tại implicit trong mọi bảng.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op (xem Up).
        }
    }
}