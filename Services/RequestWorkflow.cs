using AtiatHire.Models;

namespace AtiatHire.Services;

/// <summary>
/// The allowed status transitions.
/// Pending -> Reviewed -> Quoted -> Confirmed -> Completed, and any open request can be Cancelled.
/// </summary>
public static class RequestWorkflow
{
    private static readonly Dictionary<RequestStatus, RequestStatus[]> Transitions = new()
    {
        [RequestStatus.Pending]   = new[] { RequestStatus.Reviewed, RequestStatus.Cancelled },
        [RequestStatus.Reviewed]  = new[] { RequestStatus.Quoted, RequestStatus.Cancelled },
        [RequestStatus.Quoted]    = new[] { RequestStatus.Confirmed, RequestStatus.Cancelled },
        [RequestStatus.Confirmed] = new[] { RequestStatus.Completed, RequestStatus.Cancelled },
    };

    public static IReadOnlyList<RequestStatus> NextStatuses(RequestStatus current) =>
        Transitions.TryGetValue(current, out var next) ? next : Array.Empty<RequestStatus>();

    public static bool CanMove(RequestStatus from, RequestStatus to) => NextStatuses(from).Contains(to);

    public static bool IsOpen(RequestStatus s) =>
        s != RequestStatus.Completed && s != RequestStatus.Cancelled;

    /// <summary>Quoted and Confirmed requests count as commitments when checking scheduling conflicts.</summary>
    public static bool IsCommitted(RequestStatus s) =>
        s == RequestStatus.Quoted || s == RequestStatus.Confirmed;

    public static string Describe(RequestStatus s) => s switch
    {
        RequestStatus.Pending   => "We have received your request.",
        RequestStatus.Reviewed  => "Our team has reviewed your request.",
        RequestStatus.Quoted    => "A quote has been prepared for you.",
        RequestStatus.Confirmed => "Your vehicle hire is confirmed.",
        RequestStatus.Completed => "This hire has been completed.",
        RequestStatus.Cancelled => "This request was cancelled.",
        _ => string.Empty
    };
}
