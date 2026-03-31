namespace SwiftRoute.API.DTOs;

// ── Auth ─────────────────────────────────────────────────────────────────────
public record LoginRequest(string Username, string Password);
public record LoginResponse(string Token, string UserId, string FullName, string Role, DateTime ExpiresAt);

// ── Driver DTOs ───────────────────────────────────────────────────────────────
public record DriverDto(
    string Id, string Name, string LicenseClass,
    DateTime LicenseExpiry, string Status,
    string? Phone, string? Email,
    bool IsLicenseValid, DateTime CreatedAt);

public record CreateDriverRequest(
    string Id, string Name, string LicenseClass,
    DateTime LicenseExpiry, string? Phone, string? Email);

public record UpdateDriverRequest(
    string Name, string LicenseClass,
    DateTime LicenseExpiry, string Status,
    string? Phone, string? Email);

// ── Vehicle DTOs ──────────────────────────────────────────────────────────────
public record VehicleDto(
    string VIN, string Model, string LicenseClass,
    double CurrentOdometer, double LastServiceOdometer,
    string Status, double KmSinceService, bool NeedsService,
    string? Notes, DateTime CreatedAt);

public record CreateVehicleRequest(
    string VIN, string Model, string LicenseClass,
    double CurrentOdometer, double LastServiceOdometer,
    string? Notes);

public record UpdateVehicleRequest(
    string Model, string LicenseClass,
    double CurrentOdometer, string Status, string? Notes);

public record UpdateOdometerRequest(double NewOdometer);

// ── Assignment DTOs ───────────────────────────────────────────────────────────
public record AssignmentDto(
    int Id, string DriverId, string DriverName,
    string VehicleVIN, string VehicleModel,
    string Route, string Origin, string Destination,
    DateTime ScheduledStart, DateTime? ScheduledEnd,
    DateTime? ActualStart, DateTime? ActualEnd,
    string Status, string CreatedByName, DateTime CreatedAt);

public record CreateAssignmentRequest(
    string DriverId, string VehicleVIN,
    string Route, string Origin, string Destination,
    DateTime ScheduledStart, DateTime? ScheduledEnd);

public record UpdateAssignmentStatusRequest(string Status);

// ── Maintenance DTOs ──────────────────────────────────────────────────────────
public record MaintenanceLogDto(
    int Id, string VehicleVIN, string VehicleModel,
    string Description, double OdometerAtService,
    string PerformedByName, DateTime ServiceDate,
    decimal Cost, string? Parts);

public record CreateMaintenanceLogRequest(
    string VehicleVIN, string Description,
    double OdometerAtService, decimal Cost, string? Parts);

// ── Dashboard DTOs ────────────────────────────────────────────────────────────
public record DashboardStats(
    int TotalDrivers, int AvailableDrivers, int AssignedDrivers,
    int TotalVehicles, int ActiveVehicles, int VehiclesInMaintenance,
    int ActiveAssignments, int PendingAssignments,
    int VehiclesNeedingService, int ExpiredLicenses);

public record MaintenanceAlert(
    string VehicleVIN, string VehicleModel,
    double CurrentOdometer, double LastServiceOdometer,
    double KmSinceService, string Status, DateTime AlertedAt);

// ── Error ─────────────────────────────────────────────────────────────────────
public record ErrorResponse(string Message, string? Detail = null);
