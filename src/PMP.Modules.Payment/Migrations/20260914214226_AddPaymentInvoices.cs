using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMP.Modules.Payment.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "invoice_number_sequences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    LastNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_number_sequences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "payment_invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PaymentTransactionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TransactionReference = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    InvoiceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ResidentUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UnitId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PropertyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    Purpose = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Method = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    PaymentDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConfirmationNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payment_invoices_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_invoices_payment_transactions_PaymentTransactionId",
                        column: x => x.PaymentTransactionId,
                        principalTable: "payment_transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_number_sequences_Year",
                table: "invoice_number_sequences",
                column: "Year",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_invoices_InvoiceId",
                table: "payment_invoices",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_invoices_InvoiceNumber",
                table: "payment_invoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_invoices_PaymentTransactionId",
                table: "payment_invoices",
                column: "PaymentTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_invoices_PropertyId",
                table: "payment_invoices",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_invoices_ResidentUserId",
                table: "payment_invoices",
                column: "ResidentUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invoice_number_sequences");

            migrationBuilder.DropTable(
                name: "payment_invoices");
        }
    }
}
