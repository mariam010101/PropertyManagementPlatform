using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMP.Modules.Booking.Migrations
{
    /// <inheritdoc />
    public partial class InitialBooking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "facilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PropertyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    OpenMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    CloseMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    SlotMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    CancellationWindowHours = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_facilities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "facility_bookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FacilityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BookedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    EndAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CancelledAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CancellationReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    BookingReference = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_facility_bookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_facility_bookings_facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_facilities_PropertyId",
                table: "facilities",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_facility_bookings_BookedByUserId",
                table: "facility_bookings",
                column: "BookedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_facility_bookings_BookingReference",
                table: "facility_bookings",
                column: "BookingReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_facility_bookings_FacilityId",
                table: "facility_bookings",
                column: "FacilityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "facility_bookings");

            migrationBuilder.DropTable(
                name: "facilities");
        }
    }
}
