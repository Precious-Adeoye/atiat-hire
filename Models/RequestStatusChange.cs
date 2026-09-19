using System.ComponentModel.DataAnnotations;

namespace AtiatHire.Models;

/// <summary>Audit trail for a request. FromStatus == ToStatus means a non-status update (for example a vehicle assignment).</summary>
public class RequestStatusChange
{
    public int Id { get; set; }
    public int HireRequestId { get; set; }
    public HireRequest? HireRequest { get; set; }

    public RequestStatus? FromStatus { get; set; }
    public RequestStatus ToStatus { get; set; }

    /// <summary>UTC.</summary>
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string ChangedBy { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Note { get; set; }
}
