using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.Common.Extensions;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.Kobo;
using Kavita.Models.Entities.Enums;
using Kavita.Server.Attributes;
using Kavita.Services.Helpers.Kobo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.Controllers;

[Route("api/v3/content")]
[KoboDeviceAuthenticate]
[Authorize(PolicyGroups.DownloadPolicy)]
[ServerSettingEnabled(ServerSettingKey.EnableKobo, "kobo-sync-disabled")]
public class KoboAnnotationController(
    ILogger<KoboAnnotationController> logger,
    IKoboSyncService koboSyncService,
    IHttpClientFactory httpClientFactory,
    IUnitOfWork unitOfWork): BaseApiController
{

    private HttpClient KoboReadingServicesClient => httpClientFactory.CreateClient("KoboReadingServices");
    private JsonSerializerOptions JsonSerializerOptions => KoboJsonOptions.JsonSerializerOptions;

    /// <summary>
    /// Returns annotations for the given book
    ///
    /// Not intended for use beyond Kobo devices
    /// </summary>
    /// <param name="bookId"></param>
    /// <param name="limit"></param>
    /// <returns></returns>
    [HttpGet("{bookId}/annotations")]
    [ChapterAccess(chapterIdKey: "bookId", failOnMissing: false)]
    public async Task<ActionResult> GetAnnotations(string bookId, [FromQuery] int limit = 100)
    {
        var ct = HttpContext.RequestAborted;

        if (!int.TryParse(bookId, out var kavitaId))
        {
            return await koboSyncService.ProxyRequestIfEnabled(KoboReadingServicesClient);
        }

        var etags = await koboSyncService.GetKoboAnnotationETagsAsync(UserId, [kavitaId], ct);
        var etag = etags[kavitaId];
        Response.Headers.ETag = etag;

        if (Request.Headers.IfNoneMatch.Any(value => string.Equals(value, etag, StringComparison.Ordinal)))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        var annotations = await koboSyncService.GetKoboAnnotationsAsync(UserId, [kavitaId], ct);

        return Ok(new
        {
            Annotations = annotations,
            NextPageOffsetToken = (string?) null,
        });
    }

    /// <summary>
    /// Check if the given books have had any changes since their last ETag
    ///
    /// Not intended for use beyond Kobo devices
    /// </summary>
    /// <returns></returns>
    [HttpPost("checkforchanges")]
    public async Task<ActionResult> GetAnnotationsForBook()
    {
        var ct = HttpContext.RequestAborted;

        var array = await JsonSerializer.DeserializeAsync<List<JsonNode>>(Request.Body, options: JsonSerializerOptions, cancellationToken: ct);
        if (array is null)
        {
            return BadRequest();
        }

        var local = new List<(int ChapterId, string ContentId, string? ETag)>();
        var remote = new List<JsonNode>();

        foreach (var entry in array)
        {
            if (entry is not JsonObject json) continue;

            var contentId = json["ContentId"].GetString() ?? json["contentId"].GetString();
            if (string.IsNullOrEmpty(contentId)) continue;

            if (int.TryParse(contentId, out var chapterId))
            {
                local.Add((chapterId, contentId, json["etag"].GetString() ?? json["ETag"].GetString() ?? json["Etag"].GetString()));
            }
            else
            {
                remote.Add(json);
            }
        }

        var changed = new List<string>();

        if (local.Count > 0)
        {
            var etags = await koboSyncService.GetKoboAnnotationETagsAsync(UserId, local.Select(item => item.ChapterId), ct);
            foreach (var (chapterId, contentId, reportedEtag) in local)
            {
                if (string.IsNullOrEmpty(reportedEtag) || !string.Equals(reportedEtag, etags[chapterId], StringComparison.Ordinal))
                {
                    changed.Add(contentId);
                }
            }
        }

        var syncSettings = await unitOfWork.UserRepository.GetKoboSyncPreferences(UserId, HttpContext.RequestAborted);

        if (remote.Count > 0 && syncSettings.ForwardToKoboStore)
        {
            var (remoteJson, _) = await koboSyncService.ProxyRequest(null, ct, copyResponse: false, body: remote,
                httpClient: KoboReadingServicesClient, writeResponseOnError: false);

            if (remoteJson is JsonArray remoteArray)
            {
                changed.AddRange(remoteArray.Select(node => node.GetString()).WhereNotNull());
            }
        }

        return Ok(changed.Distinct());
    }

    /// <summary>
    /// Update an annotation
    ///
    /// Not intended for use beyond Kobo devices
    /// </summary>
    /// <param name="bookId"></param>
    /// <returns></returns>
    [HttpPatch("{bookId}/annotations")]
    [ChapterAccess(chapterIdKey: "bookId", failOnMissing: false)]
    public async Task<ActionResult> UpdateAnnotations(string bookId)
    {
        var ct = HttpContext.RequestAborted;

        if (!int.TryParse(bookId, out var kavitaId))
        {
            return await koboSyncService.ProxyRequestIfEnabled(KoboReadingServicesClient);
        }

        var koboAnnotations = await JsonSerializer.DeserializeAsync<KoboPatchAnnotationsDto>(Request.Body, options: JsonSerializerOptions , cancellationToken: ct);
        if (koboAnnotations is null)
        {
            return BadRequest();
        }

        await koboSyncService.UpdateKoboAnnotationsAsync(UserId, kavitaId, koboAnnotations, ct);

        return Accepted();
    }

    /// <summary>
    /// Catch all fallback
    ///
    /// Not intended for use beyond Kobo devices
    /// </summary>
    /// <returns></returns>
    [Route("{**catchAll}")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public Task<ActionResult> ProxyFallback()
    {
        return koboSyncService.ProxyRequestIfEnabled(KoboReadingServicesClient);
    }

}
