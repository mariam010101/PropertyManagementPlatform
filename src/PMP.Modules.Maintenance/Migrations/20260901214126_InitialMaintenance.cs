using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMP.Modules.Maintenance.Migrations
{
    /// <inheritdoc />
    public partial class InitialMaintenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "maintenance_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Priority = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    RequestedByResidentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UnitId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AssignedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CancellationReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maintenance_requests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "maintenance_attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MaintenanceRequestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    StoragePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maintenance_attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_maintenance_attachments_maintenance_requests_MaintenanceRequestId",
                        column: x => x.MaintenanceRequestId,
                        principalTable: "maintenance_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "maintenance_history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MaintenanceRequestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FromStatus = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    ToStatus = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Comment = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ChangedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maintenance_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_maintenance_history_maintenance_requests_MaintenanceRequestId",
                        column: x => x.MaintenanceRequestId,
                        principalTable: "maintenance_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_attachments_MaintenanceRequestId",
                table: "maintenance_attachments",
                column: "MaintenanceRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_history_MaintenanceRequestId",
                table: "maintenance_history",
                column: "MaintenanceRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_requests_AssignedToUserId",
                table: "maintenance_requests",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_requests_RequestedByResidentId",
                table: "maintenance_requests",
                column: "RequestedByResidentId");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_requests_Status",
                table: "maintenance_requests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_requests_UnitId",
                table: "maintenance_requests",
                column: "UnitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "maintenance_attachments");

            migrationBuilder.DropTable(
                name: "maintenance_history");

            migrationBuilder.DropTable(
                name: "maintenance_requests");
        }
    }
}
