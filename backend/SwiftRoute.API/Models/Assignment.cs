namespace SwiftRoute.API.Models;

public class Assignment
{
    public int Id { get; set; }
    public string DriverId { get; set; } = string.Empty;
    public string VehicleVIN { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public DateTime ScheduledStart { get; set; }
    public DateTime? ScheduledEnd { get; set; }
    public DateTime? ActualStart { get; set; }
    public DateTime? ActualEnd { get; set; }
    public AssignmentStatus Status { get; set; } = AssignmentStatus.Pending;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Concurrency token (optimistic locking)
    public uint RowVersion { get; set; }

    // Navigation
    public Driver Driver { get; set; } = null!;
    public Vehicle Vehicle { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
}

public enum AssignmentStatus
{
    Pending,
    Active,
    Completed,
    Cancelled
}
