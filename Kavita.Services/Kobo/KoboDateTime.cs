using System;

namespace Kavita.Services.Kobo;

/// <summary>
/// UTC normalization helpers for Kobo wire timestamps and entity date fields.
/// Kavita/SQLite commonly returns UTC timestamps as <see cref="DateTimeKind.Unspecified"/>.
/// </summary>
public static class KoboDateTime
{
    public static DateTime AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();

    /// <summary>
    /// Prefer the UTC column when set; otherwise fall back to the local/legacy column.
    /// </summary>
    public static DateTime CoalesceUtc(DateTime utc, DateTime local) =>
        AsUtc(utc == default ? local : utc);

    /// <summary>
    /// Kobo wire timestamp: UTC, second precision, trailing <c>Z</c>.
    /// </summary>
    public static string FormatTimestamp(DateTime value) =>
        AsUtc(value).ToString("yyyy-MM-ddTHH:mm:ssZ");
}
