using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMP.Modules.Payment.Migrations
{
    /// <inheritdoc />
    public partial class InitialPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LeaseAgreementId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ResidentUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UnitId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PropertyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    PeriodEnd = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    DueDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    PaidAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    PaidAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    DueDateReminderSentAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "payment_transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TransactionReference = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    InvoiceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ResidentUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Method = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    PaidAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ConfirmationNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    FailureReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payment_transactions_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_invoices_LeaseAgreementId",
                table: "invoices",
                column: "LeaseAgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_PropertyId",
                table: "invoices",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_ResidentUserId",
                table: "invoices",
                column: "ResidentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_Status",
                table: "invoices",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_InvoiceId",
                table: "payment_transactions",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_ResidentUserId",
                table: "payment_transactions",
                column: "ResidentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_TransactionReference",
                table: "payment_transactions",
                column: "TransactionReference",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_transactions");

            migrationBuilder.DropTable(
                name: "invoices");
        }
    }
}
