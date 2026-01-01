using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Serialization;
using API.Data;
using API.DTOs.OPDS;
using API.DTOs.OPDS.Requests;
using API.DTOs.Progress;
using API.Entities.Enums;
using API.Exceptions;
using API.Services;
using API.Services.Reading;
using Kavita.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MimeTypes;

namespace API.Controllers;
#nullable enable

[Authorize]
public class OpdsController : BaseApiController
{
    private readonly IOpdsService _opdsService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDownloadService _downloadService;
    private readonly IDirectoryService _directoryService;
    private readonly ICacheService _cacheService;
    private readonly IReaderService _readerService;
    private readonly IAccountService _accountService;
    private readonly ILocalizationService _localizationService;
    private readonly XmlSerializer _xmlOpenSearchSerializer;

    public OpdsController(IUnitOfWork unitOfWork, IDownloadService downloadService,
        IDirectoryService directoryService, ICacheService cacheService,
        IReaderService readerService, IAccountService accountService,
        ILocalizationService localizationService, IOpdsService opdsService)
    {
        _unitOfWork = unitOfWork;
        _downloadService = downloadService;
        _directoryService = directoryService;
        _cacheService = cacheService;
        _readerService = readerService;
        _accountService = accountService;
        _localizationService = localizationService;
        _opdsService = opdsService;

        _xmlOpenSearchSerializer = new XmlSerializer(typeof(OpenSearchDescription));
    }


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

        var feed = await _opdsService.GetCatalogue(new OpdsCatalogueRequest
        {
            ApiKey = apiKey,
            Prefix = prefix,
            BaseUrl =  baseUrl,
            UserId = UserId
        });


