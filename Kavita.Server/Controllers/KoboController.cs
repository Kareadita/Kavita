using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.Common.Extensions;
using Kavita.Models.Builders;
using Kavita.Models.DTOs.Kobo;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Kobo;
using Kavita.Models.Entities.User;
using Kavita.Server.Attributes;
using Kavita.Server.Extensions;
using Kavita.Server.Helpers;
using Kavita.Server.Middleware;
using Kavita.Services.Extensions;
using Kavita.Services.Helpers.Kobo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using NetVips;

namespace Kavita.Server.Controllers;

[KoboJson]
[Route("[controller]/{apiKey}")]
[ApiExplorerSettings(IgnoreApi = true)]
[ServerSettingEnabled(ServerSettingKey.EnableKobo, "kobo-sync-disabled")]
public class KoboController(
    ILogger<KoboController> logger,
    IUnitOfWork unitOfWork,
    IKoboSyncService koboSyncService,
    IHttpClientFactory httpClientFactory,
    IDirectoryService directoryService,
    IDownloadService downloadService): BaseApiController
{

    private const string KoboImageHost = "https://cdn.kobo.com/book-images/";

    private HttpClient KoboHttpClient => httpClientFactory.CreateClient("Kobo");
    private JsonSerializerOptions JsonSerializerOptions => KoboJsonAttribute.JsonSerializerOptions;

    #region Sync & State

    [HttpGet("v1/initialization")]
    public async Task<ActionResult> Initialization(string apiKey)
    {
        var ct = HttpContext.RequestAborted;

        var (json, hasWrittenResponse) = await ProxyRequest(KoboHttpClient, false, ct, false);
        if (json is null)
        {
            return hasWrittenResponse ? new EmptyResult() : NotFound();
        }

        var serverSettings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
        var instanceUrl = HttpContext.Request.GetInstanceUrl(serverSettings);
        var koboBase = $"{instanceUrl}kobo/{apiKey}";

        var resources = json["Resources"];
        if (resources is not null)
        {
            resources["image_host"] = instanceUrl.TrimEnd('/');
            resources["image_url_template"] = $"{koboBase}/v1/books/{{ImageId}}/thumbnail/{{Width}}/{{Height}}/false/image.jpg";
            resources["image_url_quality_template"] = $"{koboBase}/v1/books/{{ImageId}}/thumbnail/{{Width}}/{{Height}}/{{Quality}}/{{IsGreyscale}}/image.jpg";
        }
        else
        {
            logger.LogWarning("Failed to update Kobo Initialization values. Some request may not be redirected correctly");
        }

        Response.Headers[KoboHeaders.KoboApiToken] = "e30=";

        return Ok(json);
    }

    [HttpGet("v1/library/sync")]
    public async Task<ActionResult> SyncLibrary()
    {
        var ct = HttpContext.RequestAborted;

        var syncToken = KoboSyncToken;
        var syncSettings = await KoboSyncSettings;

        var syncPoint = await unitOfWork.KoboSyncPointRepository.GetSyncPoint(UserId, syncToken.SyncPointId, ct);

        var (kavitaChanges, completedSync) = await koboSyncService.GetChangesSinceAsync(UserId, syncSettings, syncPoint, ct);

        if (!completedSync)
        {
            Response.Headers[KoboHeaders.KoboSync] = "continue";
        }
        else if (syncSettings.ForwardToKoboStore)
        {
            // TODO: Load changes from Kobo store
        }

        syncToken.SyncPointId = syncPoint.Id;
        Response.Headers[KoboHeaders.KoboSyncToken] = KoboSyncTokenHelper.Encode(syncToken);

        return Ok(kavitaChanges);
    }

    [HttpGet("v1/library/{bookId}/metadata")]
    [ChapterAccess(failOnMissing: false, chapterIdKey: "bookId")]
    public async Task<ActionResult> GetBookMetadata(string bookId)
    {
        var ct = HttpContext.RequestAborted;

        if (!int.TryParse(bookId, out var kavitaId))
        {
            return await ProxyRequestIfEnabled();

        }

        var metadata = await koboSyncService.BuildBookMetadata(UserId, [kavitaId], ct);
        if (metadata.Count > 0)
        {
            return Ok(metadata.Values);
        }

        return NotFound();
    }

    [HttpGet("v1/library/{bookId}/state")]
    [ChapterAccess(failOnMissing: false, chapterIdKey: "bookId")]
    public async Task<ActionResult> GetBookState(string bookId)
    {
        var ct = HttpContext.RequestAborted;

        if (!int.TryParse(bookId, out var kavitaId))
        {
            return await ProxyRequestIfEnabled();
        }

        var progress = await koboSyncService.GetProgress(UserId, kavitaId, ct);
        if (progress is null)
        {
            return NotFound();
        }

        return Ok(new List<KoboReadingStateDto> { progress });
    }

    [HttpPut("v1/library/{bookId}/state")]
    [ChapterAccess(failOnMissing: false, chapterIdKey: "bookId")]
    public async Task<ActionResult> UpdateBookState(string bookId)
    {
        var ct = HttpContext.RequestAborted;

        if (!int.TryParse(bookId, out var kavitaId))
        {
            return await ProxyRequestIfEnabled();
        }

        var states = await JsonSerializer.DeserializeAsync<KoboReadingStateUpdateDto>(Request.Body, JsonSerializerOptions, ct);
        if (states is null)
        {
            return BadRequest();
        }

        var update = states.ReadingStates.FirstOrDefault();
        if (update is null)
        {
            return BadRequest();
        }

        if (update.CurrentBookmark.Location is null || update.CurrentBookmark.ContentSourceProgressPercent is null)
        {
            return BadRequest();
        }

        return Ok(await koboSyncService.SaveProgress(UserId, kavitaId, update, ct));
    }

    [ChapterAccess(chapterIdKey: "bookId")]
    [HttpGet("v1/library/{bookId:int}/file/epub")]
    public async Task<ActionResult> GetBookFile(int bookId, [FromQuery] bool convert = true)
    {
        var ct = HttpContext.RequestAborted;

        var chapter = await unitOfWork.ChapterRepository.GetChapterAsync(bookId, ct: ct);
        if (chapter is null)
        {
            return NotFound();
        }

        var downloadFormat = convert ? DownloadFormat.Kepub : DownloadFormat.Epub;
        var path = await downloadService.DownloadChapter(chapter, downloadFormat, ct);

        return PhysicalFile(path, downloadService.GetContentTypeFromFile(path), Path.GetFileName(path), enableRangeProcessing: true);
    }

    [HttpGet("v1/books/{thumbnailId}/thumbnail/{width}/{height}/{isGreyScale}/image.jpg")]
    [HttpGet("v1/books/{thumbnailId}/thumbnail/{width}/{height}/{quality}/{isGreyScale}/image.jpg")]
    [ChapterAccess(failOnMissing: false, chapterIdKey: "thumbnailId")]
    public async Task<ActionResult> GetBookCover(string thumbnailId, string width, string height, string quality, bool isGreyScale = false)
    {
        var ct = HttpContext.RequestAborted;

        if (!int.TryParse(thumbnailId, out var kavitaId))
        {
            if ((await KoboSyncSettings).ForwardToKoboStore)
            {
                var suffix = string.IsNullOrEmpty(quality)
                    ? $"{thumbnailId}/{width}/{height}/{isGreyScale}/image.jpg"
                    : $"{thumbnailId}/{width}/{height}/{quality}/{isGreyScale}/image.jpg";

                return Redirect(KoboImageHost + suffix);
            }

            return NotFound();
        }

        var coverPath = await unitOfWork.ChapterRepository.GetChapterCoverImageAsync(kavitaId, ct);
        if (coverPath is null)
        {
            return NotFound();
        }

        var fullPath = Path.Join(directoryService.CoverImageDirectory, coverPath);
        if (string.Equals(Path.GetExtension(fullPath), ".jpg", StringComparison.OrdinalIgnoreCase))
        {
            return PhysicalFile(fullPath, "image/jpeg");
        }

        var q = int.TryParse(quality, out var parsedQ) ? Math.Clamp(parsedQ, 1, 100) : 85;

        var bytes = await Task.Run(() =>
        {
            using var img = Image.Thumbnail(fullPath, int.TryParse(width, out var w) ? w : 1000000,
                height: int.TryParse(height, out var h) ? h : 1000000,
                size: Enums.Size.Down);

            using var final = isGreyScale ? img.Colourspace(Enums.Interpretation.Bw) : img;

            return final.JpegsaveBuffer(q: q);
        }, ct);

        return File(bytes, "image/jpeg");
    }

    [HttpDelete("v1/library/{bookId}")]
    [ChapterAccess(failOnMissing: false, chapterIdKey: "bookId")]
    public async Task<ActionResult> DeleteBook(string bookId)
    {
        if (int.TryParse(bookId, out var _))
        {
            // We cannot really process this. As the collection is series
            return Ok();
        }

        if ((await KoboSyncSettings).ForwardToKoboStore)
        {
            await ProxyRequest(KoboHttpClient, false, HttpContext.RequestAborted);
            return new EmptyResult();
        }

        return NotFound();
    }

    [HttpPost("v1/library/{bookId}")]
    [ChapterAccess(failOnMissing: false, chapterIdKey: "bookId")]
    public async Task<ActionResult> AddBook(string bookId)
    {
        var ct = HttpContext.RequestAborted;

        if (!int.TryParse(bookId, out var kavitaId))
        {
            return await ProxyRequestIfEnabled();
        }

        var syncPreferences = await unitOfWork.UserRepository.GetKoboSyncPreferences(UserId, ct);
        if (!syncPreferences.IsSyncingCollection)
        {
            // We cannot handle this for non collection syncs
            return Ok();
        }

        var seriesId = await unitOfWork.ChapterRepository.GetSeriesIdForChapter(kavitaId, ct);
        if (seriesId is null) return NotFound();

        var userCollection = await unitOfWork.CollectionTagRepository.GetCollectionAsync(syncPreferences.SyncCollection, CollectionIncludes.Series, ct: ct);
        if (userCollection is null) return NotFound();

        if (userCollection.Items.Any(i => i.Id == seriesId))
        {
            return BadRequest();
        }

        var stub = new Series
        {
            Id = seriesId.Value,
            Name = null,
            NormalizedName = null,
            NormalizedLocalizedName = null,
            SortName = null,
            LocalizedName = null,
            OriginalName = null
        };

        unitOfWork.DataContext.Series.Attach(stub);
        userCollection.Items.Add(stub);
        await unitOfWork.CommitAsync(ct);

        return Ok(new KoboAddBookResultResponse
        {
            Result = new KoboDetailResults<KoboAddBookResponse>
            {
                DetailedResults = [new KoboAddBookResponse
                    {
                        RevisionId = bookId,
                        CrossRevisionId = bookId,
                        EntitlementId = bookId,
                    }
                ]
            }
        });
    }

    #endregion

    #region Products

    [HttpGet("v1/products/{bookId}/reviews")]
    [ChapterAccess(failOnMissing: false, chapterIdKey: "bookId")]
    public async Task<ActionResult> GetReviews(string bookId)
    {
        if (!int.TryParse(bookId, out _))
        {
            return await ProxyRequestIfEnabled();
        }

        return Ok(new KoboReviewsResponseDto
        {
            CurrentPageIndex = 0,
            Items = [],
            TotalPageCount = 0,
            Cursor = string.Empty,
            ReviewSummary = []
        });
    }

    [HttpGet("v1/products/{bookId}/recommendations")]
    [ChapterAccess(failOnMissing: false, chapterIdKey: "bookId")]
    public async Task<ActionResult> GetRecommendations(string bookId)
    {
        var ct = HttpContext.RequestAborted;

        if (!int.TryParse(bookId, out var kavitaId))
        {
            return await ProxyRequestIfEnabled();
        }

        var rec = await unitOfWork.ChapterRepository.GetKoboRecommendationAsync(UserId, kavitaId, ct);

        return Ok(rec);
    }

    [HttpGet("v1/products/books/{bookId}/")]
    [ChapterAccess(failOnMissing: false, chapterIdKey: "bookId")]
    public async Task<ActionResult> GetBook(string bookId)
    {
        var ct = HttpContext.RequestAborted;

        if (!int.TryParse(bookId, out var kavitaId))
        {
            return await ProxyRequestIfEnabled();
        }

        var detail = await unitOfWork.ChapterRepository.GetKoboBookDetailsAsync(kavitaId, ct);
        if (detail is null)
        {
            return NotFound();
        }

        return Ok(detail);
    }

    [HttpGet("v1/products/{bookId}/nextread")]
    [ChapterAccess(failOnMissing: false, chapterIdKey: "bookId")]
    public async Task<ActionResult> GetNextRead(string bookId)
    {
        if (!int.TryParse(bookId, out _))
        {
            return await ProxyRequestIfEnabled();
        }

        // TODO: Implement

        return Ok();
    }

    [HttpGet("v1/products/{bookIds}/prices")]
    [ChapterAccess(failOnMissing: false, chapterIdKey: "bookId")]
    public async Task<ActionResult> GetPrices(string bookIds)
    {
        var isKoboRequest = bookIds.Split(',').Any(id => !int.TryParse(id, out _));
        if (isKoboRequest)
        {
            return await ProxyRequestIfEnabled();
        }

        var prices = bookIds.Split(',').Select(id => new KoboBookPriceInformationDto
        {
            Id = id,
            CrossRevisionId = id,
        });

        return Ok(new
        {
            Items = prices,
        });
    }

    [HttpPut("v1/products/{bookId}/reviews")]
    [ChapterAccess(failOnMissing: false, chapterIdKey: "bookId")]
    public async Task<ActionResult> UpdateReview(string bookId)
    {
        var ct = HttpContext.RequestAborted;
        if (!int.TryParse(bookId, out var kavitaId))
        {
            return await ProxyRequestIfEnabled();
        }

        var seriesId = await unitOfWork.ChapterRepository.GetSeriesIdForChapter(kavitaId, ct);
        if (seriesId is null)
        {
            return NotFound();
        }

        // This has different JSON encoding (:sob:)
        var reviewDto = await JsonSerializer.DeserializeAsync<KoboSubmitReviewDto>(Request.Body, cancellationToken: ct);
        if (reviewDto is null)
        {
            return NotFound();
        }

        var rating = new ChapterRatingBuilder(await unitOfWork.UserRepository.GetUserChapterRatingAsync(UserId, kavitaId, ct))
            .WithBody(reviewDto.Body)
            .WithRating(reviewDto.Rating)
            .WithChapterId(kavitaId)
            .WithSeriesId(seriesId.Value)
            .WithUserId(UserId)
            .Build();

        if (rating.Id == 0)
        {
            unitOfWork.DataContext.AppUserChapterRating.Add(rating);
        }

        await unitOfWork.CommitAsync(ct);

        return Ok();
    }

    #endregion

    #region User

    [HttpGet("v1/user/reviews")]
    public async Task<ActionResult> GetReviews([FromQuery(Name = "ProductIds")] List<string> productIds)
    {
        var ct = HttpContext.RequestAborted;

        var isKoboRequest = productIds.Any(id => !int.TryParse(id, out _));
        if (isKoboRequest)
        {
            if ((await KoboSyncSettings).ForwardToKoboStore)
            {
                await ProxyRequest(KoboHttpClient, false, ct);
            }
            return new EmptyResult();
        }

        var kavitaIds = productIds.Select(int.Parse).ToList();

        if (!await unitOfWork.UserRepository.HasAccessToAllChapters(UserId, kavitaIds, ct))
        {
            return NotFound();
        }

        var reviews = await unitOfWork.UserRepository.GetKoboReviewsForChapterAsync(UserId, kavitaIds, ct);

        return Ok(new KoboReviewsResponseDto
        {
            CurrentPageIndex = 1,
            Cursor = string.Empty,
            Items = reviews,
            ReviewSummary = reviews.GroupBy(r => r.CrossRevisionId).ToDictionary(g => g.Key,
                g => new KoboReviewSummaryDto
                {
                    AvgRating = g.Average(r => r.Rating),
                    NumberOfReviews = g.Count(),
                    OpinionCount = 0,
                    RatingHistogram = []
                }),
            TotalPageCount = 1
        });
    }

    #endregion

    [Route("{**catchAll}")]
    public Task<ActionResult> ProxyFallback()
    {
        return ProxyRequestIfEnabled();
    }

    private KoboSyncToken KoboSyncToken => KoboSyncTokenHelper.Decode(Request.Headers[KoboHeaders.KoboSyncToken]);

    private Task<AppUserKoboSyncPreferences> KoboSyncSettings => unitOfWork.UserRepository.GetKoboSyncPreferences(UserId, HttpContext.RequestAborted);

    #region Proxy

    private static readonly IReadOnlySet<string> IncludeHeaders = new HashSet<string>
    {
        HeaderNames.Authorization,
        HeaderNames.UserAgent,
        HeaderNames.Accept,
        HeaderNames.AcceptLanguage,
        HeaderNames.ContentType,
    };

    private static bool IsKoboHeader(string header) => header.StartsWith("x-kobo-", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Defaults to <see cref="KoboHttpClient"/>
    /// </summary>
    /// <param name="httpClient"></param>
    /// <param name="body"></param>
    /// <returns></returns>
    private async Task<ActionResult> ProxyRequestIfEnabled(HttpClient? httpClient = null, object? body = null)
    {
        if ((await KoboSyncSettings).ForwardToKoboStore)
        {
            await ProxyRequest(httpClient ?? KoboHttpClient, false, HttpContext.RequestAborted, body: body);
            return new EmptyResult();
        }

        return NotFound();
    }

    private async Task<(JsonNode?, bool)> ProxyRequest(HttpClient httpClient, bool includeSyncToken, CancellationToken ct, bool copyResponse = true, object? body = null)
    {
        var targetUrl = GetKoboUrl();

        logger.LogTrace("Going to proxy request to {targetUrl} for {UserId}", targetUrl.Sanitize(), UserId);

        using var upstreamRequest = new HttpRequestMessage(new HttpMethod(Request.Method), targetUrl);

        upstreamRequest.CopyRequest(Request, header => IncludeHeaders.Contains(header) || IsKoboHeader(header), body);

        var syncToken = KoboSyncToken;
        if (includeSyncToken && !string.IsNullOrEmpty(syncToken.KoboStoreSyncToken))
        {
            upstreamRequest.Headers.TryAddWithoutValidation(KoboHeaders.KoboSyncToken, syncToken.KoboStoreSyncToken);
        }
        else
        {
            upstreamRequest.Headers.Remove(KoboHeaders.KoboSyncToken);
        }

        using var upstreamResponse = await httpClient.SendAsync(upstreamRequest, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!upstreamResponse.IsSuccessStatusCode || copyResponse)
        {
            if (!upstreamResponse.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to proxy request to {Url}: {StatusCode} {ReasonPhrase}", targetUrl.Sanitize(), upstreamResponse.StatusCode, upstreamResponse.ReasonPhrase);
            }

            await Response.CopyResponse(upstreamResponse, IsKoboHeader, ct);
            return (null, true);
        }

        return (await upstreamResponse.Content.ReadFromJsonAsync<JsonNode>(ct), false);
    }

    private string GetKoboUrl()
    {
        var path = Request.Path.ToString();
        var queryString = Request.QueryString;

        var apiKey = AuthKeyAuthenticationHandler.ExtractAuthKey(Request);
        var prefix = $"/kobo/{apiKey}";

        return $"{path[prefix.Length..]}{queryString}";
    }

    #endregion

}
