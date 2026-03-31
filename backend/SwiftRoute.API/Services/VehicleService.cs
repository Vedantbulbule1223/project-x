using Microsoft.AspNetCore.SignalR;
using SwiftRoute.API.DTOs;
using SwiftRoute.API.Hubs;
using SwiftRoute.API.Models;
using SwiftRoute.API.Repositories;

namespace SwiftRoute.API.Services;

public interface IVehicleService
{
    Task<IEnumerable<VehicleDto>> GetAllAsync();
    Task<VehicleDto?> GetByVINAsync(string vin);
    Task<VehicleDto> CreateAsync(CreateVehicleRequest req);
    Task<VehicleDto> UpdateOdometerAsync(string vin, double newOdometer);
    Task<VehicleDto> UpdateStatusAsync(string vin, string status);
    Task<MaintenanceLogDto> LogMaintenanceAsync(CreateMaintenanceLogRequest req, string userId);
    Task<IEnumerable<MaintenanceLogDto>> GetLogsAsync(string vin);
}

public class VehicleService : IVehicleService
{
    private readonly IUnitOfWork _uow;
    private readonly IHubContext<PulseHub> _hub;

    public VehicleService(IUnitOfWork uow, IHubContext<PulseHub> hub)
    {
        _uow = uow;
        _hub = hub;
    }

    public async Task<IEnumerable<VehicleDto>> GetAllAsync()
    {
        var vehicles = await _uow.Vehicles.GetAllAsync();
        return vehicles.Select(MapToDto);
    }

    public async Task<VehicleDto?> GetByVINAsync(string vin)
    {
        var v = await _uow.Vehicles.GetByVINAsync(vin);
        return v == null ? null : MapToDto(v);
    }

    public async Task<VehicleDto> CreateAsync(CreateVehicleRequest req)
    {
        var vehicle = new Vehicle
        {
            VIN = req.VIN,
            Model = req.Model,
            LicenseClass = req.LicenseClass,
            CurrentOdometer = req.CurrentOdometer,
            LastServiceOdometer = req.LastServiceOdometer,
            Notes = req.Notes,
            Status = VehicleStatus.Active
        };

        // Apply 10k rule on creation too
        if (vehicle.NeedsService)
            vehicle.Status = VehicleStatus.MaintenanceRequired;

        await _uow.Vehicles.CreateAsync(vehicle);
        await _uow.SaveChangesAsync();
        return MapToDto(vehicle);
    }

    public async Task<VehicleDto> UpdateOdometerAsync(string vin, double newOdometer)
    {
        var vehicle = await _uow.Vehicles.GetByVINAsync(vin)
            ?? throw new InvalidOperationException($"Vehicle {vin} not found.");

        if (newOdometer < vehicle.CurrentOdometer)
            throw new InvalidOperationException("Odometer cannot be decreased.");

        vehicle.CurrentOdometer = newOdometer;

        // ── 10k Threshold Rule ──────────────────────────────────────────────
        if (vehicle.NeedsService && vehicle.Status == VehicleStatus.Active)
        {
            vehicle.Status = VehicleStatus.MaintenanceRequired;

            // Push real-time maintenance alert via SignalR
            var alert = new MaintenanceAlert(
                vehicle.VIN, vehicle.Model,
                vehicle.CurrentOdometer, vehicle.LastServiceOdometer,
                vehicle.KmSinceService, vehicle.Status.ToString(),
                DateTime.UtcNow);

            await _hub.Clients.All.SendAsync("MaintenanceAlert", alert);
        }

        await _uow.Vehicles.UpdateAsync(vehicle);
        await _uow.SaveChangesAsync();

        await _hub.Clients.All.SendAsync("VehicleUpdated", vin);
        return MapToDto(vehicle);
    }

    public async Task<VehicleDto> UpdateStatusAsync(string vin, string status)
    {
        var vehicle = await _uow.Vehicles.GetByVINAsync(vin)
            ?? throw new InvalidOperationException($"Vehicle {vin} not found.");

        if (!Enum.TryParse<VehicleStatus>(status, true, out var newStatus))
            throw new InvalidOperationException($"Invalid status: {status}");

        vehicle.Status = newStatus;
        await _uow.Vehicles.UpdateAsync(vehicle);
        await _uow.SaveChangesAsync();

        await _hub.Clients.All.SendAsync("VehicleUpdated", vin);
        return MapToDto(vehicle);
    }

    public async Task<MaintenanceLogDto> LogMaintenanceAsync(CreateMaintenanceLogRequest req, string userId)
    {
        var vehicle = await _uow.Vehicles.GetByVINAsync(req.VehicleVIN)
            ?? throw new InvalidOperationException($"Vehicle {req.VehicleVIN} not found.");

        // ── Status Restoration Rule ─────────────────────────────────────────
        // Only restore to Active if vehicle was in Maintenance/MaintenanceRequired
        var wasInMaintenance = vehicle.Status == VehicleStatus.Maintenance
                            || vehicle.Status == VehicleStatus.MaintenanceRequired;

        var log = new MaintenanceLog
        {
            VehicleVIN = req.VehicleVIN,
            Description = req.Description,
            OdometerAtService = req.OdometerAtService,
            PerformedByUserId = userId,
            Cost = req.Cost,
            Parts = req.Parts,
            ServiceDate = DateTime.UtcNow
        };

        await _uow.MaintenanceLogs.CreateAsync(log);

        if (wasInMaintenance)
        {
            vehicle.LastServiceOdometer = req.OdometerAtService;
            vehicle.CurrentOdometer = Math.Max(vehicle.CurrentOdometer, req.OdometerAtService);
            vehicle.Status = VehicleStatus.Active;
            await _uow.Vehicles.UpdateAsync(vehicle);
        }

        await _uow.SaveChangesAsync();

        await _hub.Clients.All.SendAsync("VehicleUpdated", req.VehicleVIN);

        return new MaintenanceLogDto(
            log.Id, log.VehicleVIN, vehicle.Model,
            log.Description, log.OdometerAtService,
            userId, log.ServiceDate, log.Cost, log.Parts);
    }

    public async Task<IEnumerable<MaintenanceLogDto>> GetLogsAsync(string vin)
    {
        var logs = await _uow.MaintenanceLogs.GetByVehicleVINAsync(vin);
        var vehicle = await _uow.Vehicles.GetByVINAsync(vin);
        return logs.Select(l => new MaintenanceLogDto(
            l.Id, l.VehicleVIN, vehicle?.Model ?? "",
            l.Description, l.OdometerAtService,
            l.PerformedBy?.FullName ?? l.PerformedByUserId,
            l.ServiceDate, l.Cost, l.Parts));
    }

    private static VehicleDto MapToDto(Vehicle v) => new(
        v.VIN, v.Model, v.LicenseClass,
        v.CurrentOdometer, v.LastServiceOdometer,
        v.Status.ToString(), v.KmSinceService, v.NeedsService,
        v.Notes, v.CreatedAt);
}
