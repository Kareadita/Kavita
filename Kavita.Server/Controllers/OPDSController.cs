using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Kavita.API.Database;
using Kavita.API.Errors;
using Kavita.API.Services;
using Kavita.API.Services.Reading;
using Kavita.Common;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.OPDS;
using Kavita.Models.DTOs.OPDS.Requests;
using Kavita.Models.DTOs.Progress;
using Kavita.Models.Entities.Enums;
using Kavita.Server.Attributes;
using Kavita.Services;
using Kavita.Services.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MimeTypes;

namespace Kavita.Server.Controllers;

[Authorize]
public class OpdsController(
    IUnitOfWork unitOfWork,
    IDownloadService downloadService,
    IDirectoryService directoryService,
    ICacheService cacheService,
    IReaderService readerService,
    ILocalizationService localizationService,
    IOpdsService opdsService)
    : BaseApiController
{
    private readonly XmlSerializer _xmlOpenSearchSerializer = new(typeof(OpenSearchDescription));


    /// <summary>
    /// Returns the Catalogue for Kavita's OPDS Service
    /// </summary>
    /// <param name="apiKey"></param>
    /// <returns></returns>
    [HttpPost("{apiKey}")]
    [HttpGet("{apiKey}")]
    [Produces("application/xml")]
    public async Task<IActionResult> Get(string apiKey)
    {
        var (baseUrl, prefix) = await GetPrefix();

        var feed = await opdsService.GetCatalogue(new OpdsCatalogueRequest
        {
            ApiKey = apiKey,
            Prefix = prefix,
            BaseUrl =  baseUrl,
            Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(UserId),
            UserId = UserId
        });


        return CreateXmlResult(opdsService.SerializeXml(feed));
    }

    private async Task<Tuple<string, string>> GetPrefix()
    {
        var baseUrl = (await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.BaseUrl)).Value;
        var prefix = OpdsService.DefaultApiPrefix;
        if (!Configuration.DefaultBaseUrl.Equals(baseUrl, StringComparison.InvariantCultureIgnoreCase))
        {
            // We need to update the Prefix to account for baseUrl
            prefix = baseUrl.TrimEnd('/') + OpdsService.DefaultApiPrefix;
        }

        return new Tuple<string, string>(baseUrl, prefix);
    }

    /// <summary>
    /// Get the User's Smart Filter - Supports Pagination
    /// </summary>
    /// <remarks>Smart filters have different entity types, this will resolve to the underlying entity</remarks>
    /// <returns></returns>
    [Produces("application/xml")]
    [HttpGet("{apiKey}/smart-filters/{filterId}")]
    public async Task<IActionResult> GetSmartFilter(string apiKey, int filterId, [FromQuery] int pageNumber = OpdsService.FirstPageNumber)
    {
        var userId = UserId;
        var (baseUrl, prefix) = await GetPrefix();

        var feed = await opdsService.ResolveSmartFilter(new OpdsItemsFromEntityIdRequest()
        {
            ApiKey = apiKey,
            Prefix =  prefix,
            BaseUrl = baseUrl,
            EntityId = filterId,
            UserId = userId,
            Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(UserId),
            PageNumber = pageNumber
        });


        return CreateXmlResult(opdsService.SerializeXml(feed));
    }

    /// <summary>
    /// Get the User's Smart Filters (Dashboard Context) - Supports Pagination
    /// </summary>
    /// <param name="apiKey"></param>
    /// <param name="pageNumber"></param>
    /// <returns></returns>
    [Produces("application/xml")]
    [HttpGet("{apiKey}/smart-filters")]
    public async Task<IActionResult> GetSmartFilters(string apiKey, [FromQuery] int pageNumber = OpdsService.FirstPageNumber)
    {
        try
        {
            var userId = UserId;
            var (baseUrl, prefix) = await GetPrefix();

            var feed = await opdsService.GetSmartFilters(new OpdsPaginatedCatalogueRequest()
            {
                BaseUrl = baseUrl,
                Prefix = prefix,
                UserId = userId,
                Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(UserId),
                ApiKey = apiKey,
                PageNumber = pageNumber
            });

            return CreateXmlResult(opdsService.SerializeXml(feed));
        }
        catch (OpdsException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Get the User's Libraries - No Pagination Support
    /// </summary>
    /// <param name="apiKey"></param>
    /// <param name="pageNumber"></param>
    /// <returns></returns>
    [HttpGet("{apiKey}/libraries")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetLibraries(string apiKey, [FromQuery] int pageNumber = OpdsService.FirstPageNumber)
    {
        try
        {
            var (baseUrl, prefix) = await GetPrefix();

            var feed = await opdsService.GetLibraries(new OpdsPaginatedCatalogueRequest()
            {
                BaseUrl = baseUrl,
                Prefix = prefix,
                UserId = UserId,
                Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(UserId),
                ApiKey = apiKey,
                PageNumber = pageNumber
            });

            return CreateXmlResult(opdsService.SerializeXml(feed));
        }
        catch (OpdsException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Get the User's Want to Read list - Supports Pagination
    /// </summary>
    /// <param name="apiKey"></param>
    /// <param name="pageNumber"></param>
    /// <returns></returns>
    [Produces("application/xml")]
    [HttpGet("{apiKey}/want-to-read")]
    public async Task<IActionResult> GetWantToRead(string apiKey, [FromQuery] int pageNumber = OpdsService.FirstPageNumber)
    {
        try
        {
            var (baseUrl, prefix) = await GetPrefix();

            var feed = await opdsService.GetWantToRead(new OpdsPaginatedCatalogueRequest()
            {
                BaseUrl = baseUrl,
                Prefix = prefix,
                UserId = UserId,
                Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(UserId),
                ApiKey = apiKey,
                PageNumber = pageNumber
            });

            return CreateXmlResult(opdsService.SerializeXml(feed));
        }
        catch (OpdsException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Get all Collections - Supports Pagination
    /// </summary>
    /// <param name="apiKey"></param>
    /// <param name="pageNumber"></param>
    /// <returns></returns>
    [Produces("application/xml")]
    [HttpGet("{apiKey}/collections")]
    public async Task<IActionResult> GetCollections(string apiKey, [FromQuery] int pageNumber = OpdsService.FirstPageNumber)
    {
        try
        {
            var (baseUrl, prefix) = await GetPrefix();

            var feed = await opdsService.GetCollections(new OpdsPaginatedCatalogueRequest()
            {
                BaseUrl = baseUrl,
                Prefix = prefix,
                UserId = UserId,
                Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(UserId),
                ApiKey = apiKey,
                PageNumber = pageNumber
            });

            return CreateXmlResult(opdsService.SerializeXml(feed));
        }
        catch (OpdsException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Get Series for a given Collection - Supports Pagination
    /// </summary>
    /// <param name="collectionId"></param>
    /// <param name="apiKey"></param>
    /// <param name="pageNumber"></param>
    /// <returns></returns>
    [Produces("application/xml")]
    [HttpGet("{apiKey}/collections/{collectionId}")]
    public async Task<IActionResult> GetCollection(int collectionId, string apiKey, [FromQuery] int pageNumber = OpdsService.FirstPageNumber)
    {
        try
        {
            var (baseUrl, prefix) = await GetPrefix();

            var feed = await opdsService.GetSeriesFromCollection(new OpdsItemsFromEntityIdRequest()
            {
                BaseUrl = baseUrl,
                Prefix = prefix,
                UserId = UserId,
                Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(UserId),
                ApiKey = apiKey,
                PageNumber = pageNumber,
                EntityId = collectionId
            });

            return CreateXmlResult(opdsService.SerializeXml(feed));
        }
        catch (OpdsException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Get a User's Reading Lists - Supports Pagination
    /// </summary>
    /// <param name="apiKey"></param>
    /// <param name="pageNumber"></param>
    /// <returns></returns>
    [Produces("application/xml")]
    [HttpGet("{apiKey}/reading-list")]
    public async Task<IActionResult> GetReadingLists(string apiKey, [FromQuery] int pageNumber = OpdsService.FirstPageNumber)
    {
        try
        {
            var (baseUrl, prefix) = await GetPrefix();

            var feed = await opdsService.GetReadingLists(new OpdsPaginatedCatalogueRequest()
            {
                BaseUrl = baseUrl,
                Prefix = prefix,
                UserId = UserId,
                Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(UserId),
                ApiKey = apiKey,
                PageNumber = pageNumber
            });

            return CreateXmlResult(opdsService.SerializeXml(feed));
        }
        catch (OpdsException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Returns individual items (chapters) from Reading List by ID - Supports Pagination
    /// </summary>
    /// <param name="readingListId"></param>
    /// <param name="apiKey"></param>
    /// <param name="pageNumber"></param>
    /// <returns></returns>
    [Produces("application/xml")]
    [HttpGet("{apiKey}/reading-list/{readingListId}")]
    public async Task<IActionResult> GetReadingListItems(int readingListId, string apiKey, [FromQuery] int pageNumber = OpdsService.FirstPageNumber)
    {
        try
        {
            var (baseUrl, prefix) = await GetPrefix();

            var feed = await opdsService.GetReadingListItems(new OpdsItemsFromEntityIdRequest()
            {
                BaseUrl = baseUrl,
                Prefix = prefix,
                UserId = UserId,
                Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(UserId),
                ApiKey = apiKey,
                PageNumber = pageNumber,
                EntityId = readingListId
            });

            return CreateXmlResult(opdsService.SerializeXml(feed));
        }
        catch (OpdsException ex)
        {
            return BadRequest(ex.Message);
        }
    }


    /// <summary>
    /// Returns Series from the Library - Supports Pagination
    /// </summary>
    /// <param name="libraryId"></param>
    /// <param name="apiKey"></param>
    /// <param name="pageNumber"></param>
    /// <returns></returns>
    [Produces("application/xml")]
    [HttpGet("{apiKey}/libraries/{libraryId}")]
    public async Task<IActionResult> GetSeriesForLibrary(int libraryId, string apiKey, [FromQuery] int pageNumber = OpdsService.FirstPageNumber)
    {
        try
        {
            var (baseUrl, prefix) = await GetPrefix();

            var feed = await opdsService.GetSeriesFromLibrary(new OpdsItemsFromEntityIdRequest()
            {
                BaseUrl = baseUrl,
                Prefix = prefix,
                UserId = UserId,
                Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(UserId),
                ApiKey = apiKey,
                PageNumber = pageNumber,
                EntityId = libraryId
            });

            return CreateXmlResult(opdsService.SerializeXml(feed));
        }
        catch (OpdsException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Returns Recently Added (Dashboard Feed) - Supports Pagination
    /// </summary>
    /// <param name="apiKey"></param>
    /// <param name="pageNumber"></param>
    /// <returns></returns>
    [Produces("application/xml")]
    [HttpGet("{apiKey}/recently-added")]
    public async Task<IActionResult> GetRecentlyAdded(string apiKey, [FromQuery] int pageNumber = OpdsService.FirstPageNumber)
    {
        try
        {
            var (baseUrl, prefix) = await GetPrefix();
            var feed = await opdsService.GetRecentlyAdded(new OpdsPaginatedCatalogueRequest()
            {
                BaseUrl = baseUrl,
                Prefix = prefix,
                UserId = UserId,
                Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(UserId),
                ApiKey = apiKey,
                PageNumber = pageNumber,
            });

            return CreateXmlResult(opdsService.SerializeXml(feed));
        }
        catch (OpdsException ex)
        {
            return BadRequest(ex.Message);
        }
    }


    /// <summary>
    /// Get the Recently Updated Series (Dashboard) - Pagination available, total pages will not be filled due to underlying implementation
    /// </summary>
    /// <param name="apiKey"></param>
    /// <param name="pageNumber"></param>
    /// <returns></returns>
    [Produces("application/xml")]
    [HttpGet("{apiKey}/recently-updated")]
    public async Task<IActionResult> GetRecentlyUpdated(string apiKey, [FromQuery] int pageNumber = OpdsService.FirstPageNumber)
    {
        try
        {
            var (baseUrl, prefix) = await GetPrefix();
            var feed = await opdsService.GetRecentlyUpdated(new OpdsPaginatedCatalogueRequest()
            {
                BaseUrl = baseUrl,
                Prefix = prefix,
                UserId = UserId,
                Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(UserId),
                ApiKey = apiKey,
                PageNumber = pageNumber,
            });

            return CreateXmlResult(opdsService.SerializeXml(feed));
        }
        catch (OpdsException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Get the On Deck (Dashboard) - Supports Pagination
    /// </summary>
    /// <param name="apiKey"></param>
    /// <param name="pageNumber"></param>
    /// <returns></returns>
    [HttpGet("{apiKey}/on-deck")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetOnDeck(string apiKey, [FromQuery] int pageNumber = OpdsService.FirstPageNumber)
    {
        try
        {
            var (baseUrl, prefix) = await GetPrefix();
            var feed = await opdsService.GetOnDeck(new OpdsPaginatedCatalogueRequest()
            {
                BaseUrl = baseUrl,
                Prefix = prefix,
                UserId = UserId,
                Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(UserId),
                ApiKey = apiKey,
                PageNumber = pageNumber,
            });

            return CreateXmlResult(opdsService.SerializeXml(feed));
        }
        catch (OpdsException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// OPDS Search endpoint
    /// </summary>
    /// <param name="apiKey"></param>
    /// <param name="query"></param>
    /// <returns></returns>
    [HttpGet("{apiKey}/series")]
    [Produces("application/xml")]
    public async Task<IActionResult> SearchSeries(string apiKey, [FromQuery] string query)
    {
        try
        {
            var (baseUrl, prefix) = await GetPrefix();
            var feed = await opdsService.Search(new OpdsSearchRequest()
            {
                BaseUrl = baseUrl,
                Prefix = prefix,
                UserId = UserId,
                Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(UserId),
                ApiKey = apiKey,
                Query = query,
            });

            return CreateXmlResult(opdsService.SerializeXml(feed));
        }
        catch (OpdsException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{apiKey}/search")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetSearchDescriptor(string apiKey)
    {
        var userId = UserId;
        var (_, prefix) = await GetPrefix();

        var feed = new OpenSearchDescription()
        {
            ShortName = await localizationService.TranslateAsync(userId, "search"),
            Description = await localizationService.TranslateAsync(userId, "search-description"),
            Url = new SearchLink()
            {
                Type = FeedLinkType.AtomAcquisition,
                Template = $"{prefix}{apiKey}/series?query=" + "{searchTerms}"
            }
        };

        await using var sm = new StringWriter();
        _xmlOpenSearchSerializer.Serialize(sm, feed);

        return CreateXmlResult(sm.ToString().Replace("utf-16", "utf-8"));
    }

    /// <summary>
    /// Returns the items within a Series (Series Detail)
    /// </summary>
    /// <param name="apiKey"></param>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    [SeriesAccess]
    [HttpGet("{apiKey}/series/{seriesId}")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetSeriesDetail(string apiKey, int seriesId)
    {
        try
        {
            var (baseUrl, prefix) = await GetPrefix();

            var feed = await opdsService.GetSeriesDetail(new OpdsItemsFromEntityIdRequest()
            {
                BaseUrl = baseUrl,
                Prefix = prefix,
                UserId = UserId,
                Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(UserId),
                ApiKey = apiKey,
                EntityId = seriesId
            });

            return CreateXmlResult(opdsService.SerializeXml(feed));
        }
        catch (OpdsException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Returns items for a given Volume
    /// </summary>
    /// <param name="apiKey"></param>
    /// <param name="seriesId"></param>
    /// <param name="volumeId"></param>
    /// <returns></returns>
    [VolumeAccess]
    [Produces("application/xml")]
    [HttpGet("{apiKey}/series/{seriesId}/volume/{volumeId}")]
    public async Task<IActionResult> GetVolume(string apiKey, int seriesId, int volumeId)
    {
        try
        {
            var (baseUrl, prefix) = await GetPrefix();

            var feed = await opdsService.GetItemsFromVolume(new OpdsItemsFromCompoundEntityIdsRequest()
            {
                BaseUrl = baseUrl,
                Prefix = prefix,
                UserId = UserId,
                Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(UserId),
                ApiKey = apiKey,
                SeriesId = seriesId,
                VolumeId = volumeId
            });

            return CreateXmlResult(opdsService.SerializeXml(feed));
        }
        catch (OpdsException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Gets items for a given Chapter
    /// </summary>
    /// <param name="apiKey"></param>
    /// <param name="seriesId"></param>
    /// <param name="volumeId"></param>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    [ChapterAccess]
    [Produces("application/xml")]
    [HttpGet("{apiKey}/series/{seriesId}/volume/{volumeId}/chapter/{chapterId}")]
    public async Task<IActionResult> GetChapter(string apiKey, int seriesId, int volumeId, int chapterId)
    {
        try
        {
            var (baseUrl, prefix) = await GetPrefix();

            var feed = await opdsService.GetItemsFromChapter(new OpdsItemsFromCompoundEntityIdsRequest()
            {
                BaseUrl = baseUrl,
                Prefix = prefix,
                UserId = UserId,
                Preferences = await unitOfWork.UserRepository.GetOpdsPreferences(UserId),
                ApiKey = apiKey,
                SeriesId = seriesId,
                VolumeId = volumeId,
                ChapterId = chapterId
            });

            return CreateXmlResult(opdsService.SerializeXml(feed));
        }
        catch (OpdsException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Downloads a file (user must have download permission)
    /// </summary>
    /// <param name="apiKey">User's API Key</param>
    /// <param name="seriesId"></param>
    /// <param name="volumeId"></param>
    /// <param name="chapterId"></param>
    /// <param name="filename">Not used. Only for Chunky to allow download links</param>
    /// <returns></returns>
    [ChapterAccess]
    [Authorize(PolicyGroups.DownloadPolicy)]
    [HttpGet("{apiKey}/series/{seriesId}/volume/{volumeId}/chapter/{chapterId}/download/{filename}")]
    public async Task<ActionResult> DownloadFile(string apiKey, int seriesId, int volumeId, int chapterId, string filename)
    {
        var files = (await unitOfWork.ChapterRepository.GetFilesForChapterAsync(chapterId)).ToList();

        if (files.Count <= 1)
        {
            var (zipFile, contentType, fileDownloadName) = downloadService.GetFirstFileDownload(files);
            return PhysicalFile(zipFile, contentType, fileDownloadName, true);
        }

        // Multi-file chapter: produce a single flat .cbz by reusing the
        // page-streaming cache, which already extracts and orders pages
        // across all the chapter's files. Otherwise OPDS clients only
        // receive the first file of the chapter.
        var chapter = await cacheService.Ensure(chapterId);
        if (chapter == null)
        {
            return BadRequest(await localizationService.TranslateAsync(UserId, "cache-file-find"));
        }

        var series = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId);
        var downloadName = $"{series!.Name} - Chapter {chapter.GetNumberTitle()}.cbz";
        var dateStr = DateTime.UtcNow.ToShortDateString().Replace("/", "_");
        var outputPath = Path.Join(directoryService.TempDirectory, $"kavita_opds_merged_c{chapterId}_{dateStr}.cbz");

        if (!System.IO.File.Exists(outputPath))
        {
            var cacheDir = cacheService.GetCachePath(chapterId);
            ZipFile.CreateFromDirectory(cacheDir, outputPath);
        }

        return PhysicalFile(outputPath, "application/x-cbz", downloadName, true);
    }

    private static ContentResult CreateXmlResult(string xml)
    {
        return new ContentResult
        {
            ContentType = "application/xml",
            Content = xml,
            StatusCode = 200
        };
    }


    /// <summary>
    /// This returns a streamed image following OPDS-PS v1.2
    /// </summary>
    /// <param name="apiKey"></param>
    /// <param name="libraryId"></param>
    /// <param name="seriesId"></param>
    /// <param name="volumeId"></param>
    /// <param name="chapterId"></param>
    /// <param name="pageNumber"></param>
    /// <param name="saveProgress">Optional parameter. Can pass false and progress saving will be suppressed</param>
    /// <returns></returns>
    [ChapterAccess]
    [HttpGet("{apiKey}/image")]
    public async Task<ActionResult> GetPageStreamedImage(string apiKey, [FromQuery] int libraryId, [FromQuery] int seriesId,
        [FromQuery] int volumeId,[FromQuery] int chapterId, [FromQuery] int pageNumber, [FromQuery] bool saveProgress = true)
    {
        var userId = UserId;
        if (pageNumber < 0) return BadRequest(await localizationService.TranslateAsync(userId, "greater-0", "Page"));
        var chapter = await cacheService.Ensure(chapterId, true);
        if (chapter == null) return BadRequest(await localizationService.TranslateAsync(userId, "cache-file-find"));

        try
        {
            var path = cacheService.GetCachedPagePath(chapter.Id, pageNumber);
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
                return BadRequest(await localizationService.TranslateAsync(userId, "no-image-for-page", pageNumber));

            var content = await directoryService.ReadFileAsync(path);
            var format = Path.GetExtension(path);

            // Save progress for the user (except Panels, they will use a direct connection)
            var userAgent = Request.Headers.UserAgent.ToString();

            if (!userAgent.StartsWith("Panels", StringComparison.InvariantCultureIgnoreCase) || !saveProgress)
            {
                // Kavita expects 0-N for progress, KOReader doesn't respect the OPDS-PS spec and does some wierd stuff
                // https://github.com/Kareadita/Kavita/pull/4014#issuecomment-3313677492
                var koreaderOffset = 0;
                if (userAgent.StartsWith("Koreader", StringComparison.InvariantCultureIgnoreCase))
                {
                    var totalPages = await unitOfWork.ChapterRepository.GetChapterTotalPagesAsync(chapterId);
                    if (totalPages - pageNumber < 2)
                    {
                        koreaderOffset = 1;
                    }
                }

                await readerService.SaveReadingProgress(new ProgressDto()
                {
                    ChapterId = chapterId,
                    PageNum = pageNumber + koreaderOffset,
                    SeriesId = seriesId,
                    VolumeId = volumeId,
                    LibraryId =libraryId
                }, userId);
            }

            return CachedContent(content, MimeTypeMap.GetMimeType(format));
        }
        catch (Exception)
        {
            cacheService.CleanupChapters([chapterId]);
            throw;
        }
    }

    [HttpGet("{apiKey}/favicon")]
    [ResponseCache(CacheProfileName = ResponseCacheProfiles.Month)]
    public async Task<ActionResult> GetFavicon(string apiKey)
    {
        var files = directoryService.GetFilesWithExtension(Path.Join(Directory.GetCurrentDirectory(), ".."), @"\.ico");
        if (files.Length == 0) return BadRequest(await localizationService.TranslateAsync(UserId, "favicon-doesnt-exist"));

        var path = files[0];
        var content = await directoryService.ReadFileAsync(path);
        var format = Path.GetExtension(path);

        return File(content, MimeTypeMap.GetMimeType(format));
    }
}
