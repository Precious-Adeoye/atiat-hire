using AtiatHire.Data;
using AtiatHire.Infrastructure;
using AtiatHire.Models;
using AtiatHire.Services;
using AtiatHire.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AtiatHire.Controllers;

[Authorize(Roles = "Staff")]
public class FleetController : Controller
{
    private readonly AppDbContext _db;

    public FleetController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(await BuildAsync(new VehicleForm(), ct));

    [HttpPost]
    public async Task<IActionResult> SetStatus(int id, VehicleStatus status, CancellationToken ct)
    {
        var vehicle = await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (vehicle is null) return NotFound();

        vehicle.Status = status;
        await _db.SaveChangesAsync(ct);

        TempData["Message"] = $"{vehicle.Name} is now {status.Humanize().ToLowerInvariant()}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Add([Bind(Prefix = "NewVehicle")] VehicleForm form, CancellationToken ct)
    {
        var plate = form.PlateNumber?.Trim().ToUpperInvariant() ?? string.Empty;

        if (plate.Length > 0 && await _db.Vehicles.AnyAsync(v => v.PlateNumber == plate, ct))
            ModelState.AddModelError("NewVehicle.PlateNumber", "A vehicle with this plate number already exists.");

        if (!ModelState.IsValid)
            return View("Index", await BuildAsync(form, ct));

        _db.Vehicles.Add(new Vehicle
        {
            Name = form.Name.Trim(),
            PlateNumber = plate,
            Type = form.Type,
            Capacity = form.Capacity,
            Notes = string.IsNullOrWhiteSpace(form.Notes) ? null : form.Notes.Trim(),
            Status = VehicleStatus.Available
        });
        await _db.SaveChangesAsync(ct);

        TempData["Message"] = $"{form.Name.Trim()} added to the fleet.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<FleetViewModel> BuildAsync(VehicleForm form, CancellationToken ct)
    {
        var vehicles = await _db.Vehicles.AsNoTracking().OrderBy(v => v.Type).ThenBy(v => v.Name).ToListAsync(ct);

        var committed = await _db.HireRequests.AsNoTracking()
            .Where(r => r.AssignedVehicleId != null &&
                        (r.Status == RequestStatus.Quoted || r.Status == RequestStatus.Confirmed))
            .ToListAsync(ct);

        var nowLagos = TimeHelper.LagosNow;

        var rows = vehicles.Select(v => new FleetRow
        {
            Vehicle = v,
            NextHire = committed
                .Where(r => r.AssignedVehicleId == v.Id && r.PickupEndDateTime >= nowLagos)
                .OrderBy(r => r.PickupDateTime)
                .FirstOrDefault()
        }).ToList();

        return new FleetViewModel { Rows = rows, NewVehicle = form };
    }
}
