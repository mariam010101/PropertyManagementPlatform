using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMP.Modules.Lease.Migrations
{
    /// <inheritdoc />
    public partial class InitialLease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lease_agreements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ResidentUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UnitId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    MonthlyRent = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    CurrentVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    TerminationComment = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    TerminatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lease_agreements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "lease_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LeaseAgreementId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    StoragePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lease_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lease_documents_lease_agreements_LeaseAgreementId",
                        column: x => x.LeaseAgreementId,
                        principalTable: "lease_agreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lease_history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LeaseAgreementId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    ChangeType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lease_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lease_history_lease_agreements_LeaseAgreementId",
                        column: x => x.LeaseAgreementId,
                        principalTable: "lease_agreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_lease_agreements_ResidentUserId",
                table: "lease_agreements",
                column: "ResidentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_lease_agreements_UnitId",
                table: "lease_agreements",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_lease_documents_LeaseAgreementId",
                table: "lease_documents",
                column: "LeaseAgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_lease_history_LeaseAgreementId",
                table: "lease_history",
                column: "LeaseAgreementId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lease_documents");

            migrationBuilder.DropTable(
                name: "lease_history");

            migrationBuilder.DropTable(
                name: "lease_agreements");
        }
    }
}
