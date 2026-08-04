using System.Text.Json.Nodes;

namespace Kavita.Models.DTOs.Kobo;

/// <summary>
/// Initialization payload. The Calibre-Web-compatible <c>x-kobo-apitoken</c> header value lives on
/// <c>KoboHttpConstants</c>.
/// </summary>
public class KoboInitializationResult
{
    public required JsonObject Resources { get; init; }
}
