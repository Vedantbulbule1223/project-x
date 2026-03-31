namespace SwiftRoute.API.Models;

public class Vehicle
{
    public string VIN { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string LicenseClass { get; set; } = string.Empty; // ClassA, ClassB
    public double CurrentOdometer { get; set; }
    public double LastServiceOdometer { get; set; }
    public VehicleStatus Status { get; set; } = VehicleStatus.Active;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Concurrency token (optimistic locking)
    public uint RowVersion { get; set; }

    // Navigation
    public ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
    public ICollection<MaintenanceLog> MaintenanceLogs { get; set; } = new List<MaintenanceLog>();

    // Computed
    public double KmSinceService => CurrentOdometer - LastServiceOdometer;
    public bool NeedsService => KmSinceService >= 10000;
}

public enum VehicleStatus
{
    Active,
    MaintenanceRequired,
    Maintenance,
    OutOfService,
    InTransit
}
