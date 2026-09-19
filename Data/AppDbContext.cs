using AtiatHire.Models;
using Microsoft.EntityFrameworkCore;

namespace AtiatHire.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<HireRequest> HireRequests => Set<HireRequest>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<RequestStatusChange> StatusChanges => Set<RequestStatusChange>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<HireRequest>(e =>
        {
            e.HasIndex(x => x.ReferenceNumber).IsUnique();
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.CreatedAt);

            // Store enums as readable text so the database can be queried by hand or by BI tools.
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.PreferredVehicleType).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.DriverRequirement).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Purpose).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.QuotedAmount).HasColumnType("decimal(18,2)");

            e.HasOne(x => x.AssignedVehicle)
                .WithMany()
                .HasForeignKey(x => x.AssignedVehicleId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasMany(x => x.History)
                .WithOne(h => h.HireRequest)
                .HasForeignKey(h => h.HireRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Vehicle>(e =>
        {
            e.HasIndex(x => x.PlateNumber).IsUnique();
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        });

        b.Entity<RequestStatusChange>(e =>
        {
            e.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(20);
        });
    }
}
