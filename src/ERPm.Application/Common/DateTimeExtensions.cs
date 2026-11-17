namespace ERPm.Application.Common;

public static class DateTimeExtensions
{
    // Dates bound from JSON/query strings ("YYYY-MM-DD") have Kind=Unspecified, which Npgsql rejects for timestamptz.
    public static DateTime AsUtc(this DateTime dt) =>
        dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
}
