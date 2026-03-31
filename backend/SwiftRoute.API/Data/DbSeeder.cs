using Microsoft.EntityFrameworkCore;
using SwiftRoute.API.Models;

namespace SwiftRoute.API.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(SwiftRouteDbContext context)
    {
        await context.Database.MigrateAsync();

        // Seed Users
        if (!await context.Users.AnyAsync())
        {
            var users = new List<User>
            {
                new() { Id = "U001", Username = "admin", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"), FullName = "System Admin", Email = "admin@swiftroute.com", Role = UserRole.Admin },
                new() { Id = "U002", Username = "dispatcher1", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Dispatch@123"), FullName = "Rahul Verma", Email = "rahul@swiftroute.com", Role = UserRole.Dispatcher },
                new() { Id = "U003", Username = "dispatcher2", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Dispatch@123"), FullName = "Sneha Patel", Email = "sneha@swiftroute.com", Role = UserRole.Dispatcher },
                new() { Id = "U004", Username = "manager1", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Manager@123"), FullName = "Vijay Nair", Email = "vijay@swiftroute.com", Role = UserRole.Manager },
                new() { Id = "U005", Username = "tech1", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Tech@123"), FullName = "Mohan Das", Email = "mohan@swiftroute.com", Role = UserRole.Technician },
            };
            context.Users.AddRange(users);
            await context.SaveChangesAsync();
        }

        // Seed Drivers (from spec)
        if (!await context.Drivers.AnyAsync())
        {
            var drivers = new List<Driver>
            {
                new() { Id = "D001", Name = "Rajesh Kumar",  LicenseClass = "ClassA", LicenseExpiry = new DateTime(2025, 12, 31), Status = DriverStatus.Available, Phone = "9876543210", Email = "rajesh@example.com" },
                new() { Id = "D002", Name = "Amit Sharma",   LicenseClass = "ClassB", LicenseExpiry = new DateTime(2023, 1, 15),  Status = DriverStatus.Available, Phone = "9876543211", Email = "amit@example.com" },
                new() { Id = "D003", Name = "Priya Singh",   LicenseClass = "ClassA", LicenseExpiry = new DateTime(2026, 6, 20),  Status = DriverStatus.Assigned,  Phone = "9876543212", Email = "priya@example.com" },
                new() { Id = "D004", Name = "Suresh Yadav",  LicenseClass = "ClassA", LicenseExpiry = new DateTime(2027, 3, 10),  Status = DriverStatus.Available, Phone = "9876543213", Email = "suresh@example.com" },
                new() { Id = "D005", Name = "Kavitha Reddy", LicenseClass = "ClassB", LicenseExpiry = new DateTime(2026, 9, 5),   Status = DriverStatus.Available, Phone = "9876543214", Email = "kavitha@example.com" },
            };
            context.Drivers.AddRange(drivers);
            await context.SaveChangesAsync();
        }

        // Seed Vehicles (from spec)
        if (!await context.Vehicles.AnyAsync())
        {
            var vehicles = new List<Vehicle>
            {
                new() { VIN = "V101", Model = "BharatBenz 3523R",     LicenseClass = "ClassA", CurrentOdometer = 45000, LastServiceOdometer = 42000, Status = VehicleStatus.Active },
                new() { VIN = "V102", Model = "Tata Prima 5530.S",    LicenseClass = "ClassA", CurrentOdometer = 88000, LastServiceOdometer = 75000, Status = VehicleStatus.MaintenanceRequired },
                new() { VIN = "V103", Model = "Ashok Leyland 4220",   LicenseClass = "ClassA", CurrentOdometer = 12000, LastServiceOdometer = 11500, Status = VehicleStatus.InTransit },
                new() { VIN = "V104", Model = "Mahindra Blazo X 35",  LicenseClass = "ClassB", CurrentOdometer = 23000, LastServiceOdometer = 22000, Status = VehicleStatus.Active },
                new() { VIN = "V105", Model = "Eicher Pro 6049",      LicenseClass = "ClassB", CurrentOdometer = 67000, LastServiceOdometer = 60000, Status = VehicleStatus.Active },
            };
            context.Vehicles.AddRange(vehicles);
            await context.SaveChangesAsync();
        }
    }
}
