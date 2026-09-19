using System.ComponentModel.DataAnnotations;

namespace AtiatHire.Models;

/// <summary>Request lifecycle: Pending -> Reviewed -> Quoted -> Confirmed -> Completed (or Cancelled).</summary>
public enum RequestStatus
{
    Pending,
    Reviewed,
    Quoted,
    Confirmed,
    Completed,
    Cancelled
}

public enum VehicleType
{
    Sedan,
    SUV,
    Bus,
    Van,
    Luxury
}

public enum VehicleStatus
{
    Available,
    [Display(Name = "On hire")] OnHire,
    Maintenance
}

public enum DriverRequirement
{
    [Display(Name = "With driver")] WithDriver,
    [Display(Name = "Self-drive")] SelfDrive
}

public enum TripPurpose
{
    [Display(Name = "Business trip")] Business,
    [Display(Name = "Airport transfer")] AirportTransfer,
    Event,
    [Display(Name = "Corporate movement")] Corporate,
    Leisure,
    Other
}
