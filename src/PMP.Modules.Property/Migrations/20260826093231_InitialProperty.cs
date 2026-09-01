using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMP.Modules.Property.Migrations
{
    /// <inheritdoc />
    public partial class InitialProperty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "property");

            migrationBuilder.CreateTable(
                name: "properties",
                schema: "property",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ManagerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_properties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "buildings",
                schema: "property",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Floors = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_buildings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_buildings_properties_PropertyId",
                        column: x => x.PropertyId,
                        principalSchema: "property",
                        principalTable: "properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "residential_units",
                schema: "property",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BuildingId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    UnitType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Bedrooms = table.Column<int>(type: "integer", nullable: true),
                    Bathrooms = table.Column<int>(type: "integer", nullable: true),
                    AreaSqM = table.Column<double>(type: "double precision", nullable: true),
                    OperationalStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_residential_units", x => x.Id);
                    table.ForeignKey(
                        name: "FK_residential_units_buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalSchema: "property",
                        principalTable: "buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_buildings_PropertyId",
                schema: "property",
                table: "buildings",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_properties_ManagerUserId",
                schema: "property",
                table: "properties",
                column: "ManagerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_residential_units_BuildingId",
                schema: "property",
                table: "residential_units",
                column: "BuildingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "residential_units",
                schema: "property");

            migrationBuilder.DropTable(
                name: "buildings",
                schema: "property");

            migrationBuilder.DropTable(
                name: "properties",
                schema: "property");
        }
    }
}
