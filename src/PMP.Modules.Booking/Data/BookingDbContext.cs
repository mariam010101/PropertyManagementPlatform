using Microsoft.EntityFrameworkCore;
using PMP.Modules.Booking.Entities;
using PMP.Modules.Booking.Enums;

namespace PMP.Modules.Booking.Data;

/// <summary>
/// Facility booking persistence. Tables live in the shared SQLite database; module
/// ownership is by table name + separate DbContext.
/// </summary>
public class BookingDbContext : DbContext
{
    public BookingDbContext(DbContextOptions<BookingDbContext> options)
        : base(options)
    {
    }

    public DbSet<Facility> Facilities => Set<Facility>();

    public DbSet<FacilityBooking> FacilityBookings => Set<FacilityBooking>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Facility>(e =>
        {
            e.ToTable("facilities");
            e.Property(f => f.Name).HasMaxLength(150).IsRequired();
            e.Property(f => f.Description).HasMaxLength(1000);
            e.HasIndex(f => f.PropertyId);
        });

        builder.Entity<FacilityBooking>(e =>
        {
            e.ToTable("facility_bookings");
            e.Property(b => b.BookingReference).HasMaxLength(40).IsRequired();
            e.Property(b => b.CancellationReason).HasMaxLength(500);
            e.Property(b => b.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.HasIndex(b => b.FacilityId);
            e.HasIndex(b => b.BookedByUserId);
            e.HasIndex(b => b.BookingReference).IsUnique();

            e.HasOne(b => b.Facility)
                .WithMany()
                .HasForeignKey(b => b.FacilityId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
