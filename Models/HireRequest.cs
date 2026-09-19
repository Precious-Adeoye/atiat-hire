using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AtiatHire.Models;

public class HireRequest
{
    public int Id { get; set; }

    /// <summary>Public reference such as ATIAT-HR-00125.</summary>
    [MaxLength(40)]
    public string ReferenceNumber { get; set; } = string.Empty;

    // --- Customer ---
    [MaxLength(120)] public string CustomerName { get; set; } = string.Empty;
    [MaxLength(30)] public string Phone { get; set; } = string.Empty;
    [MaxLength(200)] public string Email { get; set; } = string.Empty;

    // --- Trip ---
    [MaxLength(200)] public string PickupLocation { get; set; } = string.Empty;
    [MaxLength(200)] public string Destination { get; set; } = string.Empty;

    /// <summary>Pickup date and time exactly as the customer entered it (Lagos local time, WAT).</summary>
    public DateTime PickupDateTime { get; set; }

    public int DurationHours { get; set; }
    public int Passengers { get; set; }
    public VehicleType PreferredVehicleType { get; set; }
    public DriverRequirement DriverRequirement { get; set; }
    public TripPurpose Purpose { get; set; }

    [MaxLength(1000)]
    public string? AdditionalInformation { get; set; }

    // --- Operations ---
    public RequestStatus Status { get; set; } = RequestStatus.Pending;

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC. Set when staff first act on the request (used for response-time metrics).</summary>
    public DateTime? FirstRespondedAt { get; set; }

    /// <summary>UTC. Set when the request is Completed or Cancelled.</summary>
    public DateTime? ClosedAt { get; set; }

    public int? AssignedVehicleId { get; set; }
    public Vehicle? AssignedVehicle { get; set; }

    public decimal? QuotedAmount { get; set; }

    [MaxLength(2000)]
    public string? StaffNotes { get; set; }

    public List<RequestStatusChange> History { get; set; } = new();

    [NotMapped]
    public DateTime PickupEndDateTime => PickupDateTime.AddHours(DurationHours);
}
