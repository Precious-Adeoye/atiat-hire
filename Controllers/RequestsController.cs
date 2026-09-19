using AtiatHire.Data;
using AtiatHire.Infrastructure;
using AtiatHire.Services;
using AtiatHire.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace AtiatHire.Controllers;

/// <summary>Customer portal: submit a request, see the reference number, follow the status.</summary>
public class RequestsController : Controller
{
    private const string ReferenceKey = "SubmittedReference";

    private readonly AppDbContext _db;
    private readonly HireRequestService _requests;
    private readonly WhatsAppLinkBuilder _whatsApp;

    public RequestsController(AppDbContext db, HireRequestService requests, WhatsAppLinkBuilder whatsApp)
    {
        _db = db;
        _requests = requests;
        _whatsApp = whatsApp;
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new HireRequestForm
        {
            PickupDateTime = TimeHelper.LagosNow.Date.AddDays(1).AddHours(9)
        });
    }

    [HttpPost]
    [EnableRateLimiting("forms")]
    public async Task<IActionResult> Create(HireRequestForm form, CancellationToken ct)
    {
        // Honeypot: real people never see or fill this field. Pretend success to bots.
        if (!string.IsNullOrWhiteSpace(form.Website))
            return RedirectToAction(nameof(Track));

        if (!ModelState.IsValid)
            return View(form);

        var request = await _requests.CreateAsync(form, ct);

        // Confirmation is shown once via TempData rather than a guessable URL, because references are sequential.
        TempData[ReferenceKey] = request.ReferenceNumber;
        return RedirectToAction(nameof(Confirmation));
    }

    [HttpGet]
    public async Task<IActionResult> Confirmation(CancellationToken ct)
    {
        var reference = TempData.Peek(ReferenceKey) as string;
        if (string.IsNullOrEmpty(reference))
            return RedirectToAction(nameof(Track));

        var request = await _db.HireRequests.AsNoTracking()
            .FirstOrDefaultAsync(r => r.ReferenceNumber == reference, ct);
        if (request is null)
            return RedirectToAction(nameof(Track));

        return View(new ConfirmationViewModel
        {
            Request = request,
            WhatsAppUrl = _whatsApp.ForCustomer(request.ReferenceNumber)
        });
    }

    [HttpGet]
    public IActionResult Track() => View(new TrackForm());

    [HttpPost]
    [EnableRateLimiting("forms")]
    public async Task<IActionResult> Track(TrackForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(form);

        // One generic message for "no such reference" and "wrong phone" so references can't be probed.
        const string notFound = "We couldn't find a request that matches those details. Check the reference number and phone number and try again.";

        if (!HireRequestService.TryParseReference(form.Reference, out var reference))
        {
            ModelState.AddModelError(string.Empty, notFound);
            return View(form);
        }

        var request = await _db.HireRequests.AsNoTracking()
            .Include(r => r.History)
            .FirstOrDefaultAsync(r => r.ReferenceNumber == reference, ct);

        if (request is null || !PhoneHelper.Matches(request.Phone, form.Phone))
        {
            ModelState.AddModelError(string.Empty, notFound);
            return View(form);
        }

        return View("TrackResult", new TrackResultViewModel
        {
            Request = request,
            WhatsAppUrl = _whatsApp.ForCustomer(request.ReferenceNumber)
        });
    }
}
