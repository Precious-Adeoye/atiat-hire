using System.Globalization;
using AtiatHire.Data;
using AtiatHire.Infrastructure;
using AtiatHire.Models;
using AtiatHire.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AtiatHire.Services;

/// <summary>
/// The "Vehicle Hire Operations Intelligence" layer: turns raw requests into demand and performance metrics.
/// The data set is small (one company's requests), so rows are projected once and grouped in memory.
/// That keeps the logic readable and independent of database-specific SQL translation.
/// </summary>
public class AnalyticsService
{
    private readonly AppDbContext _db;

    public AnalyticsService(AppDbContext db) => _db = db;

    public async Task<AnalyticsViewModel> BuildAsync(int days, CancellationToken ct = default)
    {
        var query = _db.HireRequests.AsNoTracking().AsQueryable();

        if (days > 0)
        {
            var cutoff = DateTime.UtcNow.AddDays(-days);
            query = query.Where(r => r.CreatedAt >= cutoff);
        }

        var rows = await query
            .Select(r => new
            {
                r.Status,
                r.CreatedAt,
                r.FirstRespondedAt,
                r.PickupDateTime,
                r.PickupLocation,
                r.Destination,
                r.PreferredVehicleType,
                r.Purpose
            })
            .ToListAsync(ct);

        var vm = new AnalyticsViewModel
        {
            Days = days,
            Total = rows.Count,
            Pending = rows.Count(r => r.Status == RequestStatus.Pending),
            Open = rows.Count(r => RequestWorkflow.IsOpen(r.Status)),
            Completed = rows.Count(r => r.Status == RequestStatus.Completed),
            Cancelled = rows.Count(r => r.Status == RequestStatus.Cancelled)
        };

        // Average response time: submission -> first staff action.
        var responseHours = rows
            .Where(r => r.FirstRespondedAt.HasValue)
            .Select(r => (r.FirstRespondedAt!.Value - r.CreatedAt).TotalHours)
            .ToList();
        vm.AverageResponseHours = responseHours.Count > 0 ? responseHours.Average() : null;

        vm.VehicleTypes = CountBy(rows.Select(r => r.PreferredVehicleType.Humanize()));
        vm.Purposes = CountBy(rows.Select(r => r.Purpose.Humanize()));
        vm.TopPickups = Top(rows.Select(r => r.PickupLocation), 8);
        vm.TopDestinations = Top(rows.Select(r => r.Destination), 8);

        vm.Statuses = Enum.GetValues<RequestStatus>()
            .Select(s => new LabelCount(s.Humanize(), rows.Count(r => r.Status == s)))
            .ToList();

        // Requests received per month, last 12 months (zero-filled so gaps are visible).
        var nowLagos = TimeHelper.LagosNow;
        var firstMonth = new DateTime(nowLagos.Year, nowLagos.Month, 1).AddMonths(-11);
        var perMonth = rows
            .GroupBy(r =>
            {
                var local = TimeHelper.ToLagos(r.CreatedAt);
                return new DateTime(local.Year, local.Month, 1);
            })
            .ToDictionary(g => g.Key, g => g.Count());

        vm.PerMonth = Enumerable.Range(0, 12)
            .Select(i => firstMonth.AddMonths(i))
            .Select(m => new LabelCount(
                m.ToString("MMM yy", CultureInfo.InvariantCulture),
                perMonth.TryGetValue(m, out var n) ? n : 0))
            .ToList();

        // Which weekdays are trips actually booked for?
        var weekdayOrder = new[]
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
            DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
        };
        vm.PickupWeekdays = weekdayOrder
            .Select(d => new LabelCount(d.ToString(), rows.Count(r => r.PickupDateTime.DayOfWeek == d)))
            .ToList();

        vm.Insights = BuildInsights(vm);
        return vm;
    }

    private static List<LabelCount> CountBy(IEnumerable<string> values) =>
        values.GroupBy(v => v)
            .Select(g => new LabelCount(g.Key, g.Count()))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Label)
            .ToList();

    /// <summary>
    /// Locations are free text, so group case-insensitively. A pick-list of common locations
    /// on the form would make this even more accurate (see README).
    /// </summary>
    private static List<LabelCount> Top(IEnumerable<string> values, int take) =>
        values.Select(v => v.Trim())
            .Where(v => v.Length > 0)
            .GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
            .Select(g => new LabelCount(g.First(), g.Count()))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Label)
            .Take(take)
            .ToList();

    private static List<string> BuildInsights(AnalyticsViewModel vm)
    {
        var insights = new List<string>();
        if (vm.Total == 0) return insights;

        var topType = vm.VehicleTypes.FirstOrDefault();
        if (topType is not null)
            insights.Add($"{topType.Label} is the most requested vehicle type ({Percent(topType.Count, vm.Total)} of requests).");

        var topPickup = vm.TopPickups.FirstOrDefault();
        if (topPickup is not null)
            insights.Add($"{Percent(topPickup.Count, vm.Total)} of requests start from {topPickup.Label}.");

        var busiestMonth = vm.PerMonth.OrderByDescending(m => m.Count).FirstOrDefault();
        if (busiestMonth is { Count: > 0 })
            insights.Add($"{busiestMonth.Label} was the busiest month for new requests ({busiestMonth.Count}).");

        var busiestDay = vm.PickupWeekdays.OrderByDescending(d => d.Count).FirstOrDefault();
        if (busiestDay is { Count: > 0 })
            insights.Add($"{busiestDay.Label} is the most common pickup day.");

        if (vm.Cancelled > 0)
            insights.Add($"{Percent(vm.Cancelled, vm.Total)} of requests were cancelled.");

        if (vm.AverageResponseHours is { } hours)
            insights.Add($"Staff take {FormatDuration(hours)} on average to first respond to a request.");

        return insights;
    }

    private static string Percent(int part, int whole) =>
        whole == 0 ? "0%" : $"{Math.Round(100.0 * part / whole):0}%";

    public static string FormatDuration(double hours)
    {
        if (hours < 1) return $"{Math.Max(1, (int)Math.Round(hours * 60))} min";
        if (hours < 48) return $"{hours:0.#} h";
        return $"{hours / 24:0.#} days";
    }
}