        return CreateXmlResult(_opdsService.SerializeXml(feed));
    }

    private async Task<Tuple<string, string>> GetPrefix()
    {
        var baseUrl = (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.BaseUrl)).Value;
        var prefix = OpdsService.DefaultApiPrefix;
        if (!Configuration.DefaultBaseUrl.Equals(baseUrl, StringComparison.InvariantCultureIgnoreCase))
        {
            // We need to update the Prefix to account for baseUrl
            prefix = baseUrl.TrimEnd('/') + OpdsService.DefaultApiPrefix;
        }

        return new Tuple<string, string>(baseUrl, prefix);
    }

    /// <summary>
    /// Get the User's Smart Filter series - Supports Pagination
    /// </summary>
    /// <returns></returns>
    [HttpGet("{apiKey}/smart-filters/{filterId}")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetSmartFilter(string apiKey, int filterId, [FromQuery] int pageNumber = OpdsService.FirstPageNumber)
    {
        var userId = UserId;
        var (baseUrl, prefix) = await GetPrefix();

        var feed = await _opdsService.GetSeriesFromSmartFilter(new OpdsItemsFromEntityIdRequest()
        {
            ApiKey = apiKey,
            Prefix =  prefix,
            BaseUrl = baseUrl,
            EntityId = filterId,
            UserId = userId,
            PageNumber = pageNumber
        });


        return CreateXmlResult(_opdsService.SerializeXml(feed));
    }

    /// <summary>
    /// Get the User's Smart Filters (Dashboard Context) - Supports Pagination
    /// </summary>
    /// <param name="apiKey"></param>
    /// <param name="pageNumber"></param>
    /// <returns></returns>
    [HttpGet("{apiKey}/smart-filters")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetSmartFilters(string apiKey, [FromQuery] int pageNumber = OpdsService.FirstPageNumber)
    {
        try
        {
            var userId = UserId;
            var (baseUrl, prefix) = await GetPrefix();

            var feed = await _opdsService.GetSmartFilters(new OpdsPaginatedCatalogueRequest()
            {
                BaseUrl = baseUrl,
                Prefix = prefix,
                UserId = userId,
                ApiKey = apiKey,
                PageNumber = pageNumber
            });

            return CreateXmlResult(_opdsService.SerializeXml(feed));
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

            var feed = await _opdsService.GetLibraries(new OpdsPaginatedCatalogueRequest()
            {
                BaseUrl = baseUrl,
                Prefix = prefix,
                UserId = UserId,
                ApiKey = apiKey,
                PageNumber = pageNumber
            });

            return CreateXmlResult(_opdsService.SerializeXml(feed));
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
    [HttpGet("{apiKey}/want-to-read")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetWantToRead(string apiKey, [FromQuery] int pageNumber = OpdsService.FirstPageNumber)
    {
        try
        {
            var (baseUrl, prefix) = await GetPrefix();

            var feed = await _opdsService.GetWantToRead(new OpdsPaginatedCatalogueRequest()
            {
                BaseUrl = baseUrl,
                Prefix = prefix,
                UserId = UserId,
                ApiKey = apiKey,
                PageNumber = pageNumber
            });

            return CreateXmlResult(_opdsService.SerializeXml(feed));
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
    [HttpGet("{apiKey}/collections")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetCollections(string apiKey, [FromQuery] int pageNumber = OpdsService.FirstPageNumber)
    {
        try
        {
            var (baseUrl, prefix) = await GetPrefix();

            var feed = await _opdsService.GetCollections(new OpdsPaginatedCatalogueRequest()
            {
                BaseUrl = baseUrl,
                Prefix = prefix,
                UserId = UserId,
                ApiKey = apiKey,
                PageNumber = pageNumber
            });

            return CreateXmlResult(_opdsService.SerializeXml(feed));
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
    [HttpGet("{apiKey}/collections/{collectionId}")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetCollection(int collectionId, string apiKey, [FromQuery] int pageNumber = OpdsService.FirstPageNumber)
    {
        try
        {
            var (baseUrl, prefix) = await GetPrefix();

            var feed = await _opdsService.GetSeriesFromCollection(new OpdsItemsFromEntityIdRequest()
            {
                BaseUrl = baseUrl,
                Prefix = prefix,
                UserId = UserId,
                ApiKey = apiKey,
                PageNumber = pageNumber,
                EntityId = collectionId
            });

            return CreateXmlResult(_opdsService.SerializeXml(feed));
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
    [HttpGet("{apiKey}/reading-list")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetReadingLists(string apiKey, [FromQuery] int pageNumber = OpdsService.FirstPageNumber)
    {
        try
        {
            var (baseUrl, prefix) = await GetPrefix();

            var feed = await _opdsService.GetReadingLists(new OpdsPaginatedCatalogueRequest()
            {
                BaseUrl = baseUrl,
                Prefix = prefix,
                UserId = UserId,
                ApiKey = apiKey,
                PageNumber = pageNumber
            });

            return CreateXmlResult(_opdsService.SerializeXml(feed));
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
    [HttpGet("{apiKey}/reading-list/{readingListId}")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetReadingListItems(int readingListId, string apiKey, [FromQuery] int pageNumber = OpdsService.FirstPageNumber)
    {
        try
        {
            var (baseUrl, prefix) = await GetPrefix();

            var feed = await _opdsService.GetReadingListItems(new OpdsItemsFromEntityIdRequest()
            {
                BaseUrl = baseUrl,
                Prefix = prefix,
                UserId = UserId,
                ApiKey = apiKey,
                PageNumber = pageNumber,
                EntityId = readingListId
            });

            return CreateXmlResult(_opdsService.SerializeXml(feed));
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
    [HttpGet("{apiKey}/libraries/{libraryId}")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetSeriesForLibrary(int libraryId, string apiKey, [FromQuery] int pageNumber = OpdsService.FirstPageNumber)
    {
        try
        {
            var (baseUrl, prefix) = await GetPrefix();

            var feed = await _opdsService.GetSeriesFromLibrary(new OpdsItemsFromEntityIdRequest()
            {
                BaseUrl = baseUrl,
                Prefix = prefix,
                UserId = UserId,
                ApiKey = apiKey,
                PageNumber = pageNumber,
                EntityId = libraryId
            });

            return CreateXmlResult(_opdsService.SerializeXml(feed));
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
    [HttpGet("{apiKey}/recently-added")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetRecentlyAdded(string apiKey, [FromQuery] int pageNumber = OpdsService.FirstPageNumber)
    {
        try
        {
            var (baseUrl, prefix) = await GetPrefix();
            var feed = await _opdsService.GetRecentlyAdded(new OpdsPaginatedCatalogueRequest()
            {
                BaseUrl = baseUrl,
                Prefix = prefix,
                UserId = UserId,
                ApiKey = apiKey,
                PageNumber = pageNumber,
            });

            return CreateXmlResult(_opdsService.SerializeXml(feed));
        }
        catch (OpdsException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Returns More In a Genre (Dashboard Feed) - Supports Pagination
    /// </summary>
    /// <param name="apiKey"></param>
    /// <param name="genreId"></param>
    /// <param name="pageNumber"></param>
    /// <returns></returns>
    [HttpGet("{apiKey}/more-in-genre")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetMoreInGenre(string apiKey, [FromQuery] int genreId, [FromQuery] int pageNumber = OpdsService.FirstPageNumber)
    {
        try
        {
            var (baseUrl, prefix) = await GetPrefix();
            var feed = await _opdsService.GetMoreInGenre(new OpdsItemsFromEntityIdRequest()
            {
                BaseUrl = baseUrl,
                Prefix = prefix,
                UserId = UserId,
                ApiKey = apiKey,
                PageNumber = pageNumber,
                EntityId = genreId
            });

            return CreateXmlResult(_opdsService.SerializeXml(feed));
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
    [HttpGet("{apiKey}/recently-updated")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetRecentlyUpdated(string apiKey, [FromQuery] int pageNumber = OpdsService.FirstPageNumber)
    {
        try
        {
            var (baseUrl, prefix) = await GetPrefix();
            var feed = await _opdsService.GetRecentlyUpdated(new OpdsPaginatedCatalogueRequest()
            {
                BaseUrl = baseUrl,
                Prefix = prefix,
                UserId = UserId,
                ApiKey = apiKey,
                PageNumber = pageNumber,
            });

            return CreateXmlResult(_opdsService.SerializeXml(feed));
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
            var feed = await _opdsService.GetOnDeck(new OpdsPaginatedCatalogueRequest()
            {
                BaseUrl = baseUrl,
                Prefix = prefix,
                UserId = UserId,
                ApiKey = apiKey,
                PageNumber = pageNumber,
            });

            return CreateXmlResult(_opdsService.SerializeXml(feed));
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
            var feed = await _opdsService.Search(new OpdsSearchRequest()
            {
                BaseUrl = baseUrl,
                Prefix = prefix,
                UserId = UserId,
                ApiKey = apiKey,
                Query = query,
            });

            return CreateXmlResult(_opdsService.SerializeXml(feed));
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
            ShortName = await _localizationService.Translate(userId, "search"),
            Description = await _localizationService.Translate(userId, "search-description"),
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
    [HttpGet("{apiKey}/series/{seriesId}")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetSeriesDetail(string apiKey, int seriesId)
    {
        try
        {
            var (baseUrl, prefix) = await GetPrefix();

            var feed = await _opdsService.GetSeriesDetail(new OpdsItemsFromEntityIdRequest()
            {
                BaseUrl = baseUrl,
                Prefix = prefix,
                UserId = UserId,
                ApiKey = apiKey,
                EntityId = seriesId
            });

            return CreateXmlResult(_opdsService.SerializeXml(feed));
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
    [HttpGet("{apiKey}/series/{seriesId}/volume/{volumeId}")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetVolume(string apiKey, int seriesId, int volumeId)
    {
        try
        {
            var (baseUrl, prefix) = await GetPrefix();

            var feed = await _opdsService.GetItemsFromVolume(new OpdsItemsFromCompoundEntityIdsRequest()
            {
                BaseUrl = baseUrl,
                Prefix = prefix,
                UserId = UserId,
                ApiKey = apiKey,
                SeriesId = seriesId,
                VolumeId = volumeId
            });

            return CreateXmlResult(_opdsService.SerializeXml(feed));
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
    [HttpGet("{apiKey}/series/{seriesId}/volume/{volumeId}/chapter/{chapterId}")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetChapter(string apiKey, int seriesId, int volumeId, int chapterId)
    {
        try
        {
            var (baseUrl, prefix) = await GetPrefix();

            var feed = await _opdsService.GetItemsFromChapter(new OpdsItemsFromCompoundEntityIdsRequest()
            {
                BaseUrl = baseUrl,
                Prefix = prefix,
                UserId = UserId,
                ApiKey = apiKey,
                SeriesId = seriesId,
                VolumeId = volumeId,
                ChapterId = chapterId
            });

            return CreateXmlResult(_opdsService.SerializeXml(feed));
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
    [HttpGet("{apiKey}/series/{seriesId}/volume/{volumeId}/chapter/{chapterId}/download/{filename}")]
    public async Task<ActionResult> DownloadFile(string apiKey, int seriesId, int volumeId, int chapterId, string filename)
    {
        var userId = UserId;
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId);
        if (!await _accountService.HasDownloadPermission(user))
        {
            return Forbid(await _localizationService.Translate(userId, "download-not-allowed"));
        }

        var files = await _unitOfWork.ChapterRepository.GetFilesForChapterAsync(chapterId);
        var (zipFile, contentType, fileDownloadName) = _downloadService.GetFirstFileDownload(files);
        return PhysicalFile(zipFile, contentType, fileDownloadName, true);
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
    [HttpGet("{apiKey}/image")]
    public async Task<ActionResult> GetPageStreamedImage(string apiKey, [FromQuery] int libraryId, [FromQuery] int seriesId,
        [FromQuery] int volumeId,[FromQuery] int chapterId, [FromQuery] int pageNumber, [FromQuery] bool saveProgress = true)
    {
        var userId = UserId;
        if (pageNumber < 0) return BadRequest(await _localizationService.Translate(userId, "greater-0", "Page"));
        var chapter = await _cacheService.Ensure(chapterId, true);
        if (chapter == null) return BadRequest(await _localizationService.Translate(userId, "cache-file-find"));

        try
        {
            var path = _cacheService.GetCachedPagePath(chapter.Id, pageNumber);
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
                return BadRequest(await _localizationService.Translate(userId, "no-image-for-page", pageNumber));

            var content = await _directoryService.ReadFileAsync(path);
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
                    var totalPages = await _unitOfWork.ChapterRepository.GetChapterTotalPagesAsync(chapterId);
                    if (totalPages - pageNumber < 2)
                    {
                        koreaderOffset = 1;
                    }
                }

                await _readerService.SaveReadingProgress(new ProgressDto()
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
            _cacheService.CleanupChapters([chapterId]);
            throw;
        }
    }

    [HttpGet("{apiKey}/favicon")]
    [ResponseCache(Duration = 60 * 60, Location = ResponseCacheLocation.Client, NoStore = false)]
    public async Task<ActionResult> GetFavicon(string apiKey)
    {
        var files = _directoryService.GetFilesWithExtension(Path.Join(Directory.GetCurrentDirectory(), ".."), @"\.ico");
        if (files.Length == 0) return BadRequest(await _localizationService.Translate(UserId, "favicon-doesnt-exist"));

        var path = files[0];
        var content = await _directoryService.ReadFileAsync(path);
        var format = Path.GetExtension(path);

        return File(content, MimeTypeMap.GetMimeType(format));
    }
}
