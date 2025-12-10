using API.Constants;
using API.Entities.Enums;
using API.Entities.Progress;

namespace API.DTOs.Progress;
#nullable enable

public sealed record ClientInfoDto
{
    /// <summary>
    /// Raw User-Agent string from request header
    /// </summary>
    public string UserAgent { get; set; } = string.Empty;

    /// <summary>
    /// Client IP address (respecting X-Forwarded-For if present)
    /// </summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// How the user authenticated (JWT token vs API key)
    /// </summary>
    public AuthenticationType AuthType { get; set; }

    /// <summary>
    /// Parsed client type from User-Agent or custom Kavita header
    /// Examples: Web App, OPDS Reader, KOReader, Tachiyomi, etc.
    /// </summary>
    public ClientDeviceType ClientType { get; set; } = ClientDeviceType.Unknown;

    /// <summary>
    /// Application version (from web app or mobile app)
    /// </summary>
    public string? AppVersion { get; set; }

    /// <summary>
    /// Browser name (Chrome, Firefox, Safari, Edge) - Web clients only
    /// </summary>
    public string? Browser { get; set; }

    /// <summary>
    /// Browser version - Web clients only
    /// </summary>
    public string? BrowserVersion { get; set; }

    /// <summary>
    /// Platform/OS (Windows, macOS, Linux, iOS, Android)
    /// </summary>
    public ClientDevicePlatform Platform { get; set; } = ClientDevicePlatform.Unknown;

    /// <summary>
    /// Device type (Desktop, Mobile, Tablet)
    /// </summary>
    public string? DeviceType { get; set; }

    /// <summary>
    /// Screen width in pixels - Web clients only
    /// </summary>
    public int? ScreenWidth { get; set; }

    /// <summary>
    /// Screen height in pixels - Web clients only
    /// </summary>
    public int? ScreenHeight { get; set; }

    /// <summary>
    /// Screen orientation (portrait, landscape) - Web clients only
    /// </summary>
    public string? Orientation { get; set; }
}
