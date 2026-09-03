using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMP.Modules.Security.Migrations
{
    /// <inheritdoc />
    public partial class InitialSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "access_grants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PropertyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TargetType = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    TargetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubjectType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    SubjectUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    VisitorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    GrantedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    RevokedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_access_grants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "visitors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PropertyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PhoneNumber = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    HostUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UnitNumber = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    RegisteredByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CheckInAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CheckOutAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_visitors", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_access_grants_PropertyId",
                table: "access_grants",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_access_grants_SubjectUserId",
                table: "access_grants",
                column: "SubjectUserId");

            migrationBuilder.CreateIndex(
                name: "IX_access_grants_VisitorId",
                table: "access_grants",
                column: "VisitorId");

            migrationBuilder.CreateIndex(
                name: "IX_visitors_PropertyId",
                table: "visitors",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_visitors_Status",
                table: "visitors",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "access_grants");

            migrationBuilder.DropTable(
                name: "visitors");
        }
    }
}
