namespace Kavita.Services.Kobo;

/// <summary>
/// HTTP header names and Calibre-Web-compatible values used by the Kobo device API.
/// </summary>
public static class KoboHttpConstants
{
    /// <summary>Response header Kobo clients expect on initialization.</summary>
    public const string ApiTokenHeaderName = "x-kobo-apitoken";

    /// <summary>Base64 of <c>{}</c> — Calibre-Web always sets this on initialization.</summary>
    public const string ApiTokenHeaderValue = "e30=";

    /// <summary>Request header carrying the device user key on analytics calls.</summary>
    public const string UserKeyHeaderName = "X-Kobo-userkey";

    /// <summary>Retry-After seconds returned while a conversion is unavailable/busy.</summary>
    public const int ConvertRetryAfterSeconds = 30;
}
