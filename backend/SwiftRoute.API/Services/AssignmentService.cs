using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SwiftRoute.API.DTOs;
using SwiftRoute.API.Hubs;
using SwiftRoute.API.Models;
using SwiftRoute.API.Repositories;

namespace SwiftRoute.API.Services;

public interface IAssignmentService
{
    Task<AssignmentDto> CreateAssignmentAsync(CreateAssignmentRequest req, string createdByUserId);
    Task<AssignmentDto> UpdateStatusAsync(int id, string status, string userId);
    Task<IEnumerable<AssignmentDto>> GetAllAsync();
    Task<AssignmentDto?> GetByIdAsync(int id);
}

public class AssignmentService : IAssignmentService
{
    private readonly IUnitOfWork _uow;
    private readonly IHubContext<PulseHub> _hub;

    public AssignmentService(IUnitOfWork uow, IHubContext<PulseHub> hub)
    {
        _uow = uow;
        _hub = hub;
    }

    public async Task<AssignmentDto> CreateAssignmentAsync(CreateAssignmentRequest req, string createdByUserId)
    {
        // 1. Load driver
        var driver = await _uow.Drivers.GetByIdAsync(req.DriverId)
            ?? throw new InvalidOperationException($"Driver {req.DriverId} not found.");

        // 2. License validity check (hard block)
        if (!driver.IsLicenseValid)
            throw new InvalidOperationException(
                $"Driver '{driver.Name}' has an expired license (expired {driver.LicenseExpiry:dd MMM yyyy}). Assignment blocked.");

        // 3. Load vehicle
        var vehicle = await _uow.Vehicles.GetByVINAsync(req.VehicleVIN)
            ?? throw new InvalidOperationException($"Vehicle {req.VehicleVIN} not found.");

        // 4. Vehicle status check (only Active allowed)
        if (vehicle.Status != VehicleStatus.Active)
            throw new InvalidOperationException(
                $"Vehicle '{vehicle.Model}' is not available. Current status: {vehicle.Status}. Assignment blocked.");

        // 5. License class match (bonus rule)
        if (driver.LicenseClass == "ClassB" && vehicle.LicenseClass == "ClassA")
            throw new InvalidOperationException(
                $"Driver '{driver.Name}' holds a Class B license but vehicle '{vehicle.Model}' requires Class A. Assignment blocked.");

        // 6. Resource exclusivity – driver (concurrency-safe)
        if (await _uow.Assignments.IsDriverActivelyAssignedAsync(req.DriverId))
            throw new InvalidOperationException(
                $"Driver '{driver.Name}' is already on an active assignment. Resource Already Assigned.");

        // 7. Resource exclusivity – vehicle (concurrency-safe)
        if (await _uow.Assignments.IsVehicleActivelyAssignedAsync(req.VehicleVIN))
            throw new InvalidOperationException(
                $"Vehicle '{vehicle.Model}' is already assigned. Resource Already Assigned.");

        // 8. Create assignment
        var assignment = new Assignment
        {
            DriverId = req.DriverId,
            VehicleVIN = req.VehicleVIN,
            Route = req.Route,
            Origin = req.Origin,
            Destination = req.Destination,
            ScheduledStart = req.ScheduledStart,
            ScheduledEnd = req.ScheduledEnd,
            Status = AssignmentStatus.Pending,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _uow.Assignments.CreateAsync(assignment);

        // 9. Update driver status
        driver.Status = DriverStatus.Assigned;
        await _uow.Drivers.UpdateAsync(driver);

        // 10. Update vehicle status to InTransit
        vehicle.Status = VehicleStatus.InTransit;
        await _uow.Vehicles.UpdateAsync(vehicle);

        try
        {
            await _uow.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException(
                "Resource Already Assigned – another dispatcher created an assignment at the same time. Please refresh and try again.");
        }

        // 11. Broadcast via SignalR
        await _hub.Clients.All.SendAsync("AssignmentCreated", assignment.Id);

        return MapToDto(assignment, driver, vehicle, "System");
    }

    public async Task<AssignmentDto> UpdateStatusAsync(int id, string status, string userId)
    {
        var assignment = await _uow.Assignments.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Assignment {id} not found.");

        if (!Enum.TryParse<AssignmentStatus>(status, true, out var newStatus))
            throw new InvalidOperationException($"Invalid status: {status}");

        assignment.Status = newStatus;
        assignment.UpdatedAt = DateTime.UtcNow;

        if (newStatus == AssignmentStatus.Active)
            assignment.ActualStart = DateTime.UtcNow;

        if (newStatus == AssignmentStatus.Completed || newStatus == AssignmentStatus.Cancelled)
        {
            assignment.ActualEnd = DateTime.UtcNow;

            // Free driver
            var driver = await _uow.Drivers.GetByIdAsync(assignment.DriverId);
            if (driver != null && !await _uow.Assignments.IsDriverActivelyAssignedAsync(assignment.DriverId))
            {
                driver.Status = DriverStatus.Available;
                await _uow.Drivers.UpdateAsync(driver);
            }

            // Free vehicle (check maintenance)
            var vehicle = await _uow.Vehicles.GetByVINAsync(assignment.VehicleVIN);
            if (vehicle != null)
            {
                vehicle.Status = vehicle.NeedsService
                    ? VehicleStatus.MaintenanceRequired
                    : VehicleStatus.Active;
                await _uow.Vehicles.UpdateAsync(vehicle);
            }
        }

        await _uow.Assignments.UpdateAsync(assignment);
        await _uow.SaveChangesAsync();

        await _hub.Clients.All.SendAsync("AssignmentUpdated", id);

        var reloaded = await _uow.Assignments.GetByIdAsync(id)!;
        return MapToDto(reloaded!, reloaded!.Driver, reloaded.Vehicle, reloaded.CreatedBy?.FullName ?? "");
    }

    public async Task<IEnumerable<AssignmentDto>> GetAllAsync()
    {
        var list = await _uow.Assignments.GetAllAsync();
        return list.Select(a => MapToDto(a, a.Driver, a.Vehicle, a.CreatedBy?.FullName ?? ""));
    }

    public async Task<AssignmentDto?> GetByIdAsync(int id)
    {
        var a = await _uow.Assignments.GetByIdAsync(id);
        if (a == null) return null;
        return MapToDto(a, a.Driver, a.Vehicle, a.CreatedBy?.FullName ?? "");
    }

    private static AssignmentDto MapToDto(Assignment a, Driver d, Vehicle v, string createdBy) => new(
        a.Id, a.DriverId, d.Name, a.VehicleVIN, v.Model,
        a.Route, a.Origin, a.Destination,
        a.ScheduledStart, a.ScheduledEnd, a.ActualStart, a.ActualEnd,
        a.Status.ToString(), createdBy, a.CreatedAt);
}
