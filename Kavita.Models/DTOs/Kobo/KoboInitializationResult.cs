using System.Text.Json.Nodes;

namespace Kavita.Models.DTOs.Kobo;

/// <summary>
/// Initialization payload plus the Calibre-Web-compatible <c>x-kobo-apitoken</c> header value.
/// </summary>
public class KoboInitializationResult
{
    /// <summary>
    /// Base64 of <c>{}</c> — Calibre-Web always sets this on initialization.
    /// </summary>
    public const string ApiTokenHeaderValue = "e30=";

    public required JsonObject Resources { get; init; }
}
