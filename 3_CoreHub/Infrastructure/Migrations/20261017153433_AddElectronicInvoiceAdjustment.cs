using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanAn.CoreHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddElectronicInvoiceAdjustment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdjustmentReason",
                table: "ElectronicInvoices",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AdjustmentType",
                table: "ElectronicInvoices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceSeries",
                table: "ElectronicInvoices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OriginalInvoiceId",
                table: "ElectronicInvoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OriginalInvoiceIssueDate",
                table: "ElectronicInvoices",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalProviderInvoiceNumber",
                table: "ElectronicInvoices",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdjustmentReason",
                table: "ElectronicInvoices");

            migrationBuilder.DropColumn(
                name: "AdjustmentType",
                table: "ElectronicInvoices");

            migrationBuilder.DropColumn(
                name: "InvoiceSeries",
                table: "ElectronicInvoices");

            migrationBuilder.DropColumn(
                name: "OriginalInvoiceId",
                table: "ElectronicInvoices");

            migrationBuilder.DropColumn(
                name: "OriginalInvoiceIssueDate",
                table: "ElectronicInvoices");

            migrationBuilder.DropColumn(
                name: "OriginalProviderInvoiceNumber",
                table: "ElectronicInvoices");
        }
    }
}
