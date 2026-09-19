using AtiatHire.Data;
using AtiatHire.Models;
using Microsoft.EntityFrameworkCore;

namespace AtiatHire.Services;

/// <summary>
/// Flags potential scheduling conflicts. It does not block anything: staff stay in control.
///
/// Only Quoted and Confirmed requests count as commitments. Two rules:
///  1. Request has a vehicle assigned  -> warn if that vehicle is in maintenance or already
///     committed to an overlapping request.
///  2. No vehicle assigned yet          -> warn if every non-maintenance vehicle of the preferred
///     type is already committed during the same period (or none exists).
/// </summary>
public class ConflictService
{
    private readonly AppDbContext _db;

    public ConflictService(AppDbContext db) => _db = db;

    /// <summary>Warnings keyed by request id, for every open request.</summary>
    public async Task<Dictionary<int, List<string>>> GetWarningsAsync(CancellationToken ct = default)
    {
        var open = await _db.HireRequests
            .AsNoTracking()
            .Include(r => r.AssignedVehicle)
            .Where(r => r.Status != RequestStatus.Completed && r.Status != RequestStatus.Cancelled)
            .ToListAsync(ct);

        var fleet = await _db.Vehicles.AsNoTracking().ToListAsync(ct);

        return Analyze(open, fleet);
    }

    public static Dictionary<int, List<string>> Analyze(IReadOnlyList<HireRequest> open, IReadOnlyList<Vehicle> fleet)
    {
        var result = new Dictionary<int, List<string>>();
        var committed = open.Where(r => RequestWorkflow.IsCommitted(r.Status)).ToList();

        foreach (var request in open)
        {
            var warnings = new List<string>();

            if (request.AssignedVehicle is not null)
            {
                var vehicle = request.AssignedVehicle;

                if (vehicle.Status == VehicleStatus.Maintenance)
                    warnings.Add($"{vehicle.Name} ({vehicle.PlateNumber}) is marked as under maintenance.");

                foreach (var other in committed.Where(o =>
                             o.Id != request.Id &&
                             o.AssignedVehicleId == vehicle.Id &&
                             Overlaps(request, o)))
                {
                    warnings.Add(
                        $"{vehicle.Name} is already assigned to {other.ReferenceNumber} " +
                        $"({other.PickupDateTime:d MMM HH:mm} to {other.PickupEndDateTime:d MMM HH:mm}).");
                }
            }
            else
            {
                var type = request.PreferredVehicleType;
                var capacity = fleet.Count(v => v.Type == type && v.Status != VehicleStatus.Maintenance);

                if (capacity == 0)
                {
                    warnings.Add($"No {type} vehicle is currently in service in the fleet.");
                }
                else
                {
                    var overlapping = committed.Count(o =>
                        o.Id != request.Id && TypeOf(o) == type && Overlaps(request, o));

                    if (overlapping >= capacity)
                        warnings.Add($"All {capacity} in-service {type} vehicle(s) appear to be committed during this period.");
                }
            }

            if (warnings.Count > 0)
                result[request.Id] = warnings;
        }

        return result;
    }

    private static VehicleType TypeOf(HireRequest r) => r.AssignedVehicle?.Type ?? r.PreferredVehicleType;

    private static bool Overlaps(HireRequest a, HireRequest b) =>
        a.PickupDateTime < b.PickupEndDateTime && b.PickupDateTime < a.PickupEndDateTime;
}
