using Microsoft.EntityFrameworkCore;
using SwiftRoute.API.Data;
using SwiftRoute.API.Models;

namespace SwiftRoute.API.Repositories;

public class DriverRepository : IDriverRepository
{
    private readonly SwiftRouteDbContext _ctx;
    public DriverRepository(SwiftRouteDbContext ctx) => _ctx = ctx;

    public async Task<IEnumerable<Driver>> GetAllAsync() =>
        await _ctx.Drivers.OrderBy(d => d.Name).ToListAsync();

    public async Task<Driver?> GetByIdAsync(string id) =>
        await _ctx.Drivers.FindAsync(id);

    public async Task<Driver> CreateAsync(Driver driver)
    {
        _ctx.Drivers.Add(driver);
        return driver;
    }

    public async Task<Driver> UpdateAsync(Driver driver)
    {
        driver.UpdatedAt = DateTime.UtcNow;
        _ctx.Drivers.Update(driver);
        return driver;
    }

    public async Task DeleteAsync(string id)
    {
        var d = await _ctx.Drivers.FindAsync(id);
        if (d != null) _ctx.Drivers.Remove(d);
    }

    public async Task<bool> HasActiveAssignmentAsync(string driverId) =>
        await _ctx.Assignments.AnyAsync(a =>
            a.DriverId == driverId &&
            (a.Status == AssignmentStatus.Active || a.Status == AssignmentStatus.Pending));
}

public class VehicleRepository : IVehicleRepository
{
    private readonly SwiftRouteDbContext _ctx;
    public VehicleRepository(SwiftRouteDbContext ctx) => _ctx = ctx;

    public async Task<IEnumerable<Vehicle>> GetAllAsync() =>
        await _ctx.Vehicles.OrderBy(v => v.Model).ToListAsync();

    public async Task<Vehicle?> GetByVINAsync(string vin) =>
        await _ctx.Vehicles.FindAsync(vin);

    public async Task<Vehicle> CreateAsync(Vehicle vehicle)
    {
        _ctx.Vehicles.Add(vehicle);
        return vehicle;
    }

    public async Task<Vehicle> UpdateAsync(Vehicle vehicle)
    {
        vehicle.UpdatedAt = DateTime.UtcNow;
        _ctx.Vehicles.Update(vehicle);
        return vehicle;
    }

    public async Task DeleteAsync(string vin)
    {
        var v = await _ctx.Vehicles.FindAsync(vin);
        if (v != null) _ctx.Vehicles.Remove(v);
    }

    public async Task<bool> HasActiveAssignmentAsync(string vin) =>
        await _ctx.Assignments.AnyAsync(a =>
            a.VehicleVIN == vin &&
            (a.Status == AssignmentStatus.Active || a.Status == AssignmentStatus.Pending));

    public async Task<IEnumerable<Vehicle>> GetVehiclesNeedingServiceAsync() =>
        await _ctx.Vehicles
            .Where(v => v.CurrentOdometer - v.LastServiceOdometer >= 10000)
            .ToListAsync();
}

public class AssignmentRepository : IAssignmentRepository
{
    private readonly SwiftRouteDbContext _ctx;
    public AssignmentRepository(SwiftRouteDbContext ctx) => _ctx = ctx;

    public async Task<IEnumerable<Assignment>> GetAllAsync() =>
        await _ctx.Assignments
            .Include(a => a.Driver)
            .Include(a => a.Vehicle)
            .Include(a => a.CreatedBy)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

    public async Task<Assignment?> GetByIdAsync(int id) =>
        await _ctx.Assignments
            .Include(a => a.Driver)
            .Include(a => a.Vehicle)
            .Include(a => a.CreatedBy)
            .FirstOrDefaultAsync(a => a.Id == id);

    public async Task<Assignment> CreateAsync(Assignment assignment)
    {
        _ctx.Assignments.Add(assignment);
        return assignment;
    }

    public async Task<Assignment> UpdateAsync(Assignment assignment)
    {
        assignment.UpdatedAt = DateTime.UtcNow;
        _ctx.Assignments.Update(assignment);
        return assignment;
    }

    public async Task<bool> IsDriverActivelyAssignedAsync(string driverId) =>
        await _ctx.Assignments.AnyAsync(a =>
            a.DriverId == driverId &&
            (a.Status == AssignmentStatus.Active || a.Status == AssignmentStatus.Pending));

    public async Task<bool> IsVehicleActivelyAssignedAsync(string vin) =>
        await _ctx.Assignments.AnyAsync(a =>
            a.VehicleVIN == vin &&
            (a.Status == AssignmentStatus.Active || a.Status == AssignmentStatus.Pending));
}

public class MaintenanceLogRepository : IMaintenanceLogRepository
{
    private readonly SwiftRouteDbContext _ctx;
    public MaintenanceLogRepository(SwiftRouteDbContext ctx) => _ctx = ctx;

    public async Task<IEnumerable<MaintenanceLog>> GetByVehicleVINAsync(string vin) =>
        await _ctx.MaintenanceLogs
            .Include(m => m.PerformedBy)
            .Where(m => m.VehicleVIN == vin)
            .OrderByDescending(m => m.ServiceDate)
            .ToListAsync();

    public async Task<MaintenanceLog> CreateAsync(MaintenanceLog log)
    {
        _ctx.MaintenanceLogs.Add(log);
        return log;
    }
}

public class UnitOfWork : IUnitOfWork
{
    private readonly SwiftRouteDbContext _ctx;
    public IDriverRepository Drivers { get; }
    public IVehicleRepository Vehicles { get; }
    public IAssignmentRepository Assignments { get; }
    public IMaintenanceLogRepository MaintenanceLogs { get; }

    public UnitOfWork(SwiftRouteDbContext ctx)
    {
        _ctx = ctx;
        Drivers = new DriverRepository(ctx);
        Vehicles = new VehicleRepository(ctx);
        Assignments = new AssignmentRepository(ctx);
        MaintenanceLogs = new MaintenanceLogRepository(ctx);
    }

    public async Task<int> SaveChangesAsync() => await _ctx.SaveChangesAsync();
    public void Dispose() => _ctx.Dispose();
}
