using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMP.Modules.Resident.Migrations
{
    /// <inheritdoc />
    public partial class InitialResident : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "resident");

            migrationBuilder.CreateTable(
                name: "resident_profiles",
                schema: "resident",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resident_profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "resident_units",
                schema: "resident",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResidentProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    MoveInDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MoveOutDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resident_units", x => x.Id);
                    table.ForeignKey(
                        name: "FK_resident_units_resident_profiles_ResidentProfileId",
                        column: x => x.ResidentProfileId,
                        principalSchema: "resident",
                        principalTable: "resident_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_resident_profiles_Email",
                schema: "resident",
                table: "resident_profiles",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_resident_profiles_UserId",
                schema: "resident",
                table: "resident_profiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_resident_units_ResidentProfileId_UnitId",
                schema: "resident",
                table: "resident_units",
                columns: new[] { "ResidentProfileId", "UnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_resident_units_UnitId",
                schema: "resident",
                table: "resident_units",
                column: "UnitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "resident_units",
                schema: "resident");

            migrationBuilder.DropTable(
                name: "resident_profiles",
                schema: "resident");
        }
    }
}
