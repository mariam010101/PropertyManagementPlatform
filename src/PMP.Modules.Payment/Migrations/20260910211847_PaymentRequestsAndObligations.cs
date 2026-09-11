using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMP.Modules.Payment.Migrations
{
    /// <inheritdoc />
    public partial class PaymentRequestsAndObligations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "invoices",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledAt",
                table: "invoices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "invoices",
                type: "TEXT",
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OverdueNotifiedAt",
                table: "invoices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "invoices",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                defaultValue: "Rent");

            // Backfill requests created before the currency/purpose fields existed.
            migrationBuilder.Sql("UPDATE invoices SET Currency = 'USD' WHERE Currency IS NULL OR Currency = '';");
            migrationBuilder.Sql("UPDATE invoices SET Purpose = 'Rent' WHERE Purpose IS NULL OR Purpose = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "OverdueNotifiedAt",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "invoices");
        }
    }
}
