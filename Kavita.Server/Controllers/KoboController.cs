using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Kavita.API.Services;
using Kavita.Common;
using Kavita.Models.DTOs.Kobo;
using Kavita.Services.Kobo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kavita.Server.Controllers;

/// <summary>
/// Calibre-Web–shaped Kobo device API mounted at <c>/api/kobo/{apiKey}/…</c>.
/// Auth resolves the URL token to the user's named <c>kobo</c> AuthKey.
/// </summary>
[Authorize]
[Route("api/kobo/{apiKey}")]
[ApiController]
public class KoboController(IKoboService koboService) : ControllerBase
{
    private static readonly JsonSerializerOptions KoboJsonOptions = new()
    {
        PropertyNamingPolicy = null,
    };

    /// <summary>
    /// Blueprint root keep-alive.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Root(string apiKey)
    {
        await koboService.ResolveUserIdAsync(apiKey, HttpContext.RequestAborted);
        return KoboJson(koboService.GetEmptyStub());
    }

    [HttpGet("v1/initialization")]
    public async Task<IActionResult> Initialization(string apiKey)
    {
        var result = await koboService.GetInitializationAsync(apiKey, HttpContext.RequestAborted);
        Response.Headers["x-kobo-apitoken"] = KoboInitializationResult.ApiTokenHeaderValue;

        return KoboJson(new JsonObject
        {
            ["Resources"] = result.Resources,
        });
    }

    [HttpPost("v1/auth/device")]
    [HttpPost("v1/auth/refresh")]
    public async Task<IActionResult> AuthDevice(string apiKey, [FromBody] KoboDeviceAuthRequestDto? body)
    {
        var response = await koboService.CreateDeviceAuthResponseAsync(apiKey, body?.UserKey,
            HttpContext.RequestAborted);
        return KoboJson(response);
    }

    [HttpGet("v1/library/sync")]
    public async Task<IActionResult> LibrarySync(string apiKey)
    {
        Request.Headers.TryGetValue(KoboSyncToken.HeaderName, out var syncToken);
        var result = await koboService.SyncLibraryAsync(apiKey, syncToken.ToString(),
            HttpContext.RequestAborted);

        Response.Headers[KoboSyncToken.HeaderName] = result.SyncToken;
        if (result.Continue)
        {
            Response.Headers[KoboSyncToken.ContinueHeaderName] = KoboSyncToken.ContinueHeaderValue;
        }

        return KoboJson(result.Items);
    }

    [HttpGet("v1/library/{entitlementId}/metadata")]
    public async Task<IActionResult> Metadata(string apiKey, string entitlementId)
    {
        try
        {
            var metadata = await koboService.GetMetadataAsync(apiKey, entitlementId,
                HttpContext.RequestAborted);
            return KoboJson(metadata);
        }
        catch (KavitaException ex) when (ex.Message == "kobo-entitlement-not-found")
        {
            return NotFound();
        }
    }

    [HttpGet("download/{entitlementId}/{format}")]
    public async Task<IActionResult> Download(string apiKey, string entitlementId, string format)
    {
        try
        {
            var download = await koboService.GetDownloadAsync(apiKey, entitlementId, format,
                HttpContext.RequestAborted);
            return PhysicalFile(download.FilePath, download.ContentType, download.FileDownloadName,
                enableRangeProcessing: true);
        }
        catch (KavitaException ex) when (ex.Message is "kobo-entitlement-not-found" or "kobo-epub-missing"
                                            or "kobo-format-unsupported")
        {
            return NotFound();
        }
    }

    [HttpGet("{entitlementId}/{width}/{height}/{isGreyscale}/image.jpg")]
    [HttpGet("{entitlementId}/{width}/{height}/{quality}/{isGreyscale}/image.jpg")]
    public async Task<IActionResult> Cover(string apiKey, string entitlementId)
    {
        var cover = await koboService.GetCoverAsync(apiKey, entitlementId, HttpContext.RequestAborted);
        if (cover == null || !System.IO.File.Exists(cover.FilePath)) return NotFound();

        var contentType = Path.GetExtension(cover.FilePath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg",
        };
        return PhysicalFile(cover.FilePath, contentType);
    }

    [HttpGet("v1/user/loyalty/benefits")]
    public async Task<IActionResult> LoyaltyBenefits(string apiKey)
    {
        await koboService.ResolveUserIdAsync(apiKey, HttpContext.RequestAborted);
        return KoboJson(koboService.GetLoyaltyBenefitsStub());
    }

    [HttpGet("v1/analytics/gettests")]
    [HttpPost("v1/analytics/gettests")]
    public async Task<IActionResult> AnalyticsGetTests(string apiKey)
    {
        await koboService.ResolveUserIdAsync(apiKey, HttpContext.RequestAborted);
        Request.Headers.TryGetValue("X-Kobo-userkey", out var userKey);
        return KoboJson(koboService.GetAnalyticsTestsStub(userKey.ToString()));
    }

    [HttpGet("v1/library/{entitlementId}/state")]
    public async Task<IActionResult> GetReadingState(string apiKey, string entitlementId)
    {
        await koboService.ResolveUserIdAsync(apiKey, HttpContext.RequestAborted);
        return KoboJson(koboService.GetReadingStateStub(entitlementId));
    }

    [HttpPut("v1/library/{entitlementId}/state")]
    public async Task<IActionResult> PutReadingState(string apiKey, string entitlementId)
    {
        await koboService.ResolveUserIdAsync(apiKey, HttpContext.RequestAborted);
        return KoboJson(koboService.PutReadingStateStub(entitlementId));
    }

    // Keep-alive stubs that return {} (Calibre-Web non-proxy behaviour)
    [HttpGet("v1/user/profile")]
    [HttpPost("v1/user/profile")]
    [HttpGet("v1/user/wishlist")]
    [HttpPost("v1/user/wishlist")]
    [HttpGet("v1/user/recommendations")]
    [HttpPost("v1/user/recommendations")]
    [HttpGet("v1/user/loyalty/{dummy}")]
    [HttpPost("v1/user/loyalty/{dummy}")]
    [HttpGet("v1/analytics/{dummy}")]
    [HttpPost("v1/analytics/{dummy}")]
    [HttpGet("v1/assets")]
    [HttpGet("v1/products/{*dummy}")]
    [HttpPost("v1/products/{*dummy}")]
    [HttpGet("v1/products")]
    [HttpPost("v1/products")]
    [HttpGet("v1/affiliate")]
    [HttpPost("v1/affiliate")]
    [HttpGet("v1/deals")]
    [HttpPost("v1/deals")]
    [HttpGet("v1/library/{dummy}")]
    [HttpPost("v1/library/{dummy}")]
    [HttpPost("v1/library/{dummy}/preview")]
    public async Task<IActionResult> EmptyKeepAlive(string apiKey)
    {
        await koboService.ResolveUserIdAsync(apiKey, HttpContext.RequestAborted);
        return KoboJson(koboService.GetEmptyStub());
    }

    private ContentResult KoboJson(object payload)
    {
        return new ContentResult
        {
            Content = JsonSerializer.Serialize(payload, KoboJsonOptions),
            ContentType = "application/json",
            StatusCode = 200,
        };
    }
}
