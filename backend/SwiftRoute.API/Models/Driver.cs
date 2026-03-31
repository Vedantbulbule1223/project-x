namespace SwiftRoute.API.Models;

public class Driver
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string LicenseClass { get; set; } = string.Empty; // ClassA, ClassB
    public DateTime LicenseExpiry { get; set; }
    public DriverStatus Status { get; set; } = DriverStatus.Available;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Concurrency token (optimistic locking)
    public uint RowVersion { get; set; }

    // Navigation
    public ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();

    // Computed
    public bool IsLicenseValid => LicenseExpiry > DateTime.UtcNow;
}

public enum DriverStatus
{
    Available,
    Assigned,
    OffDuty,
    Suspended
}
