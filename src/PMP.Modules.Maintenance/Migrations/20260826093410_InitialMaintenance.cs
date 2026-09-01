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
            migrationBuilder.EnsureSchema(
                name: "maintenance");

            migrationBuilder.CreateTable(
                name: "maintenance_requests",
                schema: "maintenance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Priority = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RequestedByResidentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maintenance_requests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "maintenance_attachments",
                schema: "maintenance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MaintenanceRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maintenance_attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_maintenance_attachments_maintenance_requests_MaintenanceReq~",
                        column: x => x.MaintenanceRequestId,
                        principalSchema: "maintenance",
                        principalTable: "maintenance_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "maintenance_history",
                schema: "maintenance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MaintenanceRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ToStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maintenance_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_maintenance_history_maintenance_requests_MaintenanceRequest~",
                        column: x => x.MaintenanceRequestId,
                        principalSchema: "maintenance",
                        principalTable: "maintenance_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_attachments_MaintenanceRequestId",
                schema: "maintenance",
                table: "maintenance_attachments",
                column: "MaintenanceRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_history_MaintenanceRequestId",
                schema: "maintenance",
                table: "maintenance_history",
                column: "MaintenanceRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_requests_AssignedToUserId",
                schema: "maintenance",
                table: "maintenance_requests",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_requests_RequestedByResidentId",
                schema: "maintenance",
                table: "maintenance_requests",
                column: "RequestedByResidentId");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_requests_Status",
                schema: "maintenance",
                table: "maintenance_requests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_requests_UnitId",
                schema: "maintenance",
                table: "maintenance_requests",
                column: "UnitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "maintenance_attachments",
                schema: "maintenance");

            migrationBuilder.DropTable(
                name: "maintenance_history",
                schema: "maintenance");

            migrationBuilder.DropTable(
                name: "maintenance_requests",
                schema: "maintenance");
        }
    }
}
