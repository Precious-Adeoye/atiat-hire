using AtiatHire.Data;
using AtiatHire.Models;
using AtiatHire.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AtiatHire.Services;

public class HireRequestService
{
    public const string ReferencePrefix = "ATIAT-HR-";

    private readonly AppDbContext _db;

    public HireRequestService(AppDbContext db) => _db = db;

    public static string FormatReference(int id) => $"{ReferencePrefix}{id:D5}";

    /// <summary>
    /// Accepts "ATIAT-HR-00125", "HR-00125", "125" and similar, and returns the canonical reference.
    /// </summary>
    public static bool TryParseReference(string? input, out string reference)
    {
        reference = string.Empty;
        var digits = new string((input ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length == 0 || digits.Length > 9 || !int.TryParse(digits, out var id) || id <= 0)
            return false;

        reference = FormatReference(id);
        return true;
    }

    public async Task<HireRequest> CreateAsync(HireRequestForm form, CancellationToken ct = default)
    {
        var entity = new HireRequest
        {
            // Temporary unique value; replaced with the sequential reference once the row has an Id.
            ReferenceNumber = "TMP-" + Guid.NewGuid().ToString("N"),
            CustomerName = form.CustomerName.Trim(),
            Phone = form.Phone.Trim(),
            Email = form.Email.Trim(),
            PickupLocation = form.PickupLocation.Trim(),
            Destination = form.Destination.Trim(),
            PickupDateTime = form.PickupDateTime!.Value,
            DurationHours = form.DurationHours,
            Passengers = form.Passengers,
            PreferredVehicleType = form.PreferredVehicleType,
            DriverRequirement = form.DriverRequirement,
            Purpose = form.Purpose,
            AdditionalInformation = string.IsNullOrWhiteSpace(form.AdditionalInformation)
                ? null
                : form.AdditionalInformation.Trim(),
            Status = RequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        entity.History.Add(new RequestStatusChange
        {
            FromStatus = null,
            ToStatus = RequestStatus.Pending,
            ChangedAt = entity.CreatedAt,
            ChangedBy = "Customer",
            Note = "Request submitted online"
        });

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        _db.HireRequests.Add(entity);
        await _db.SaveChangesAsync(ct);

        entity.ReferenceNumber = FormatReference(entity.Id);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return entity;
    }

    public async Task<(bool Ok, string? Error)> ChangeStatusAsync(
        int id, RequestStatus to, string changedBy, string? note, decimal? quotedAmount,
        CancellationToken ct = default)
    {
        var request = await _db.HireRequests.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (request is null) return (false, "Request not found.");

        if (!RequestWorkflow.CanMove(request.Status, to))
            return (false, $"A {request.Status} request cannot be moved to {to}.");

        var now = DateTime.UtcNow;
        var from = request.Status;

        request.Status = to;

        if (from == RequestStatus.Pending && to != RequestStatus.Cancelled && request.FirstRespondedAt is null)
            request.FirstRespondedAt = now;

        if (to == RequestStatus.Quoted && quotedAmount.HasValue)
            request.QuotedAmount = quotedAmount.Value;

        if (to == RequestStatus.Completed || to == RequestStatus.Cancelled)
            request.ClosedAt = now;

        _db.StatusChanges.Add(new RequestStatusChange
        {
            HireRequestId = request.Id,
            FromStatus = from,
            ToStatus = to,
            ChangedAt = now,
            ChangedBy = changedBy,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
        });

        await _db.SaveChangesAsync(ct);
        return (true, null);
    }
}
