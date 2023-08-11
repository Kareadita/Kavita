using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;
using API.Comparators;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.DTOs.CollectionTags;
using API.DTOs.Filtering;
using API.DTOs.Filtering.v2;
using API.DTOs.OPDS;
using API.DTOs.Search;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers;
using API.Services;
using Kavita.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MimeTypes;

namespace API.Controllers;

#nullable enable

[AllowAnonymous]
public class OpdsController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDownloadService _downloadService;
    private readonly IDirectoryService _directoryService;
    private readonly ICacheService _cacheService;
    private readonly IReaderService _readerService;
    private readonly ISeriesService _seriesService;
    private readonly IAccountService _accountService;
    private readonly ILocalizationService _localizationService;


    private readonly XmlSerializer _xmlSerializer;
    private readonly XmlSerializer _xmlOpenSearchSerializer;
    private readonly FilterDto _filterDto = new FilterDto()
    {
        Formats = new List<MangaFormat>(),
        Character = new List<int>(),
        Colorist = new List<int>(),
        Editor = new List<int>(),
        Genres = new List<int>(),
        Inker = new List<int>(),
        Languages = new List<string>(),
        Letterer = new List<int>(),
        Penciller = new List<int>(),
        Libraries = new List<int>(),
        Publisher = new List<int>(),
        Rating = 0,
        Tags = new List<int>(),
        Translators = new List<int>(),
        Writers = new List<int>(),
        AgeRating = new List<AgeRating>(),
        CollectionTags = new List<int>(),
        CoverArtist = new List<int>(),
        ReadStatus = new ReadStatus(),
        SortOptions = null,
        PublicationStatus = new List<PublicationStatus>()
    };

    private readonly FilterV2Dto _filterV2Dto = new FilterV2Dto();
    private readonly ChapterSortComparer _chapterSortComparer = ChapterSortComparer.Default;
    private const int PageSize = 20;

    public OpdsController(IUnitOfWork unitOfWork, IDownloadService downloadService,
        IDirectoryService directoryService, ICacheService cacheService,
        IReaderService readerService, ISeriesService seriesService,
        IAccountService accountService, ILocalizationService localizationService)
    {
        _unitOfWork = unitOfWork;
        _downloadService = downloadService;
        _directoryService = directoryService;
        _cacheService = cacheService;
        _readerService = readerService;
        _seriesService = seriesService;
        _accountService = accountService;
        _localizationService = localizationService;

        _xmlSerializer = new XmlSerializer(typeof(Feed));
        _xmlOpenSearchSerializer = new XmlSerializer(typeof(OpenSearchDescription));
    }

    [HttpPost("{apiKey}")]
    [HttpGet("{apiKey}")]
    [Produces("application/xml")]
    public async Task<IActionResult> Get(string apiKey)
    {
        var userId = await GetUser(apiKey);
        if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
            return BadRequest(await _localizationService.Translate(userId, "opds-disabled"));

        var (baseUrl, prefix) = await GetPrefix();

        var feed = CreateFeed("Kavita", string.Empty, apiKey, prefix);
        SetFeedId(feed, "root");
        feed.Entries.Add(new FeedEntry()
        {
            Id = "onDeck",
            Title = await _localizationService.Translate(userId, "on-deck"),
            Content = new FeedEntryContent()
            {
                Text = await _localizationService.Translate(userId, "browse-on-deck")
            },
            Links = new List<FeedLink>()
            {
                CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, $"{prefix}{apiKey}/on-deck"),
            }
        });
        feed.Entries.Add(new FeedEntry()
        {
            Id = "recentlyAdded",
            Title = await _localizationService.Translate(userId, "recently-added"),
            Content = new FeedEntryContent()
            {
                Text = await _localizationService.Translate(userId, "browse-recently-added")
            },
            Links = new List<FeedLink>()
            {
                CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, $"{prefix}{apiKey}/recently-added"),
            }
        });
        feed.Entries.Add(new FeedEntry()
        {
            Id = "readingList",
            Title = await _localizationService.Translate(userId, "reading-lists"),
            Content = new FeedEntryContent()
            {
                Text = await _localizationService.Translate(userId, "browse-reading-lists")
            },
            Links = new List<FeedLink>()
            {
                CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, $"{prefix}{apiKey}/reading-list"),
            }
        });
        feed.Entries.Add(new FeedEntry()
        {
            Id = "allLibraries",
            Title = await _localizationService.Translate(userId, "libraries"),
            Content = new FeedEntryContent()
            {
                Text = await _localizationService.Translate(userId, "browse-libraries")
            },
            Links = new List<FeedLink>()
            {
                CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, $"{prefix}{apiKey}/libraries"),
            }
        });
        feed.Entries.Add(new FeedEntry()
        {
            Id = "allCollections",
            Title = await _localizationService.Translate(userId, "collections"),
            Content = new FeedEntryContent()
            {
                Text = await _localizationService.Translate(userId, "browse-collections")
            },
            Links = new List<FeedLink>()
            {
                CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, $"{prefix}{apiKey}/collections"),
            }
        });
        return CreateXmlResult(SerializeXml(feed));
    }

    private async Task<Tuple<string, string>> GetPrefix()
    {
        var baseUrl = (await _unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.BaseUrl)).Value;
        var prefix = "/api/opds/";
        if (!Configuration.DefaultBaseUrl.Equals(baseUrl))
        {
            // We need to update the Prefix to account for baseUrl
            prefix = baseUrl + "api/opds/";
        }

        return new Tuple<string, string>(baseUrl, prefix);
    }


    [HttpGet("{apiKey}/libraries")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetLibraries(string apiKey)
    {
        var userId = await GetUser(apiKey);
        if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
            return BadRequest(await _localizationService.Translate(userId, "opds-disabled"));
        var (baseUrl, prefix) = await GetPrefix();
        var libraries = await _unitOfWork.LibraryRepository.GetLibrariesForUserIdAsync(userId);
        var feed = CreateFeed(await _localizationService.Translate(userId, "libraries"), $"{prefix}{apiKey}/libraries", apiKey, prefix);
        SetFeedId(feed, "libraries");
        foreach (var library in libraries)
        {
            feed.Entries.Add(new FeedEntry()
            {
                Id = library.Id.ToString(),
                Title = library.Name,
                Links = new List<FeedLink>()
                {
                    CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, $"{prefix}{apiKey}/libraries/{library.Id}"),
                }
            });
        }

        return CreateXmlResult(SerializeXml(feed));
    }

    [HttpGet("{apiKey}/collections")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetCollections(string apiKey)
    {
        var userId = await GetUser(apiKey);
        if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
            return BadRequest(await _localizationService.Translate(userId, "opds-disabled"));
        var (baseUrl, prefix) = await GetPrefix();
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId);
        if (user == null) return Unauthorized();
        var isAdmin = await _unitOfWork.UserRepository.IsUserAdminAsync(user);

        var tags = isAdmin ? (await _unitOfWork.CollectionTagRepository.GetAllTagDtosAsync())
            : (await _unitOfWork.CollectionTagRepository.GetAllPromotedTagDtosAsync(userId));


        var feed = CreateFeed(await _localizationService.Translate(userId, "collections"), $"{prefix}{apiKey}/collections", apiKey, prefix);
        SetFeedId(feed, "collections");
        foreach (var tag in tags)
        {
            feed.Entries.Add(new FeedEntry()
            {
                Id = tag.Id.ToString(),
                Title = tag.Title,
                Summary = tag.Summary,
                Links = new List<FeedLink>()
                {
                    CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation,  $"{prefix}{apiKey}/collections/{tag.Id}"),
                    CreateLink(FeedLinkRelation.Image, FeedLinkType.Image, $"{baseUrl}api/image/collection-cover?collectionId={tag.Id}&apiKey={apiKey}"),
                    CreateLink(FeedLinkRelation.Thumbnail, FeedLinkType.Image, $"{baseUrl}api/image/collection-cover?collectionId={tag.Id}&apiKey={apiKey}")
                }
            });
        }

        return CreateXmlResult(SerializeXml(feed));
    }


    [HttpGet("{apiKey}/collections/{collectionId}")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetCollection(int collectionId, string apiKey, [FromQuery] int pageNumber = 0)
    {
        var userId = await GetUser(apiKey);
        if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
            return BadRequest(await _localizationService.Translate(userId, "opds-disabled"));
        var (baseUrl, prefix) = await GetPrefix();
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId);
        if (user == null) return Unauthorized();
        var isAdmin = await _unitOfWork.UserRepository.IsUserAdminAsync(user);

        IEnumerable <CollectionTagDto> tags;
        if (isAdmin)
        {
            tags = await _unitOfWork.CollectionTagRepository.GetAllTagDtosAsync();
        }
        else
        {
            tags = await _unitOfWork.CollectionTagRepository.GetAllPromotedTagDtosAsync(userId);
        }

        var tag = tags.SingleOrDefault(t => t.Id == collectionId);
        if (tag == null)
        {
            return BadRequest("Collection does not exist or you don't have access");
        }

        var series = await _unitOfWork.SeriesRepository.GetSeriesDtoForCollectionAsync(collectionId, userId, GetUserParams(pageNumber));
        var seriesMetadatas = await _unitOfWork.SeriesRepository.GetSeriesMetadataForIds(series.Select(s => s.Id));

        var feed = CreateFeed(tag.Title + " Collection", $"{prefix}{apiKey}/collections/{collectionId}", apiKey, prefix);
        SetFeedId(feed, $"collections-{collectionId}");
        AddPagination(feed, series, $"{prefix}{apiKey}/collections/{collectionId}");

        foreach (var seriesDto in series)
        {
            feed.Entries.Add(CreateSeries(seriesDto, seriesMetadatas.First(s => s.SeriesId == seriesDto.Id), apiKey, prefix, baseUrl));
        }


        return CreateXmlResult(SerializeXml(feed));
    }

    [HttpGet("{apiKey}/reading-list")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetReadingLists(string apiKey, [FromQuery] int pageNumber = 0)
    {
        var userId = await GetUser(apiKey);
        if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
            return BadRequest(await _localizationService.Translate(userId, "opds-disabled"));
        var (baseUrl, prefix) = await GetPrefix();

        var readingLists = await _unitOfWork.ReadingListRepository.GetReadingListDtosForUserAsync(userId,
            true, GetUserParams(pageNumber), false);


        var feed = CreateFeed("All Reading Lists", $"{prefix}{apiKey}/reading-list", apiKey, prefix);
        SetFeedId(feed, "reading-list");
        foreach (var readingListDto in readingLists)
        {
            feed.Entries.Add(new FeedEntry()
            {
                Id = readingListDto.Id.ToString(),
                Title = readingListDto.Title,
                Summary = readingListDto.Summary,
                Links = new List<FeedLink>()
                {
                    CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, $"{prefix}{apiKey}/reading-list/{readingListDto.Id}"),
                }
            });
        }

        return CreateXmlResult(SerializeXml(feed));
    }

    private static UserParams GetUserParams(int pageNumber)
    {
        return new UserParams()
        {
            PageNumber = pageNumber,
            PageSize = PageSize
        };
    }

    [HttpGet("{apiKey}/reading-list/{readingListId}")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetReadingListItems(int readingListId, string apiKey)
    {
        var userId = await GetUser(apiKey);
        if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
            return BadRequest(await _localizationService.Translate(userId, "opds-disabled"));
        var (baseUrl, prefix) = await GetPrefix();
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId);

        var userWithLists = await _unitOfWork.UserRepository.GetUserByUsernameAsync(user!.UserName!, AppUserIncludes.ReadingListsWithItems);
        if (userWithLists == null) return Unauthorized();
        var readingList = userWithLists.ReadingLists.SingleOrDefault(t => t.Id == readingListId);
        if (readingList == null)
        {
            return BadRequest(await _localizationService.Translate(userId, "reading-list-restricted"));
        }

        var feed = CreateFeed(readingList.Title + " " + await _localizationService.Translate(userId, "reading-list"), $"{prefix}{apiKey}/reading-list/{readingListId}", apiKey, prefix);
        SetFeedId(feed, $"reading-list-{readingListId}");

        var items = (await _unitOfWork.ReadingListRepository.GetReadingListItemDtosByIdAsync(readingListId, userId)).ToList();
        foreach (var item in items)
        {
            feed.Entries.Add(
                CreateChapter(apiKey, $"{item.Order} - {item.SeriesName}: {item.Title}",
                    string.Empty, item.ChapterId, item.VolumeId, item.SeriesId, prefix, baseUrl));
        }
        return CreateXmlResult(SerializeXml(feed));
    }

    [HttpGet("{apiKey}/libraries/{libraryId}")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetSeriesForLibrary(int libraryId, string apiKey, [FromQuery] int pageNumber = 0)
    {
        var userId = await GetUser(apiKey);
        if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
            return BadRequest(await _localizationService.Translate(userId, "opds-disabled"));
        var (baseUrl, prefix) = await GetPrefix();
        var library =
            (await _unitOfWork.LibraryRepository.GetLibrariesForUserIdAsync(userId)).SingleOrDefault(l =>
                l.Id == libraryId);
        if (library == null)
        {
            return BadRequest(await _localizationService.Translate(userId, "no-library-access"));
        }

        var filter = new FilterV2Dto();
        filter.Statements.Add(new FilterStatementDto()
        {
            Comparison = FilterComparison.Equal,
            Field = FilterField.Libraries,
            Value = libraryId + string.Empty
        });

        var series = await _unitOfWork.SeriesRepository.GetSeriesDtoForLibraryIdV2Async(userId, GetUserParams(pageNumber), filter);
        var seriesMetadatas = await _unitOfWork.SeriesRepository.GetSeriesMetadataForIds(series.Select(s => s.Id));

        var feed = CreateFeed(library.Name, $"{apiKey}/libraries/{libraryId}", apiKey, prefix);
        SetFeedId(feed, $"library-{library.Name}");
        AddPagination(feed, series, $"{prefix}{apiKey}/libraries/{libraryId}");

        foreach (var seriesDto in series)
        {
            feed.Entries.Add(CreateSeries(seriesDto, seriesMetadatas.First(s => s.SeriesId == seriesDto.Id), apiKey, prefix, baseUrl));
        }

        return CreateXmlResult(SerializeXml(feed));
    }

    [HttpGet("{apiKey}/recently-added")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetRecentlyAdded(string apiKey, [FromQuery] int pageNumber = 1)
    {
        var userId = await GetUser(apiKey);
        if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
            return BadRequest(await _localizationService.Translate(userId, "opds-disabled"));
        var (baseUrl, prefix) = await GetPrefix();
        var recentlyAdded = await _unitOfWork.SeriesRepository.GetRecentlyAddedV2(userId, GetUserParams(pageNumber), _filterV2Dto);
        var seriesMetadatas = await _unitOfWork.SeriesRepository.GetSeriesMetadataForIds(recentlyAdded.Select(s => s.Id));

        var feed = CreateFeed(await _localizationService.Translate(userId, "recently-added"), $"{prefix}{apiKey}/recently-added", apiKey, prefix);
        SetFeedId(feed, "recently-added");
        AddPagination(feed, recentlyAdded, $"{prefix}{apiKey}/recently-added");

        foreach (var seriesDto in recentlyAdded)
        {
            feed.Entries.Add(CreateSeries(seriesDto, seriesMetadatas.First(s => s.SeriesId == seriesDto.Id), apiKey, prefix, baseUrl));
        }

        return CreateXmlResult(SerializeXml(feed));
    }

    [HttpGet("{apiKey}/on-deck")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetOnDeck(string apiKey, [FromQuery] int pageNumber = 1)
    {
        var userId = await GetUser(apiKey);
        if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
            return BadRequest(await _localizationService.Translate(userId, "opds-disabled"));

        var (baseUrl, prefix) = await GetPrefix();

        var userParams = GetUserParams(pageNumber);
        var pagedList = await _unitOfWork.SeriesRepository.GetOnDeck(userId, 0, userParams, _filterDto);
        var seriesMetadatas = await _unitOfWork.SeriesRepository.GetSeriesMetadataForIds(pagedList.Select(s => s.Id));

        Response.AddPaginationHeader(pagedList.CurrentPage, pagedList.PageSize, pagedList.TotalCount, pagedList.TotalPages);

        var feed = CreateFeed(await _localizationService.Translate(userId, "on-deck"), $"{prefix}{apiKey}/on-deck", apiKey, prefix);
        SetFeedId(feed, "on-deck");
        AddPagination(feed, pagedList, $"{prefix}{apiKey}/on-deck");

        foreach (var seriesDto in pagedList)
        {
            feed.Entries.Add(CreateSeries(seriesDto, seriesMetadatas.First(s => s.SeriesId == seriesDto.Id), apiKey, prefix, baseUrl));
        }

        return CreateXmlResult(SerializeXml(feed));
    }

    [HttpGet("{apiKey}/series")]
    [Produces("application/xml")]
    public async Task<IActionResult> SearchSeries(string apiKey, [FromQuery] string query)
    {
        var userId = await GetUser(apiKey);
        if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
            return BadRequest(await _localizationService.Translate(userId, "opds-disabled"));
        var (baseUrl, prefix) = await GetPrefix();
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId);

        if (string.IsNullOrEmpty(query))
        {
            return BadRequest(await _localizationService.Translate(userId, "query-required"));
        }
        query = query.Replace(@"%", string.Empty);
        // Get libraries user has access to
        var libraries = (await _unitOfWork.LibraryRepository.GetLibrariesForUserIdAsync(userId)).ToList();
        if (!libraries.Any()) return BadRequest(await _localizationService.Translate(userId, "libraries-restricted"));

        var isAdmin = await _unitOfWork.UserRepository.IsUserAdminAsync(user);

        var series = await _unitOfWork.SeriesRepository.SearchSeries(userId, isAdmin, libraries.Select(l => l.Id).ToArray(), query);

        var feed = CreateFeed(query, $"{prefix}{apiKey}/series?query=" + query, apiKey, prefix);
        SetFeedId(feed, "search-series");
        foreach (var seriesDto in series.Series)
        {
            feed.Entries.Add(CreateSeries(seriesDto, apiKey, prefix, baseUrl));
        }

        foreach (var collection in series.Collections)
        {
            feed.Entries.Add(new FeedEntry()
            {
                Id = collection.Id.ToString(),
                Title = collection.Title,
                Summary = collection.Summary,
                Links = new List<FeedLink>()
                {
                    CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation,
                        $"{prefix}{apiKey}/collections/{collection.Id}"),
                    CreateLink(FeedLinkRelation.Image, FeedLinkType.Image,
                        $"{baseUrl}api/image/collection-cover?collectionId={collection.Id}&apiKey={apiKey}"),
                    CreateLink(FeedLinkRelation.Thumbnail, FeedLinkType.Image,
                        $"{baseUrl}api/image/collection-cover?collectionId={collection.Id}&apiKey={apiKey}")
                }
            });
        }

        foreach (var readingListDto in series.ReadingLists)
        {
            feed.Entries.Add(new FeedEntry()
            {
                Id = readingListDto.Id.ToString(),
                Title = readingListDto.Title,
                Summary = readingListDto.Summary,
                Links = new List<FeedLink>()
                {
                    CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, $"{prefix}{apiKey}/reading-list/{readingListDto.Id}"),
                }
            });
        }


        return CreateXmlResult(SerializeXml(feed));
    }

    private static void SetFeedId(Feed feed, string id)
    {
        feed.Id = id;
    }

    [HttpGet("{apiKey}/search")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetSearchDescriptor(string apiKey)
    {
        var userId = await GetUser(apiKey);
        if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
            return BadRequest(await _localizationService.Translate(userId, "opds-disabled"));
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

    [HttpGet("{apiKey}/series/{seriesId}")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetSeries(string apiKey, int seriesId)
    {
        var userId = await GetUser(apiKey);
        if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
            return BadRequest(await _localizationService.Translate(userId, "opds-disabled"));
        var (baseUrl, prefix) = await GetPrefix();
        var series = await _unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(seriesId, userId);

        var feed = CreateFeed(series!.Name + " - Storyline", $"{prefix}{apiKey}/series/{series.Id}", apiKey, prefix);
        SetFeedId(feed, $"series-{series.Id}");
        feed.Links.Add(CreateLink(FeedLinkRelation.Image, FeedLinkType.Image, $"{baseUrl}api/image/series-cover?seriesId={seriesId}&apiKey={apiKey}"));

        var seriesDetail =  await _seriesService.GetSeriesDetail(seriesId, userId);
        foreach (var volume in seriesDetail.Volumes)
        {
            var chapters = (await _unitOfWork.ChapterRepository.GetChaptersAsync(volume.Id)).OrderBy(x => double.Parse(x.Number),
        _chapterSortComparer);

            foreach (var chapter in chapters)
            {
                var files = await _unitOfWork.ChapterRepository.GetFilesForChapterAsync(chapter.Id);
                var chapterTest = await _unitOfWork.ChapterRepository.GetChapterDtoAsync(chapter.Id);
                foreach (var mangaFile in files)
                {
                    feed.Entries.Add(await CreateChapterWithFile(userId, seriesId, volume.Id, chapter.Id, mangaFile, series, chapterTest, apiKey, prefix, baseUrl));
                }
            }

        }

        foreach (var storylineChapter in seriesDetail.StorylineChapters.Where(c => !c.IsSpecial))
        {
            var files = await _unitOfWork.ChapterRepository.GetFilesForChapterAsync(storylineChapter.Id);
            var chapterTest = await _unitOfWork.ChapterRepository.GetChapterDtoAsync(storylineChapter.Id);
            foreach (var mangaFile in files)
            {
                feed.Entries.Add(await CreateChapterWithFile(userId, seriesId, storylineChapter.VolumeId, storylineChapter.Id, mangaFile, series, chapterTest, apiKey, prefix, baseUrl));
            }
        }

        foreach (var special in seriesDetail.Specials)
        {
            var files = await _unitOfWork.ChapterRepository.GetFilesForChapterAsync(special.Id);
            var chapterTest = await _unitOfWork.ChapterRepository.GetChapterDtoAsync(special.Id);
            foreach (var mangaFile in files)
            {
                feed.Entries.Add(await CreateChapterWithFile(userId, seriesId, special.VolumeId, special.Id, mangaFile, series, chapterTest, apiKey, prefix, baseUrl));
            }
        }

        return CreateXmlResult(SerializeXml(feed));
    }

    [HttpGet("{apiKey}/series/{seriesId}/volume/{volumeId}")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetVolume(string apiKey, int seriesId, int volumeId)
    {
        var userId = await GetUser(apiKey);
        if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
            return BadRequest(await _localizationService.Translate(userId, "opds-disabled"));
        var (baseUrl, prefix) = await GetPrefix();
        var series = await _unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(seriesId, userId);
        var libraryType = await _unitOfWork.LibraryRepository.GetLibraryTypeAsync(series.LibraryId);
        var volume = await _unitOfWork.VolumeRepository.GetVolumeAsync(volumeId);
        var chapters =
            (await _unitOfWork.ChapterRepository.GetChaptersAsync(volumeId)).OrderBy(x => double.Parse(x.Number),
                _chapterSortComparer);
        var feed = CreateFeed(series.Name + " - Volume " + volume!.Name + $" - {_seriesService.FormatChapterName(userId, libraryType)}s ",
            $"{prefix}{apiKey}/series/{seriesId}/volume/{volumeId}", apiKey, prefix);
        SetFeedId(feed, $"series-{series.Id}-volume-{volume.Id}-{_seriesService.FormatChapterName(userId, libraryType)}s");
        foreach (var chapter in chapters)
        {
            var files = await _unitOfWork.ChapterRepository.GetFilesForChapterAsync(chapter.Id);
            var chapterTest = await _unitOfWork.ChapterRepository.GetChapterDtoAsync(chapter.Id);
            foreach (var mangaFile in files)
            {
                feed.Entries.Add(await CreateChapterWithFile(userId, seriesId, volumeId, chapter.Id, mangaFile, series, chapterTest, apiKey, prefix, baseUrl));
            }
        }

        return CreateXmlResult(SerializeXml(feed));
    }

    [HttpGet("{apiKey}/series/{seriesId}/volume/{volumeId}/chapter/{chapterId}")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetChapter(string apiKey, int seriesId, int volumeId, int chapterId)
    {
        var userId = await GetUser(apiKey);
        if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
            return BadRequest(await _localizationService.Translate(userId, "opds-disabled"));
        var (baseUrl, prefix) = await GetPrefix();
        var series = await _unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(seriesId, userId);
        var libraryType = await _unitOfWork.LibraryRepository.GetLibraryTypeAsync(series.LibraryId);
        var chapter = await _unitOfWork.ChapterRepository.GetChapterDtoAsync(chapterId);
        if (chapter == null) return BadRequest(await _localizationService.Translate(userId, "chapter-doesnt-exist"));
        var volume = await _unitOfWork.VolumeRepository.GetVolumeAsync(volumeId);
        var files = await _unitOfWork.ChapterRepository.GetFilesForChapterAsync(chapterId);

        var feed = CreateFeed(series.Name + " - Volume " + volume!.Name + $" - {_seriesService.FormatChapterName(userId, libraryType)}s",
            $"{prefix}{apiKey}/series/{seriesId}/volume/{volumeId}/chapter/{chapterId}", apiKey, prefix);
        SetFeedId(feed, $"series-{series.Id}-volume-{volumeId}-{_seriesService.FormatChapterName(userId, libraryType)}-{chapterId}-files");
        foreach (var mangaFile in files)
        {
            feed.Entries.Add(await CreateChapterWithFile(userId, seriesId, volumeId, chapterId, mangaFile, series, chapter, apiKey, prefix, baseUrl));
        }

        return CreateXmlResult(SerializeXml(feed));
    }

    /// <summary>
    /// Downloads a file
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
        var userId = await GetUser(apiKey);
        if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
            return BadRequest(await _localizationService.Translate(userId, "opds-disabled"));
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(await GetUser(apiKey));
        if (!await _accountService.HasDownloadPermission(user))
        {
            return BadRequest("User does not have download permissions");
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

    private static void AddPagination(Feed feed, PagedList<SeriesDto> list, string href)
    {
        var url = href;
        if (href.Contains('?'))
        {
            url += "&amp;";
        }
        else
        {
            url += "?";
        }

        var pageNumber = Math.Max(list.CurrentPage, 1);

        if (pageNumber > 1)
        {
            feed.Links.Add(CreateLink(FeedLinkRelation.Prev, FeedLinkType.AtomNavigation, url + "pageNumber=" + (pageNumber - 1)));
        }

        if (pageNumber + 1 <= list.TotalPages)
        {
            feed.Links.Add(CreateLink(FeedLinkRelation.Next, FeedLinkType.AtomNavigation, url + "pageNumber=" + (pageNumber + 1)));
        }

        // Update self to point to current page
        var selfLink = feed.Links.SingleOrDefault(l => l.Rel == FeedLinkRelation.Self);
        if (selfLink != null)
        {
            selfLink.Href = url + "pageNumber=" + pageNumber;
        }


        feed.Total = list.TotalCount;
        feed.ItemsPerPage = list.PageSize;
        feed.StartIndex = (Math.Max(list.CurrentPage - 1, 0) * list.PageSize) + 1;
    }

    private static FeedEntry CreateSeries(SeriesDto seriesDto, SeriesMetadataDto metadata, string apiKey, string prefix, string baseUrl)
    {
        return new FeedEntry()
        {
            Id = seriesDto.Id.ToString(),
            Title = $"{seriesDto.Name} ({seriesDto.Format})",
            Summary = seriesDto.Summary,
            Authors = metadata.Writers.Select(p => new FeedAuthor()
            {
                Name = p.Name,
                Uri = "http://opds-spec.org/author/" + p.Id
            }).ToList(),
            Categories = metadata.Genres.Select(g => new FeedCategory()
            {
                Label = g.Title,
                Term = string.Empty
            }).ToList(),
            Links = new List<FeedLink>()
            {
                CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation,  $"{prefix}{apiKey}/series/{seriesDto.Id}"),
                CreateLink(FeedLinkRelation.Image, FeedLinkType.Image, $"{baseUrl}api/image/series-cover?seriesId={seriesDto.Id}&apiKey={apiKey}"),
                CreateLink(FeedLinkRelation.Thumbnail, FeedLinkType.Image, $"{baseUrl}api/image/series-cover?seriesId={seriesDto.Id}&apiKey={apiKey}")
            }
        };
    }

    private static FeedEntry CreateSeries(SearchResultDto searchResultDto, string apiKey, string prefix, string baseUrl)
    {
        return new FeedEntry()
        {
            Id = searchResultDto.SeriesId.ToString(),
            Title = $"{searchResultDto.Name} ({searchResultDto.Format})",
            Links = new List<FeedLink>()
            {
                CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, $"{prefix}{apiKey}/series/{searchResultDto.SeriesId}"),
                CreateLink(FeedLinkRelation.Image, FeedLinkType.Image, $"{baseUrl}api/image/series-cover?seriesId={searchResultDto.SeriesId}&apiKey={apiKey}"),
                CreateLink(FeedLinkRelation.Thumbnail, FeedLinkType.Image, $"{baseUrl}api/image/series-cover?seriesId={searchResultDto.SeriesId}&apiKey={apiKey}")
            }
        };
    }

    private static FeedEntry CreateChapter(string apiKey, string title, string summary, int chapterId, int volumeId, int seriesId, string prefix, string baseUrl)
    {
        return new FeedEntry()
        {
            Id = chapterId.ToString(),
            Title = title,
            Summary = summary ?? string.Empty,
            Links = new List<FeedLink>()
            {
                CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation,
                     $"{prefix}{apiKey}/series/{seriesId}/volume/{volumeId}/chapter/{chapterId}"),
                CreateLink(FeedLinkRelation.Image, FeedLinkType.Image,
                    $"{baseUrl}api/image/chapter-cover?chapterId={chapterId}&apiKey={apiKey}"),
                CreateLink(FeedLinkRelation.Thumbnail, FeedLinkType.Image,
                    $"{baseUrl}api/image/chapter-cover?chapterId={chapterId}&apiKey={apiKey}")
            }
        };
    }

    private async Task<FeedEntry> CreateChapterWithFile(int userId, int seriesId, int volumeId, int chapterId, MangaFile mangaFile, SeriesDto series, ChapterDto chapter, string apiKey, string prefix, string baseUrl)
    {
        var fileSize =
            mangaFile.Bytes > 0 ? DirectoryService.GetHumanReadableBytes(mangaFile.Bytes) :
            DirectoryService.GetHumanReadableBytes(_directoryService.GetTotalSize(new List<string>()
                {mangaFile.FilePath}));
        var fileType = _downloadService.GetContentTypeFromFile(mangaFile.FilePath);
        var filename = Uri.EscapeDataString(Path.GetFileName(mangaFile.FilePath));
        var libraryType = await _unitOfWork.LibraryRepository.GetLibraryTypeAsync(series.LibraryId);
        var volume = await _unitOfWork.VolumeRepository.GetVolumeDtoAsync(volumeId, await GetUser(apiKey));


        var title = $"{series.Name}";

        if (volume!.Chapters.Count == 1)
        {
            var volumeLabel = await _localizationService.Translate(userId, "volume-num", string.Empty);
            SeriesService.RenameVolumeName(volume.Chapters.First(), volume, libraryType, volumeLabel);
            if (volume.Name != "0")
            {
                title += $" - {volume.Name}";
            }
        }
        else if (volume.Number != 0)
        {
            title = $"{series.Name} - Volume {volume.Name} - {await _seriesService.FormatChapterTitle(userId, chapter, libraryType)}";
        }
        else
        {
            title = $"{series.Name} - {await _seriesService.FormatChapterTitle(userId, chapter, libraryType)}";
        }

        // Chunky requires a file at the end. Our API ignores this
        var accLink =
                CreateLink(FeedLinkRelation.Acquisition, fileType,
                    $"{prefix}{apiKey}/series/{seriesId}/volume/{volumeId}/chapter/{chapterId}/download/{filename}",
                    filename);
        accLink.TotalPages = chapter.Pages;

        var entry = new FeedEntry()
        {
            Id = mangaFile.Id.ToString(),
            Title = title,
            Extent = fileSize,
            Summary = $"{fileType.Split("/")[1]} - {fileSize}",
            Format = mangaFile.Format.ToString(),
            Links = new List<FeedLink>()
            {
                CreateLink(FeedLinkRelation.Image, FeedLinkType.Image, $"{baseUrl}api/image/chapter-cover?chapterId={chapterId}&apiKey={apiKey}"),
                CreateLink(FeedLinkRelation.Thumbnail, FeedLinkType.Image, $"{baseUrl}api/image/chapter-cover?chapterId={chapterId}&apiKey={apiKey}"),
                // We can't not include acc link in the feed, panels doesn't work with just page streaming option. We have to block download directly
                accLink,
                await CreatePageStreamLink(series.LibraryId, seriesId, volumeId, chapterId, mangaFile, apiKey, prefix)
            },
            Content = new FeedEntryContent()
            {
                Text = fileType,
                Type = "text"
            }
        };

        return entry;
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
    /// <returns></returns>
    [HttpGet("{apiKey}/image")]
    public async Task<ActionResult> GetPageStreamedImage(string apiKey, [FromQuery] int libraryId, [FromQuery] int seriesId, [FromQuery] int volumeId,[FromQuery] int chapterId, [FromQuery] int pageNumber)
    {
        var userId = await GetUser(apiKey);
        if (pageNumber < 0) return BadRequest(await _localizationService.Translate(userId, "greater-0", "Page"));
        var chapter = await _cacheService.Ensure(chapterId);
        if (chapter == null) return BadRequest(await _localizationService.Translate(userId, "cache-file-find"));

        try
        {
            var path = _cacheService.GetCachedPagePath(chapter.Id, pageNumber);
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
                return BadRequest(await _localizationService.Translate(userId, "no-image-for-page", pageNumber));

            var content = await _directoryService.ReadFileAsync(path);
            var format = Path.GetExtension(path);

            // Calculates SHA1 Hash for byte[]
            Response.AddCacheHeader(content);

            // Save progress for the user
            await _readerService.SaveReadingProgress(new ProgressDto()
            {
                ChapterId = chapterId,
                PageNum = pageNumber,
                SeriesId = seriesId,
                VolumeId = volumeId,
                LibraryId =libraryId
            }, await GetUser(apiKey));

            return File(content, MimeTypeMap.GetMimeType(format));
        }
        catch (Exception)
        {
            _cacheService.CleanupChapters(new []{ chapterId });
            throw;
        }
    }

    [HttpGet("{apiKey}/favicon")]
    [ResponseCache(Duration = 60 * 60, Location = ResponseCacheLocation.Client, NoStore = false)]
    public async Task<ActionResult> GetFavicon(string apiKey)
    {
        var userId = await GetUser(apiKey);
        var files = _directoryService.GetFilesWithExtension(Path.Join(Directory.GetCurrentDirectory(), ".."), @"\.ico");
        if (files.Length == 0) return BadRequest(await _localizationService.Translate(userId, "favicon-doesnt-exist"));
        var path = files[0];
        var content = await _directoryService.ReadFileAsync(path);
        var format = Path.GetExtension(path);

        return File(content, MimeTypeMap.GetMimeType(format));
    }

    /// <summary>
    /// Gets the user from the API key
    /// </summary>
    /// <returns></returns>
    private async Task<int> GetUser(string apiKey)
    {
        try
        {
            var user = await _unitOfWork.UserRepository.GetUserIdByApiKeyAsync(apiKey);
            return user;
        }
        catch
        {
            /* Do nothing */
        }
        throw new KavitaException(await _localizationService.Get("en", "user-doesnt-exist"));
    }

    private async Task<FeedLink> CreatePageStreamLink(int libraryId, int seriesId, int volumeId, int chapterId, MangaFile mangaFile, string apiKey, string prefix)
    {
        var userId = await GetUser(apiKey);
        var progress = await _unitOfWork.AppUserProgressRepository.GetUserProgressDtoAsync(chapterId, userId);

        // TODO: Type could be wrong
        var link = CreateLink(FeedLinkRelation.Stream, "image/jpeg",
            $"{prefix}{apiKey}/image?libraryId={libraryId}&seriesId={seriesId}&volumeId={volumeId}&chapterId={chapterId}&pageNumber=" + "{pageNumber}");
        link.TotalPages = mangaFile.Pages;
        if (progress != null)
        {
            link.LastRead = progress.PageNum;
            link.LastReadDate = progress.LastModifiedUtc;
        }
        link.IsPageStream = true;
        return link;
    }

    private static FeedLink CreateLink(string rel, string type, string href, string? title = null)
    {
        return new FeedLink()
        {
            Rel = rel,
            Href = href,
            Type = type,
            Title = string.IsNullOrEmpty(title) ? string.Empty : title
        };
    }

    private static Feed CreateFeed(string title, string href, string apiKey, string prefix)
    {
        var link = CreateLink(FeedLinkRelation.Self, string.IsNullOrEmpty(href) ?
            FeedLinkType.AtomNavigation :
            FeedLinkType.AtomAcquisition, prefix + href);

        return new Feed()
        {
            Title = title,
            Icon = $"{prefix}{apiKey}/favicon",
            Links = new List<FeedLink>()
            {
                link,
                CreateLink(FeedLinkRelation.Start, FeedLinkType.AtomNavigation, $"{prefix}{apiKey}"),
                CreateLink(FeedLinkRelation.Search, FeedLinkType.AtomSearch, $"{prefix}{apiKey}/search")
            },
        };
    }

    private string SerializeXml(Feed feed)
    {
        if (feed == null) return string.Empty;
        using var sm = new StringWriter();
        _xmlSerializer.Serialize(sm, feed);
        return sm.ToString().Replace("utf-16", "utf-8"); // Chunky cannot accept UTF-16 feeds
    }
}
