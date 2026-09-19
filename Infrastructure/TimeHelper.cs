namespace AtiatHire.Infrastructure;

/// <summary>
/// Nigeria (West Africa Time) is UTC+1 all year with no daylight saving,
/// so a fixed offset is safe and avoids OS time-zone database differences.
/// Timestamps are stored in UTC; pickup times are stored exactly as entered (Lagos local).
/// </summary>
public static class TimeHelper
{
    private static readonly TimeSpan Wat = TimeSpan.FromHours(1);

    public static DateTime LagosNow => DateTime.UtcNow + Wat;

    public static DateTime ToLagos(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).Add(Wat);

    public static string ToLagosDisplay(this DateTime utc) =>
        ToLagos(utc).ToString("d MMM yyyy, HH:mm");

    public static string ToLagosDisplay(this DateTime? utc) =>
        utc.HasValue ? utc.Value.ToLagosDisplay() : "-";
}
