using Microsoft.EntityFrameworkCore;
using SwiftRoute.API.Models;

namespace SwiftRoute.API.Data;

public class SwiftRouteDbContext : DbContext
{
    public SwiftRouteDbContext(DbContextOptions<SwiftRouteDbContext> options) : base(options) { }

    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<MaintenanceLog> MaintenanceLogs => Set<MaintenanceLog>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ── Driver
        modelBuilder.Entity<Driver>(e =>
        {
            e.HasKey(d => d.Id);
            e.Property(d => d.Name).IsRequired().HasMaxLength(100);
            e.Property(d => d.LicenseClass).IsRequired().HasMaxLength(20);
            e.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(d => d.Phone).HasMaxLength(20);
            e.Property(d => d.Email).HasMaxLength(100);
            e.Property(d => d.RowVersion).HasColumnType("int unsigned").IsConcurrencyToken();
            e.HasIndex(d => d.Status);
        });

        // ── Vehicle
        modelBuilder.Entity<Vehicle>(e =>
        {
            e.HasKey(v => v.VIN);
            e.Property(v => v.VIN).HasMaxLength(50);
            e.Property(v => v.Model).IsRequired().HasMaxLength(100);
            e.Property(v => v.LicenseClass).IsRequired().HasMaxLength(20);
            e.Property(v => v.Status).HasConversion<string>().HasMaxLength(30);
            e.Property(v => v.Notes).HasMaxLength(500);
            e.Property(v => v.RowVersion).HasColumnType("int unsigned").IsConcurrencyToken();
            e.HasIndex(v => v.Status);
        });

        // ── Assignment
        modelBuilder.Entity<Assignment>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Route).HasMaxLength(200);
            e.Property(a => a.Origin).HasMaxLength(100);
            e.Property(a => a.Destination).HasMaxLength(100);
            e.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(a => a.RowVersion).HasColumnType("int unsigned").IsConcurrencyToken();

            e.HasOne(a => a.Driver)
             .WithMany(d => d.Assignments)
             .HasForeignKey(a => a.DriverId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(a => a.Vehicle)
             .WithMany(v => v.Assignments)
             .HasForeignKey(a => a.VehicleVIN)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(a => a.CreatedBy)
             .WithMany(u => u.CreatedAssignments)
             .HasForeignKey(a => a.CreatedByUserId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(a => new { a.DriverId, a.Status });
            e.HasIndex(a => new { a.VehicleVIN, a.Status });
        });

        // ── MaintenanceLog
        modelBuilder.Entity<MaintenanceLog>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.Description).IsRequired().HasMaxLength(500);
            e.Property(m => m.Parts).HasMaxLength(300);
            e.Property(m => m.Cost).HasColumnType("decimal(10,2)");

            e.HasOne(m => m.Vehicle)
             .WithMany(v => v.MaintenanceLogs)
             .HasForeignKey(m => m.VehicleVIN)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(m => m.PerformedBy)
             .WithMany(u => u.MaintenanceLogs)
             .HasForeignKey(m => m.PerformedByUserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── User
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.Username).IsRequired().HasMaxLength(50);
            e.Property(u => u.PasswordHash).IsRequired().HasMaxLength(255);
            e.Property(u => u.FullName).IsRequired().HasMaxLength(100);
            e.Property(u => u.Email).IsRequired().HasMaxLength(100);
            e.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(u => u.Username).IsUnique();
        });
    }
}
