using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Kavita.API.Services;
using Kavita.Common;
using Kavita.Models.DTOs.Kobo;
using Kavita.Services;
using Kavita.Services.Kobo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
        Response.Headers[KoboHttpConstants.ApiTokenHeaderName] = KoboHttpConstants.ApiTokenHeaderValue;

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
        try
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
        catch (KavitaException ex) when (MapKoboException(ex) is { } result)
        {
            return result;
        }
    }

    [HttpDelete("v1/library/{entitlementId}")]
    public async Task<IActionResult> DeleteEntitlement(string apiKey, string entitlementId)
    {
        await koboService.DeleteEntitlementAsync(apiKey, entitlementId, HttpContext.RequestAborted);
        return NoContent();
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
        catch (KavitaException ex) when (MapKoboException(ex) is { } result)
        {
            return result;
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
        catch (KavitaException ex) when (MapKoboException(ex) is { } result)
        {
            return result;
        }
    }

    [HttpGet("{entitlementId}/{width}/{height}/{isGreyscale}/image.jpg")]
    [HttpGet("{entitlementId}/{width}/{height}/{quality}/{isGreyscale}/image.jpg")]
    public async Task<IActionResult> Cover(string apiKey, string entitlementId)
    {
        var cover = await koboService.GetCoverAsync(apiKey, entitlementId, HttpContext.RequestAborted);
        if (cover == null || !System.IO.File.Exists(cover.FilePath)) return NotFound();

        return PhysicalFile(cover.FilePath, cover.ContentType);
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
        Request.Headers.TryGetValue(KoboHttpConstants.UserKeyHeaderName, out var userKey);
        return KoboJson(koboService.GetAnalyticsTestsStub(userKey.ToString()));
    }

    [HttpGet("v1/library/{entitlementId}/state")]
    public async Task<IActionResult> GetReadingState(string apiKey, string entitlementId)
    {
        return KoboJson(await koboService.GetReadingStateAsync(apiKey, entitlementId,
            HttpContext.RequestAborted));
    }

    [HttpPut("v1/library/{entitlementId}/state")]
    public async Task<IActionResult> PutReadingState(string apiKey, string entitlementId,
        [FromBody] JsonObject? body)
    {
        try
        {
            return KoboJson(await koboService.PutReadingStateAsync(apiKey, entitlementId, body,
                HttpContext.RequestAborted));
        }
        catch (KavitaException ex) when (MapKoboException(ex) is { } result)
        {
            return result;
        }
    }

    /// <summary>
    /// Device create Tag → owned Reading List. Returns the deterministic Tag UUID string.
    /// </summary>
    [HttpPost("v1/library/tags")]
    public async Task<IActionResult> CreateTag(string apiKey, [FromBody] JsonObject? body)
    {
        try
        {
            var tagId = await koboService.CreateTagAsync(apiKey, body, HttpContext.RequestAborted);
            return new ContentResult
            {
                Content = JsonSerializer.Serialize(tagId, KoboJsonOptions),
                ContentType = "application/json",
                StatusCode = StatusCodes.Status201Created,
            };
        }
        catch (KavitaException ex) when (MapKoboException(ex) is { } result)
        {
            return result;
        }
    }

    /// <summary>
    /// Explicit 405 so DELETE /tags is not treated as an entitlement delete.
    /// </summary>
    [HttpDelete("v1/library/tags")]
    public IActionResult DeleteTagsCollection() => StatusCode(StatusCodes.Status405MethodNotAllowed);

    [HttpPut("v1/library/tags/{tagId}")]
    public async Task<IActionResult> RenameTag(string apiKey, string tagId, [FromBody] JsonObject? body)
    {
        try
        {
            await koboService.RenameTagAsync(apiKey, tagId, body, HttpContext.RequestAborted);
            return KoboAckSpace();
        }
        catch (KavitaException ex) when (MapKoboException(ex) is { } result)
        {
            return result;
        }
    }

    [HttpDelete("v1/library/tags/{tagId}")]
    public async Task<IActionResult> DeleteTag(string apiKey, string tagId)
    {
        try
        {
            await koboService.DeleteTagAsync(apiKey, tagId, HttpContext.RequestAborted);
            return KoboAckSpace();
        }
        catch (KavitaException ex) when (MapKoboException(ex) is { } result)
        {
            return result;
        }
    }

    [HttpPost("v1/library/tags/{tagId}/items")]
    [HttpPost("v1/library/tags/{tagId}/Items")]
    public async Task<IActionResult> AddTagItems(string apiKey, string tagId, [FromBody] JsonObject? body)
    {
        try
        {
            await koboService.AddTagItemsAsync(apiKey, tagId, body, HttpContext.RequestAborted);
            return StatusCode(StatusCodes.Status201Created);
        }
        catch (KavitaException ex) when (MapKoboException(ex) is { } result)
        {
            return result;
        }
    }

    [HttpPost("v1/library/tags/{tagId}/items/delete")]
    [HttpPost("v1/library/tags/{tagId}/Items/delete")]
    public async Task<IActionResult> RemoveTagItems(string apiKey, string tagId, [FromBody] JsonObject? body)
    {
        try
        {
            await koboService.RemoveTagItemsAsync(apiKey, tagId, body, HttpContext.RequestAborted);
            return Ok();
        }
        catch (KavitaException ex) when (MapKoboException(ex) is { } result)
        {
            return result;
        }
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

    /// <summary>
    /// Maps a <see cref="KavitaException"/> raised by <see cref="IKoboService"/> to the Kobo-shaped
    /// HTTP response, or <c>null</c> when the message is not a recognised Kobo error (rethrow).
    /// </summary>
    private IActionResult? MapKoboException(KavitaException ex)
    {
        switch (ex.Message)
        {
            case KoboService.EntitlementNotFoundMessage:
            case KoboService.EpubMissingMessage:
            case KoboService.FormatUnsupportedMessage:
            case KoboService.TagNotFoundMessage:
                return NotFound();
            case KoboService.TagForbiddenMessage:
                return StatusCode(StatusCodes.Status403Forbidden);
            case KoboService.SyncBusyMessage:
                Response.Headers.RetryAfter = KoboService.SyncLockWaitSeconds.ToString();
                return StatusCode(StatusCodes.Status503ServiceUnavailable);
            case KoboConversionService.ConvertUnavailableMessage:
                Response.Headers.RetryAfter = KoboHttpConstants.ConvertRetryAfterSeconds.ToString();
                return StatusCode(StatusCodes.Status503ServiceUnavailable);
            case KoboConversionService.ConvertFailedMessage:
                return StatusCode(StatusCodes.Status500InternalServerError);
            case KoboService.ReadingStateMalformedMessage:
            case KoboService.TagNameRequiredMessage:
            case "reading-list-name-exists":
                return BadRequest(ex.Message);
            default:
                return null;
        }
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

    /// <summary>Calibre-Web ACK body for rename/delete Tag: a single space.</summary>
    private static ContentResult KoboAckSpace() => new()
    {
        Content = " ",
        ContentType = "application/json",
        StatusCode = StatusCodes.Status200OK,
    };
}
