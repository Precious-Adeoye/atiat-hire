using System.ComponentModel.DataAnnotations;

namespace AtiatHire.Models;

public class Vehicle
{
    public int Id { get; set; }

    [MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string PlateNumber { get; set; } = string.Empty;

    public VehicleType Type { get; set; }

    public int Capacity { get; set; }

    /// <summary>Set manually by staff (Available / On hire / Maintenance).</summary>
    public VehicleStatus Status { get; set; } = VehicleStatus.Available;

    [MaxLength(300)]
    public string? Notes { get; set; }
}
