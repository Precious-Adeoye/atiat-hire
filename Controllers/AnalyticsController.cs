using AtiatHire.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtiatHire.Controllers;

[Authorize(Roles = "Staff")]
public class AnalyticsController : Controller
{
    private static readonly int[] AllowedRanges = { 30, 90, 365, 0 };

    private readonly AnalyticsService _analytics;

    public AnalyticsController(AnalyticsService analytics) => _analytics = analytics;

    /// <param name="days">Look-back window in days; 0 means all time.</param>
    [HttpGet]
    public async Task<IActionResult> Index(int days = 365, CancellationToken ct = default)
    {
        if (!AllowedRanges.Contains(days)) days = 365;
        return View(await _analytics.BuildAsync(days, ct));
    }
}
