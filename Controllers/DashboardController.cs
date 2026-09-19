using System.Text;
using AtiatHire.Data;
using AtiatHire.Infrastructure;
using AtiatHire.Models;
using AtiatHire.Services;
using AtiatHire.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AtiatHire.Controllers;

/// <summary>ATIAT operations dashboard: every request in one place, with status workflow and conflict flags.</summary>
[Authorize(Roles = "Staff")]
public class DashboardController : Controller
{
    private readonly AppDbContext _db;
    private readonly HireRequestService _requests;
    private readonly ConflictService _conflicts;

    public DashboardController(AppDbContext db, HireRequestService requests, ConflictService conflicts)
    {
        _db = db;
        _requests = requests;
        _conflicts = conflicts;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q, RequestStatus? status, VehicleType? vehicleType, CancellationToken ct)
    {
        var query = _db.HireRequests.AsNoTracking().Include(r => r.AssignedVehicle).AsQueryable();

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        if (vehicleType.HasValue)
            query = query.Where(r => r.PreferredVehicleType == vehicleType.Value);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            var pattern = $"%{term}%";
            query = query.Where(r =>
                EF.Functions.Like(r.ReferenceNumber, pattern) ||
                EF.Functions.Like(r.CustomerName, pattern) ||
                EF.Functions.Like(r.Phone, pattern) ||
                EF.Functions.Like(r.Email, pattern) ||
                EF.Functions.Like(r.PickupLocation, pattern) ||
                EF.Functions.Like(r.Destination, pattern));
        }

        var rows = await query.OrderByDescending(r => r.CreatedAt).Take(200).ToListAsync(ct);

        var statusCounts = (await _db.HireRequests.AsNoTracking().Select(r => r.Status).ToListAsync(ct))
            .GroupBy(s => s)
            .ToDictionary(g => g.Key, g => g.Count());

        return View(new DashboardViewModel
        {
            Requests = rows,
            Warnings = await _conflicts.GetWarningsAsync(ct),
            StatusCounts = statusCounts,
            Q = q,
            Status = status,
            VehicleType = vehicleType
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var request = await _db.HireRequests
            .AsNoTracking()
            .Include(r => r.AssignedVehicle)
            .Include(r => r.History)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (request is null) return NotFound();

        var warnings = await _conflicts.GetWarningsAsync(ct);

        return View(new RequestDetailsViewModel
        {
            Request = request,
            Vehicles = await _db.Vehicles.AsNoTracking().OrderBy(v => v.Type).ThenBy(v => v.Name).ToListAsync(ct),
            Warnings = warnings.TryGetValue(id, out var w) ? w : new List<string>(),
            NextStatuses = RequestWorkflow.NextStatuses(request.Status),
            CustomerWhatsAppUrl = WhatsAppLinkBuilder.ForStaff(request.Phone, request.CustomerName, request.ReferenceNumber)
        });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStatus(int id, RequestStatus newStatus, string? note, decimal? quotedAmount, CancellationToken ct)
    {
        if (quotedAmount.HasValue && quotedAmount.Value < 0)
        {
            TempData["Error"] = "The quoted amount cannot be negative.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var (ok, error) = await _requests.ChangeStatusAsync(
            id, newStatus, User.Identity?.Name ?? "Staff", note, quotedAmount, ct);

        if (ok) TempData["Message"] = $"Request updated to {newStatus.Humanize().ToLowerInvariant()}.";
        else TempData["Error"] = error;

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> AssignVehicle(int id, int? vehicleId, CancellationToken ct)
    {
        var request = await _db.HireRequests.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (request is null) return NotFound();

        if (!RequestWorkflow.IsOpen(request.Status))
        {
            TempData["Error"] = "Vehicles can only be changed on open requests.";
            return RedirectToAction(nameof(Details), new { id });
        }

        string description;
        if (vehicleId.HasValue)
        {
            var vehicle = await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleId.Value, ct);
            if (vehicle is null)
            {
                TempData["Error"] = "That vehicle does not exist.";
                return RedirectToAction(nameof(Details), new { id });
            }

            request.AssignedVehicleId = vehicle.Id;
            description = $"Vehicle assigned: {vehicle.Name} ({vehicle.PlateNumber})";
        }
        else
        {
            request.AssignedVehicleId = null;
            description = "Vehicle assignment removed";
        }

        _db.StatusChanges.Add(new RequestStatusChange
        {
            HireRequestId = request.Id,
            FromStatus = request.Status,
            ToStatus = request.Status,
            ChangedAt = DateTime.UtcNow,
            ChangedBy = User.Identity?.Name ?? "Staff",
            Note = description
        });

        await _db.SaveChangesAsync(ct);
        TempData["Message"] = description + ".";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> SaveNotes(int id, string? notes, CancellationToken ct)
    {
        var request = await _db.HireRequests.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (request is null) return NotFound();

        var trimmed = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (trimmed is { Length: > 2000 })
        {
            TempData["Error"] = "Notes can be up to 2,000 characters.";
            return RedirectToAction(nameof(Details), new { id });
        }

        request.StaffNotes = trimmed;
        await _db.SaveChangesAsync(ct);

        TempData["Message"] = "Notes saved.";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>Spreadsheet-friendly export for reporting.</summary>
    [HttpGet]
    public async Task<IActionResult> ExportCsv(CancellationToken ct)
    {
        var rows = await _db.HireRequests
            .AsNoTracking()
            .Include(r => r.AssignedVehicle)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(ct);

        var sb = new StringBuilder("\uFEFF");
        sb.AppendLine(string.Join(",", new[]
        {
            "Reference", "Received (Lagos)", "Customer", "Phone", "Email", "Pickup", "Destination",
            "Pickup date/time", "Duration (h)", "Passengers", "Vehicle preference", "Driver", "Purpose",
            "Status", "Assigned vehicle", "Quoted amount (NGN)", "First response (Lagos)", "Closed (Lagos)"
        }));

        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(",", new[]
            {
                Csv(r.ReferenceNumber),
                Csv(r.CreatedAt.ToLagosDisplay()),
                Csv(r.CustomerName),
                Csv(r.Phone),
                Csv(r.Email),
                Csv(r.PickupLocation),
                Csv(r.Destination),
                Csv(r.PickupDateTime.ToString("yyyy-MM-dd HH:mm")),
                Csv(r.DurationHours.ToString()),
                Csv(r.Passengers.ToString()),
                Csv(r.PreferredVehicleType.Humanize()),
                Csv(r.DriverRequirement.Humanize()),
                Csv(r.Purpose.Humanize()),
                Csv(r.Status.Humanize()),
                Csv(r.AssignedVehicle?.Name),
                Csv(r.QuotedAmount?.ToString("0.00")),
                Csv(r.FirstRespondedAt.HasValue ? r.FirstRespondedAt.ToLagosDisplay() : null),
                Csv(r.ClosedAt.HasValue ? r.ClosedAt.ToLagosDisplay() : null)
            }));
        }

        var fileName = $"atiat-vehicle-hire-requests-{TimeHelper.LagosNow:yyyyMMdd}.csv";
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", fileName);
    }

    /// <summary>Quotes a CSV cell and neutralises spreadsheet formula injection (=, +, -, @ at the start).</summary>
    private static string Csv(string? value)
    {
        value ??= string.Empty;
        if (value.Length > 0 && "=+-@".Contains(value[0]))
            value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
