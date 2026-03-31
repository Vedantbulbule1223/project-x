using SwiftRoute.API.Models;

namespace SwiftRoute.API.Repositories;

public interface IDriverRepository
{
    Task<IEnumerable<Driver>> GetAllAsync();
    Task<Driver?> GetByIdAsync(string id);
    Task<Driver> CreateAsync(Driver driver);
    Task<Driver> UpdateAsync(Driver driver);
    Task DeleteAsync(string id);
    Task<bool> HasActiveAssignmentAsync(string driverId);
}

public interface IVehicleRepository
{
    Task<IEnumerable<Vehicle>> GetAllAsync();
    Task<Vehicle?> GetByVINAsync(string vin);
    Task<Vehicle> CreateAsync(Vehicle vehicle);
    Task<Vehicle> UpdateAsync(Vehicle vehicle);
    Task DeleteAsync(string vin);
    Task<bool> HasActiveAssignmentAsync(string vin);
    Task<IEnumerable<Vehicle>> GetVehiclesNeedingServiceAsync();
}

public interface IAssignmentRepository
{
    Task<IEnumerable<Assignment>> GetAllAsync();
    Task<Assignment?> GetByIdAsync(int id);
    Task<Assignment> CreateAsync(Assignment assignment);
    Task<Assignment> UpdateAsync(Assignment assignment);
    Task<bool> IsDriverActivelyAssignedAsync(string driverId);
    Task<bool> IsVehicleActivelyAssignedAsync(string vin);
}

public interface IMaintenanceLogRepository
{
    Task<IEnumerable<MaintenanceLog>> GetByVehicleVINAsync(string vin);
    Task<MaintenanceLog> CreateAsync(MaintenanceLog log);
}

public interface IUnitOfWork : IDisposable
{
    IDriverRepository Drivers { get; }
    IVehicleRepository Vehicles { get; }
    IAssignmentRepository Assignments { get; }
    IMaintenanceLogRepository MaintenanceLogs { get; }
    Task<int> SaveChangesAsync();
}
