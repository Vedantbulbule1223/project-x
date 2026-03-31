using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SwiftRoute.API.DTOs;
using SwiftRoute.API.Models;
using SwiftRoute.API.Repositories;
using SwiftRoute.API.Services;
using System.Security.Claims;

namespace SwiftRoute.API.Controllers;

// ── Auth ──────────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        try { return Ok(await _auth.LoginAsync(req)); }
        catch (UnauthorizedAccessException ex) { return Unauthorized(new ErrorResponse(ex.Message)); }
    }
}

// ── Dashboard ─────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IUnitOfWork _uow;
    public DashboardController(IUnitOfWork uow) => _uow = uow;

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var drivers = (await _uow.Drivers.GetAllAsync()).ToList();
        var vehicles = (await _uow.Vehicles.GetAllAsync()).ToList();
        var assignments = (await _uow.Assignments.GetAllAsync()).ToList();

        var stats = new DashboardStats(
            drivers.Count,
            drivers.Count(d => d.Status == DriverStatus.Available),
            drivers.Count(d => d.Status == DriverStatus.Assigned),
            vehicles.Count,
            vehicles.Count(v => v.Status == VehicleStatus.Active),
            vehicles.Count(v => v.Status is VehicleStatus.Maintenance or VehicleStatus.MaintenanceRequired),
            assignments.Count(a => a.Status == AssignmentStatus.Active),
            assignments.Count(a => a.Status == AssignmentStatus.Pending),
            vehicles.Count(v => v.NeedsService),
            drivers.Count(d => !d.IsLicenseValid)
        );
        return Ok(stats);
    }

    [HttpGet("alerts")]
    public async Task<IActionResult> GetMaintenanceAlerts()
    {
        var vehicles = (await _uow.Vehicles.GetVehiclesNeedingServiceAsync()).ToList();
        var alerts = vehicles.Select(v => new MaintenanceAlert(
            v.VIN, v.Model, v.CurrentOdometer, v.LastServiceOdometer,
            v.KmSinceService, v.Status.ToString(), DateTime.UtcNow));
        return Ok(alerts);
    }
}

// ── Drivers ───────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DriversController : ControllerBase
{
    private readonly IUnitOfWork _uow;
    public DriversController(IUnitOfWork uow) => _uow = uow;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var drivers = await _uow.Drivers.GetAllAsync();
        return Ok(drivers.Select(MapToDto));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var d = await _uow.Drivers.GetByIdAsync(id);
        return d == null ? NotFound() : Ok(MapToDto(d));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager,Dispatcher")]
    public async Task<IActionResult> Create([FromBody] CreateDriverRequest req)
    {
        var driver = new Driver
        {
            Id = req.Id,
            Name = req.Name,
            LicenseClass = req.LicenseClass,
            LicenseExpiry = req.LicenseExpiry,
            Phone = req.Phone,
            Email = req.Email
        };
        await _uow.Drivers.CreateAsync(driver);
        await _uow.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = driver.Id }, MapToDto(driver));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateDriverRequest req)
    {
        var driver = await _uow.Drivers.GetByIdAsync(id);
        if (driver == null) return NotFound();

        driver.Name = req.Name;
        driver.LicenseClass = req.LicenseClass;
        driver.LicenseExpiry = req.LicenseExpiry;
        driver.Phone = req.Phone;
        driver.Email = req.Email;
        if (Enum.TryParse<DriverStatus>(req.Status, true, out var s)) driver.Status = s;

        await _uow.Drivers.UpdateAsync(driver);
        await _uow.SaveChangesAsync();
        return Ok(MapToDto(driver));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        await _uow.Drivers.DeleteAsync(id);
        await _uow.SaveChangesAsync();
        return NoContent();
    }

    private static DriverDto MapToDto(Driver d) => new(
        d.Id, d.Name, d.LicenseClass, d.LicenseExpiry,
        d.Status.ToString(), d.Phone, d.Email, d.IsLicenseValid, d.CreatedAt);
}

// ── Vehicles ──────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VehiclesController : ControllerBase
{
    private readonly IVehicleService _svc;
    public VehiclesController(IVehicleService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _svc.GetAllAsync());

    [HttpGet("{vin}")]
    public async Task<IActionResult> GetByVIN(string vin)
    {
        var v = await _svc.GetByVINAsync(vin);
        return v == null ? NotFound() : Ok(v);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Create([FromBody] CreateVehicleRequest req)
    {
        try
        {
            var result = await _svc.CreateAsync(req);
            return CreatedAtAction(nameof(GetByVIN), new { vin = result.VIN }, result);
        }
        catch (InvalidOperationException ex) { return BadRequest(new ErrorResponse(ex.Message)); }
    }

    [HttpPatch("{vin}/odometer")]
    [Authorize(Roles = "Admin,Manager,Technician")]
    public async Task<IActionResult> UpdateOdometer(string vin, [FromBody] UpdateOdometerRequest req)
    {
        try { return Ok(await _svc.UpdateOdometerAsync(vin, req.NewOdometer)); }
        catch (InvalidOperationException ex) { return BadRequest(new ErrorResponse(ex.Message)); }
    }

    [HttpPatch("{vin}/status")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> UpdateStatus(string vin, [FromBody] UpdateAssignmentStatusRequest req)
    {
        try { return Ok(await _svc.UpdateStatusAsync(vin, req.Status)); }
        catch (InvalidOperationException ex) { return BadRequest(new ErrorResponse(ex.Message)); }
    }

    [HttpGet("{vin}/logs")]
    public async Task<IActionResult> GetLogs(string vin) => Ok(await _svc.GetLogsAsync(vin));

    [HttpPost("maintenance")]
    [Authorize(Roles = "Admin,Manager,Technician")]
    public async Task<IActionResult> LogMaintenance([FromBody] CreateMaintenanceLogRequest req)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
        try { return Ok(await _svc.LogMaintenanceAsync(req, userId)); }
        catch (InvalidOperationException ex) { return BadRequest(new ErrorResponse(ex.Message)); }
    }
}

// ── Assignments ───────────────────────────────────────────────────────────────
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AssignmentsController : ControllerBase
{
    private readonly IAssignmentService _svc;
    public AssignmentsController(IAssignmentService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _svc.GetAllAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var a = await _svc.GetByIdAsync(id);
        return a == null ? NotFound() : Ok(a);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager,Dispatcher")]
    public async Task<IActionResult> Create([FromBody] CreateAssignmentRequest req)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
        try
        {
            var result = await _svc.CreateAssignmentAsync(req, userId);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse(ex.Message));
        }
    }

    [HttpPatch("{id}/status")]
    [Authorize(Roles = "Admin,Manager,Dispatcher")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateAssignmentStatusRequest req)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
        try { return Ok(await _svc.UpdateStatusAsync(id, req.Status, userId)); }
        catch (InvalidOperationException ex) { return BadRequest(new ErrorResponse(ex.Message)); }
    }
}
