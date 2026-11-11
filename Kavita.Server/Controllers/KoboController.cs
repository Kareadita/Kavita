using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.Common.Extensions;
using Kavita.Models.Builders;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.Kobo;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.User;
using Kavita.Server.Attributes;
using Kavita.Server.Extensions;
using Kavita.Services.Extensions;
using Kavita.Services.Helpers.Kobo;
using Kavita.Services.Scanner;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetVips;

namespace Kavita.Server.Controllers;

/// <summary>
/// Kobo sync implementation. Not intended for use beyond Kobo devices
/// </summary>
[KoboJson]
[Route("[controller]/{apiKey}")]
[Authorize(PolicyGroups.DownloadPolicy)]
[ServerSettingEnabled(ServerSettingKey.EnableKobo, "kobo-sync-disabled")]
public class KoboController(
    ILogger<KoboController> logger,
    IUnitOfWork unitOfWork,
    IKoboSyncService koboSyncService,
    IDirectoryService directoryService,
    IDownloadService downloadService): BaseApiController
{

    private const string KoboImageHost = "https://cdn.kobo.com/book-images/";

    private JsonSerializerOptions JsonSerializerOptions => KoboJsonOptions.JsonSerializerOptions;

    #region Sync & State

    /// <summary>
    /// First request when starting the sync flow. Returns all config for the Kobo to make request. We inject some
    /// redirect to the Kavita instance
    ///
    /// Not intended for use beyond Kobo devices
    /// </summary>
    /// <returns></returns>
    [HttpGet("v1/initialization")]
    public async Task<ActionResult> Initialization()
    {
        var ct = HttpContext.RequestAborted;

        var (json, hasWrittenResponse) = await koboSyncService.ProxyRequest(null, ct, false);
        if (json is null)
        {
            return hasWrittenResponse ? new EmptyResult() : NotFound();
        }

        var serverSettings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
        var instanceUrl = HttpContext.Request.GetInstanceUrl(serverSettings);
        var koboBase = $"{instanceUrl}kobo/{AuthKey}";

        var resources = json["Resources"];
        if (resources is not null)
        {
            resources["image_host"] = instanceUrl.TrimEnd('/');
            resources["image_url_template"] = $"{koboBase}/v1/books/{{ImageId}}/thumbnail/{{Width}}/{{Height}}/false/image.jpg";
            resources["image_url_quality_template"] = $"{koboBase}/v1/books/{{ImageId}}/thumbnail/{{Width}}/{{Height}}/{{Quality}}/{{IsGreyscale}}/image.jpg";
            resources["reading_services_host"] = koboBase;
        }
        else
        {
            logger.LogWarning("Failed to update Kobo Initialization values. Some request may not be redirected correctly");
        }

        Response.Headers[KoboHeaders.KoboApiToken] = "e30=";

        return Ok(json);
    }

    /// <summary>
    /// Returns changes since last sync
    ///
    /// Not intended for use beyond Kobo devices
    /// </summary>
    /// <returns></returns>
    [HttpGet("v1/library/sync")]
    public async Task<ActionResult> SyncLibrary()
    {
        var ct = HttpContext.RequestAborted;

        var syncToken = KoboSyncToken;
        var deviceId = KoboDeviceId;
        var syncSettings = await KoboSyncSettings;

        var syncPoint = await unitOfWork.KoboSyncPointRepository.GetSyncPoint(UserId, syncToken.SyncPointId, ct);

        // Delete all other KoboSyncPoints for this device id as they'll not be used anymore
        await unitOfWork.KoboSyncPointRepository.DeleteOthersForDeviceId(UserId, syncPoint.Id, deviceId, ct);
        syncPoint.KoboDeviceId = deviceId;

        var (kavitaChanges, completedSync) = await koboSyncService.GetChangesSinceAsync(UserId, syncSettings, syncPoint, ct);
        syncToken.SyncPointId = syncPoint.Id; // After since GetSyncPoint does not commit

        if (kavitaChanges.Count == 0 && completedSync && syncSettings.ForwardToKoboStore)
        {
            await koboSyncService.ProxyRequest(syncToken, ct);
            return new EmptyResult();
        }

        if (!completedSync || syncSettings.ForwardToKoboStore)
        {
            Response.Headers[KoboHeaders.KoboSync] = "continue";
        }

        Response.Headers[KoboHeaders.KoboSyncToken] = KoboSyncTokenHelper.Encode(syncToken);

        return Ok(kavitaChanges);
    }

    /// <summary>
    /// Metadata for the given book. Called when opening information, and sometimes during sync but not always
    ///
    /// Not intended for use beyond Kobo devices
    /// </summary>
    /// <param name="bookId"></param>
    /// <returns></returns>
    [HttpGet("v1/library/{bookId}/metadata")]
    [ChapterAccess(failOnMissing: false, chapterIdKey: "bookId")]
    public async Task<ActionResult> GetBookMetadata(string bookId)
    {
        var ct = HttpContext.RequestAborted;

        if (!int.TryParse(bookId, out var kavitaId))
        {
            return await koboSyncService.ProxyRequestIfEnabled();

        }

        var metadata = await koboSyncService.BuildBookMetadata(UserId, [kavitaId], ct);
        if (metadata.Count > 0)
        {
            return Ok(metadata.Values);
        }

        return NotFound();
    }

    /// <summary>
    /// Gets progress. Called on book open to sync
    ///
    /// Not intended for use beyond Kobo devices
    /// </summary>
    /// <param name="bookId"></param>
    /// <returns></returns>
    [HttpGet("v1/library/{bookId}/state")]
    [ChapterAccess(failOnMissing: false, chapterIdKey: "bookId")]
    public async Task<ActionResult> GetBookState(string bookId)
    {
        var ct = HttpContext.RequestAborted;

        if (!int.TryParse(bookId, out var kavitaId))
        {
            return await koboSyncService.ProxyRequestIfEnabled();
        }

        var progress = await koboSyncService.GetProgress(UserId, kavitaId, ct);
        if (progress is null)
        {
            return NotFound();
        }

        return Ok(new List<KoboReadingStateDto> { progress });
    }

    /// <summary>
    /// Updates progress. Call on book close and when switching chapters via ToC while connected to the internet.
    /// Otherwise, during sync
    ///
    /// Not intended for use beyond Kobo devices
    /// </summary>
    /// <param name="bookId"></param>
    /// <returns></returns>
    [HttpPut("v1/library/{bookId}/state")]
    [ChapterAccess(failOnMissing: false, chapterIdKey: "bookId")]
    public async Task<ActionResult> UpdateBookState(string bookId)
    {
        var ct = HttpContext.RequestAborted;

        if (!int.TryParse(bookId, out var kavitaId))
        {
            return await koboSyncService.ProxyRequestIfEnabled();
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

    /// <summary>
    /// Gets the (k)epub file
    ///
    /// Not intended for use beyond Kobo devices
    /// </summary>
    /// <param name="bookId"></param>
    /// <param name="convert"></param>
    /// <returns></returns>
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
        var path = await downloadService.DownloadChapter(UserId, chapter, downloadFormat, ct);

        return PhysicalFile(path, downloadService.GetContentTypeFromFile(path), Path.GetFileName(path), enableRangeProcessing: true);
    }

    /// <summary>
    /// Gets the cover. Called during sync, and on extra info when missing
    ///
    /// Not intended for use beyond Kobo devices
    /// </summary>
    /// <param name="thumbnailId"></param>
    /// <param name="width"></param>
    /// <param name="height"></param>
    /// <param name="quality"></param>
    /// <param name="isGreyScale"></param>
    /// <returns></returns>
    [ChapterAccess(failOnMissing: false, chapterIdKey: "thumbnailId")]
    [HttpGet("v1/books/{thumbnailId}/thumbnail/{width}/{height}/{isGreyScale}/image.jpg")]
    [HttpGet("v1/books/{thumbnailId}/thumbnail/{width}/{height}/{quality}/{isGreyScale}/image.jpg")]
    public async Task<ActionResult> GetBookCover(string thumbnailId, string width, string height, string? quality, bool isGreyScale = false)
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

        var coverInfo = await unitOfWork.DataContext.Chapter
            .Where(c => c.Id == kavitaId)
            .Select(c => new
            {
                ChapterCoverImage = c.CoverImage,
                VolumeCoverImage = c.Volume.CoverImage,
                c.MinNumber
            })
            .FirstOrDefaultAsync(ct);

        if (coverInfo is null)
        {
            return NotFound();
        }

        var coverPath = coverInfo.MinNumber.Is(Parser.DefaultChapterNumber)
            ? coverInfo.VolumeCoverImage
            : coverInfo.ChapterCoverImage;

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

    /// <summary>
    /// Called when a book is deleted from the Kobo. No-Op for Kavita books
    ///
    /// Not intended for use beyond Kobo devices
    /// </summary>
    /// <param name="bookId"></param>
    /// <returns></returns>
    [HttpDelete("v1/library/{bookId}")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    [ChapterAccess(failOnMissing: false, chapterIdKey: "bookId")]
    public async Task<ActionResult> DeleteBook(string bookId)
    {
        if (int.TryParse(bookId, out var _))
        {
            // We cannot really process this. As the collection is series
            return Ok();
        }

        return await koboSyncService.ProxyRequestIfEnabled();
    }

    /// <summary>
    /// Called when clicking on a recommendation. Adds the series to the Kobo sync collection if enabled
    /// Otherwise No-Op
    ///
    /// Not intended for use beyond Kobo devices
    /// </summary>
    /// <param name="bookId"></param>
    /// <returns></returns>
    [HttpPost("v1/library/{bookId}")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    [ChapterAccess(failOnMissing: false, chapterIdKey: "bookId")]
    public async Task<ActionResult> AddBook(string bookId)
    {
        var ct = HttpContext.RequestAborted;

        if (!int.TryParse(bookId, out var kavitaId))
        {
            return await koboSyncService.ProxyRequestIfEnabled();
        }

        var syncPreferences = await unitOfWork.UserRepository.GetKoboSyncPreferences(UserId, ct);
        if (!syncPreferences.HasManagedCollection)
        {
            // We cannot handle this for non collection syncs
            return Ok();
        }

        var seriesId = await unitOfWork.ChapterRepository.GetSeriesIdForChapter(kavitaId, ct);
        if (seriesId is null) return NotFound();

        var userCollection = await unitOfWork.CollectionTagRepository.GetCollectionAsync(syncPreferences.ManagedCollection, CollectionIncludes.Series, ct: ct);
        if (userCollection is null || userCollection.AppUserId != UserId) return NotFound();

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

    /// <summary>
    /// Returns reviews for the book. Implemented to reduce upstream bad requests.
    ///
    /// Not intended for use beyond Kobo devices
    /// </summary>
    /// <param name="bookId"></param>
    /// <returns></returns>
    [HttpGet("v1/products/{bookId}/reviews")]
    [ChapterAccess(failOnMissing: false, chapterIdKey: "bookId")]
    public async Task<ActionResult> GetReviews(string bookId)
    {
        if (!int.TryParse(bookId, out _))
        {
            return await koboSyncService.ProxyRequestIfEnabled();
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

    /// <summary>
    /// Returns the first books in a series that's recommended to the series the book belongs to
    /// Requires Kavita+ and Collection Sync
    ///
    /// Not intended for use beyond Kobo devices
    /// </summary>
    /// <param name="bookId"></param>
    /// <returns></returns>
    [HttpGet("v1/products/{bookId}/recommendations")]
    [ChapterAccess(failOnMissing: false, chapterIdKey: "bookId")]
    public async Task<ActionResult> GetRecommendations(string bookId)
    {
        var ct = HttpContext.RequestAborted;

        if (!int.TryParse(bookId, out var kavitaId))
        {
            return await koboSyncService.ProxyRequestIfEnabled();
        }

        var rec = await unitOfWork.ChapterRepository.GetKoboRecommendationAsync(UserId, kavitaId, ct);

        return Ok(rec);
    }

    /// <summary>
    /// Book metadata when viewing recommendations
    ///
    /// Not intended for use beyond Kobo devices
    /// </summary>
    /// <param name="bookId"></param>
    /// <returns></returns>
    [HttpGet("v1/products/books/{bookId}/")]
    [ChapterAccess(failOnMissing: false, chapterIdKey: "bookId")]
    public async Task<ActionResult> GetBook(string bookId)
    {
        var ct = HttpContext.RequestAborted;

        if (!int.TryParse(bookId, out var kavitaId))
        {
            return await koboSyncService.ProxyRequestIfEnabled();
        }

        var detail = await unitOfWork.ChapterRepository.GetKoboBookDetailsAsync(kavitaId, ct);
        if (detail is null)
        {
            return NotFound();
        }

        return Ok(detail);
    }

    /// <summary>
    /// Implemented to reduce upstream bad requests. Called during sync, and sometimes otherwise
    ///
    /// Not intended for use beyond Kobo devices
    /// </summary>
    /// <param name="bookId"></param>
    /// <returns></returns>
    [HttpGet("v1/products/{bookId}/nextread")]
    [ChapterAccess(failOnMissing: false, chapterIdKey: "bookId")]
    public async Task<ActionResult> GetNextRead(string bookId)
    {
        if (!int.TryParse(bookId, out _))
        {
            return await koboSyncService.ProxyRequestIfEnabled();
        }

        // TODO: Implement

        return Ok(new List<object>());
    }

    /// <summary>
    /// Implemented to reduce upstream bad requests. Called during sync, and sometimes otherwise
    ///
    /// Not intended for use beyond Kobo devices
    /// </summary>
    /// <param name="bookId"></param>
    /// <returns></returns>
    [HttpGet("v1/products/books/series/{bookId}")]
    [ChapterAccess(failOnMissing: false, chapterIdKey: "bookId")]
    public async Task<ActionResult> GetSeries(string bookId)
    {
        if (!int.TryParse(bookId, out _))
        {
            return await koboSyncService.ProxyRequestIfEnabled();
        }

        // TODO: Figure out what this returns and implement

        return Ok(new {
            TotalResultCount = 0,
            SearchResults = new List<object>(),
        });
    }

    /// <summary>
    /// Implemented to reduce upstream bad requests. Always returns FREE
    ///
    /// Not intended for use beyond Kobo devices
    /// </summary>
    /// <param name="bookIds"></param>
    /// <returns></returns>
    [HttpGet("v1/products/{bookIds}/prices")]
    public async Task<ActionResult> GetPrices(string bookIds)
    {
        var isKoboRequest = bookIds.Split(',').Any(id => !int.TryParse(id, out _));
        if (isKoboRequest)
        {
            return await koboSyncService.ProxyRequestIfEnabled();
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

    /// <summary>
    /// Updates review for a book. Doesn't work 100%. Reviews are a bit weird on Kobo, needs more work
    ///
    /// Not intended for use beyond Kobo devices
    /// </summary>
    /// <param name="bookId"></param>
    /// <returns></returns>
    [HttpPut("v1/products/{bookId}/reviews")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    [ChapterAccess(failOnMissing: false, chapterIdKey: "bookId")]
    public async Task<ActionResult> UpdateReview(string bookId)
    {
        var ct = HttpContext.RequestAborted;
        if (!int.TryParse(bookId, out var kavitaId))
        {
            return await koboSyncService.ProxyRequestIfEnabled();
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

    /// <summary>
    /// Returns a list of reviews
    ///
    /// Not intended for use beyond Kobo devices
    /// </summary>
    /// <param name="productIds"></param>
    /// <returns></returns>
    [HttpGet("v1/user/reviews")]
    public async Task<ActionResult> GetReviews([FromQuery(Name = "ProductIds")] List<string> productIds)
    {
        var ct = HttpContext.RequestAborted;

        var isKoboRequest = productIds.Any(id => !int.TryParse(id, out _));
        if (isKoboRequest)
        {
            return await koboSyncService.ProxyRequestIfEnabled();
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

    /// <summary>
    /// Dummy implementation to reduce failure when forward is off. Returns an empty pagination response
    ///
    /// Not intended for use beyond Kobo devices
    /// </summary>
    /// <returns></returns>
    [HttpGet("v1/user/wishlist")]
    public async Task<ActionResult> GetWishlist()
    {
        if ((await KoboSyncSettings).ForwardToKoboStore)
        {
            return await koboSyncService.ProxyRequestIfEnabled();
        }

        return Ok(KoboWishlistResponseDto.Empty);
    }

    #endregion

    #region Analytics

    /// <summary>
    /// Dummy implementation
    ///
    /// Not intended for use beyond Kobo devices
    /// </summary>
    /// <returns></returns>
    [HttpGet("v1/analytics/gettests")]
    [HttpPost("v1/analytics/gettests")]
    public ActionResult GetTests()
    {
        return Ok(new KoboAnalyticsTestsResponseDto());
    }

    #endregion

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
        return koboSyncService.ProxyRequestIfEnabled();
    }

    private KoboSyncToken KoboSyncToken => KoboSyncTokenHelper.Decode(Request.Headers[KoboHeaders.KoboSyncToken]);
    private string KoboDeviceId => Request.Headers[KoboHeaders.KoboDeviceId].FirstOrDefault() ?? string.Empty;

    private Task<AppUserKoboSyncPreferences> KoboSyncSettings => unitOfWork.UserRepository.GetKoboSyncPreferences(UserId, HttpContext.RequestAborted);

}
