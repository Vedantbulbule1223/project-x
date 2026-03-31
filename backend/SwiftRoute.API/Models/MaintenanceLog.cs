namespace SwiftRoute.API.Models;

public class MaintenanceLog
{
    public int Id { get; set; }
    public string VehicleVIN { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double OdometerAtService { get; set; }
    public string PerformedByUserId { get; set; } = string.Empty;
    public DateTime ServiceDate { get; set; } = DateTime.UtcNow;
    public decimal Cost { get; set; }
    public string? Parts { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Vehicle Vehicle { get; set; } = null!;
    public User PerformedBy { get; set; } = null!;
}
