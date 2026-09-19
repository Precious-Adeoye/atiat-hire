using System.ComponentModel.DataAnnotations;
using AtiatHire.Models;

namespace AtiatHire.ViewModels;

public record LabelCount(string Label, int Count);

public record BarListViewModel(string Title, IReadOnlyList<LabelCount> Items, string EmptyText = "No data yet.");

public class ConfirmationViewModel
{
    public required HireRequest Request { get; init; }
    public required string WhatsAppUrl { get; init; }
}

public class TrackResultViewModel
{
    public required HireRequest Request { get; init; }
    public required string WhatsAppUrl { get; init; }
}

public class DashboardViewModel
{
    public List<HireRequest> Requests { get; set; } = new();
    public Dictionary<int, List<string>> Warnings { get; set; } = new();
    public Dictionary<RequestStatus, int> StatusCounts { get; set; } = new();
    public string? Q { get; set; }
    public RequestStatus? Status { get; set; }
    public VehicleType? VehicleType { get; set; }
}

public class RequestDetailsViewModel
{
    public required HireRequest Request { get; init; }
    public List<Vehicle> Vehicles { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public IReadOnlyList<RequestStatus> NextStatuses { get; init; } = Array.Empty<RequestStatus>();
    public required string CustomerWhatsAppUrl { get; init; }
}

public class VehicleForm
{
    [Required(ErrorMessage = "Enter the vehicle name."), StringLength(80)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter the plate number."), StringLength(20)]
    [Display(Name = "Plate number")]
    public string PlateNumber { get; set; } = string.Empty;

    public VehicleType Type { get; set; } = VehicleType.SUV;

    [Range(1, 70, ErrorMessage = "Seats must be between 1 and 70.")]
    [Display(Name = "Seats")]
    public int Capacity { get; set; } = 4;

    [StringLength(300)]
    public string? Notes { get; set; }
}

public class FleetRow
{
    public required Vehicle Vehicle { get; init; }
    public HireRequest? NextHire { get; init; }
}

public class FleetViewModel
{
    public List<FleetRow> Rows { get; set; } = new();
    public VehicleForm NewVehicle { get; set; } = new();
}

public class AnalyticsViewModel
{
    public int Days { get; set; }
    public int Total { get; set; }
    public int Pending { get; set; }
    public int Open { get; set; }
    public int Completed { get; set; }
    public int Cancelled { get; set; }
    public double? AverageResponseHours { get; set; }

    public List<LabelCount> VehicleTypes { get; set; } = new();
    public List<LabelCount> Purposes { get; set; } = new();
    public List<LabelCount> TopPickups { get; set; } = new();
    public List<LabelCount> TopDestinations { get; set; } = new();
    public List<LabelCount> Statuses { get; set; } = new();
    public List<LabelCount> PerMonth { get; set; } = new();
    public List<LabelCount> PickupWeekdays { get; set; } = new();
    public List<string> Insights { get; set; } = new();
}
