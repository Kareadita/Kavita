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

namespace API.Controllers;

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


    private readonly XmlSerializer _xmlSerializer;
    private readonly XmlSerializer _xmlOpenSearchSerializer;
    private const string Prefix = "/api/opds/";
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
    private readonly ChapterSortComparer _chapterSortComparer = new ChapterSortComparer();

    public OpdsController(IUnitOfWork unitOfWork, IDownloadService downloadService,
        IDirectoryService directoryService, ICacheService cacheService,
        IReaderService readerService, ISeriesService seriesService,
        IAccountService accountService)
    {
        _unitOfWork = unitOfWork;
        _downloadService = downloadService;
        _directoryService = directoryService;
        _cacheService = cacheService;
        _readerService = readerService;
        _seriesService = seriesService;
        _accountService = accountService;

        _xmlSerializer = new XmlSerializer(typeof(Feed));
        _xmlOpenSearchSerializer = new XmlSerializer(typeof(OpenSearchDescription));

    }

    [HttpPost("{apiKey}")]
    [HttpGet("{apiKey}")]
    [Produces("application/xml")]
    public async Task<IActionResult> Get(string apiKey)
    {
        if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
            return BadRequest("OPDS is not enabled on this server");
        var feed = CreateFeed("Kavita", string.Empty, apiKey);
        SetFeedId(feed, "root");
        feed.Entries.Add(new FeedEntry()
        {
            Id = "onDeck",
            Title = "On Deck",
            Content = new FeedEntryContent()
            {
                Text = "Browse by On Deck"
            },
            Links = new List<FeedLink>()
            {
                CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, Prefix + $"{apiKey}/on-deck"),
            }
        });
        feed.Entries.Add(new FeedEntry()
        {
            Id = "recentlyAdded",
            Title = "Recently Added",
            Content = new FeedEntryContent()
            {
                Text = "Browse by Recently Added"
            },
            Links = new List<FeedLink>()
            {
                CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, Prefix + $"{apiKey}/recently-added"),
            }
        });
        feed.Entries.Add(new FeedEntry()
        {
            Id = "readingList",
            Title = "Reading Lists",
            Content = new FeedEntryContent()
            {
                Text = "Browse by Reading Lists"
            },
            Links = new List<FeedLink>()
            {
                CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, Prefix + $"{apiKey}/reading-list"),
            }
        });
        feed.Entries.Add(new FeedEntry()
        {
            Id = "allLibraries",
            Title = "All Libraries",
            Content = new FeedEntryContent()
            {
                Text = "Browse by Libraries"
            },
            Links = new List<FeedLink>()
            {
                CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, Prefix + $"{apiKey}/libraries"),
            }
        });
        feed.Entries.Add(new FeedEntry()
        {
            Id = "allCollections",
            Title = "All Collections",
            Content = new FeedEntryContent()
            {
                Text = "Browse by Collections"
            },
            Links = new List<FeedLink>()
            {
                CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, Prefix + $"{apiKey}/collections"),
            }
        });
        return CreateXmlResult(SerializeXml(feed));
    }


    [HttpGet("{apiKey}/libraries")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetLibraries(string apiKey)
    {
        if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
            return BadRequest("OPDS is not enabled on this server");
        var userId = await GetUser(apiKey);
        var libraries = await _unitOfWork.LibraryRepository.GetLibrariesForUserIdAsync(userId);
        var feed = CreateFeed("All Libraries", $"{apiKey}/libraries", apiKey);
        SetFeedId(feed, "libraries");
        foreach (var library in libraries)
        {
            feed.Entries.Add(new FeedEntry()
            {
                Id = library.Id.ToString(),
                Title = library.Name,
                Links = new List<FeedLink>()
                {
                    CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, Prefix + $"{apiKey}/libraries/{library.Id}"),
                }
            });
        }

        return CreateXmlResult(SerializeXml(feed));
    }

    [HttpGet("{apiKey}/collections")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetCollections(string apiKey)
    {
        if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
            return BadRequest("OPDS is not enabled on this server");
        var userId = await GetUser(apiKey);
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId);
        var isAdmin = await _unitOfWork.UserRepository.IsUserAdminAsync(user);

        IEnumerable<CollectionTagDto> tags = isAdmin ? (await _unitOfWork.CollectionTagRepository.GetAllTagDtosAsync())
            : (await _unitOfWork.CollectionTagRepository.GetAllPromotedTagDtosAsync(userId));


        var feed = CreateFeed("All Collections", $"{apiKey}/collections", apiKey);
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
                    CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, Prefix + $"{apiKey}/collections/{tag.Id}"),
                    CreateLink(FeedLinkRelation.Image, FeedLinkType.Image, $"/api/image/collection-cover?collectionId={tag.Id}"),
                    CreateLink(FeedLinkRelation.Thumbnail, FeedLinkType.Image, $"/api/image/collection-cover?collectionId={tag.Id}")
                }
            });
        }

        return CreateXmlResult(SerializeXml(feed));
    }


    [HttpGet("{apiKey}/collections/{collectionId}")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetCollection(int collectionId, string apiKey, [FromQuery] int pageNumber = 0)
    {
        if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
            return BadRequest("OPDS is not enabled on this server");
        var userId = await GetUser(apiKey);
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId);
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

        var series = await _unitOfWork.SeriesRepository.GetSeriesDtoForCollectionAsync(collectionId, userId, new UserParams()
        {
            PageNumber = pageNumber,
            PageSize = 20
        });

        var feed = CreateFeed(tag.Title + " Collection", $"{apiKey}/collections/{collectionId}", apiKey);
        SetFeedId(feed, $"collections-{collectionId}");
        AddPagination(feed, series, $"{Prefix}{apiKey}/collections/{collectionId}");

        foreach (var seriesDto in series)
        {
            feed.Entries.Add(CreateSeries(seriesDto, apiKey));
        }


        return CreateXmlResult(SerializeXml(feed));
    }

    [HttpGet("{apiKey}/reading-list")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetReadingLists(string apiKey, [FromQuery] int pageNumber = 0)
    {
        if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
            return BadRequest("OPDS is not enabled on this server");
        var userId = await GetUser(apiKey);

        var readingLists = await _unitOfWork.ReadingListRepository.GetReadingListDtosForUserAsync(userId, true, new UserParams()
        {
            PageNumber = pageNumber
        });


        var feed = CreateFeed("All Reading Lists", $"{apiKey}/reading-list", apiKey);
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
                    CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, Prefix + $"{apiKey}/reading-list/{readingListDto.Id}"),
                }
            });
        }

        return CreateXmlResult(SerializeXml(feed));
    }

    [HttpGet("{apiKey}/reading-list/{readingListId}")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetReadingListItems(int readingListId, string apiKey)
    {
        if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
            return BadRequest("OPDS is not enabled on this server");
        var userId = await GetUser(apiKey);
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId);

        var userWithLists = await _unitOfWork.UserRepository.GetUserByUsernameAsync(user.UserName, AppUserIncludes.ReadingListsWithItems);
        var readingList = userWithLists.ReadingLists.SingleOrDefault(t => t.Id == readingListId);
        if (readingList == null)
        {
            return BadRequest("Reading list does not exist or you don't have access");
        }

        var feed = CreateFeed(readingList.Title + " Reading List", $"{apiKey}/reading-list/{readingListId}", apiKey);
        SetFeedId(feed, $"reading-list-{readingListId}");

        var items = (await _unitOfWork.ReadingListRepository.GetReadingListItemDtosByIdAsync(readingListId, userId)).ToList();
        foreach (var item in items)
        {
            feed.Entries.Add(CreateChapter(apiKey, $"{item.SeriesName} Chapter {item.ChapterNumber}", item.ChapterId, item.VolumeId, item.SeriesId));
        }
        return CreateXmlResult(SerializeXml(feed));
    }

    [HttpGet("{apiKey}/libraries/{libraryId}")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetSeriesForLibrary(int libraryId, string apiKey, [FromQuery] int pageNumber = 0)
    {
        if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
            return BadRequest("OPDS is not enabled on this server");
        var userId = await GetUser(apiKey);
        var library =
            (await _unitOfWork.LibraryRepository.GetLibrariesForUserIdAsync(userId)).SingleOrDefault(l =>
                l.Id == libraryId);
        if (library == null)
        {
            return BadRequest("User does not have access to this library");
        }

        var series = await _unitOfWork.SeriesRepository.GetSeriesDtoForLibraryIdAsync(libraryId, userId, new UserParams()
        {
            PageNumber = pageNumber,
            PageSize = 20
        }, _filterDto);

        var feed = CreateFeed(library.Name, $"{apiKey}/libraries/{libraryId}", apiKey);
        SetFeedId(feed, $"library-{library.Name}");
        AddPagination(feed, series, $"{Prefix}{apiKey}/libraries/{libraryId}");

        foreach (var seriesDto in series)
        {
            feed.Entries.Add(CreateSeries(seriesDto, apiKey));
        }

        return CreateXmlResult(SerializeXml(feed));
    }

    [HttpGet("{apiKey}/recently-added")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetRecentlyAdded(string apiKey, [FromQuery] int pageNumber = 1)
    {
        if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
            return BadRequest("OPDS is not enabled on this server");
        var userId = await GetUser(apiKey);
        var recentlyAdded = await _unitOfWork.SeriesRepository.GetRecentlyAdded(0, userId, new UserParams()
        {
            PageNumber = pageNumber,
            PageSize = 20
        }, _filterDto);

        var feed = CreateFeed("Recently Added", $"{apiKey}/recently-added", apiKey);
        SetFeedId(feed, "recently-added");
        AddPagination(feed, recentlyAdded, $"{Prefix}{apiKey}/recently-added");

        foreach (var seriesDto in recentlyAdded)
        {
            feed.Entries.Add(CreateSeries(seriesDto, apiKey));
        }

        return CreateXmlResult(SerializeXml(feed));
    }

    [HttpGet("{apiKey}/on-deck")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetOnDeck(string apiKey, [FromQuery] int pageNumber = 1)
    {
        if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
            return BadRequest("OPDS is not enabled on this server");
        var userId = await GetUser(apiKey);
        var userParams = new UserParams()
        {
            PageNumber = pageNumber,
        };
        var pagedList = await _unitOfWork.SeriesRepository.GetOnDeck(userId, 0, userParams, _filterDto);

        Response.AddPaginationHeader(pagedList.CurrentPage, pagedList.PageSize, pagedList.TotalCount, pagedList.TotalPages);

        var feed = CreateFeed("On Deck", $"{apiKey}/on-deck", apiKey);
        SetFeedId(feed, "on-deck");
        AddPagination(feed, pagedList, $"{Prefix}{apiKey}/on-deck");

        foreach (var seriesDto in pagedList)
        {
            feed.Entries.Add(CreateSeries(seriesDto, apiKey));
        }

        return CreateXmlResult(SerializeXml(feed));
    }

    [HttpGet("{apiKey}/series")]
    [Produces("application/xml")]
    public async Task<IActionResult> SearchSeries(string apiKey, [FromQuery] string query)
    {
        if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
            return BadRequest("OPDS is not enabled on this server");
        var userId = await GetUser(apiKey);
        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId);

        if (string.IsNullOrEmpty(query))
        {
            return BadRequest("You must pass a query parameter");
        }
        query = query.Replace(@"%", "");
        // Get libraries user has access to
        var libraries = (await _unitOfWork.LibraryRepository.GetLibrariesForUserIdAsync(userId)).ToList();

        if (!libraries.Any()) return BadRequest("User does not have access to any libraries");

        var isAdmin = await _unitOfWork.UserRepository.IsUserAdminAsync(user);

        var series = await _unitOfWork.SeriesRepository.SearchSeries(userId, isAdmin, libraries.Select(l => l.Id).ToArray(), query);

        var feed = CreateFeed(query, $"{apiKey}/series?query=" + query, apiKey);
        SetFeedId(feed, "search-series");
        foreach (var seriesDto in series.Series)
        {
            feed.Entries.Add(CreateSeries(seriesDto, apiKey));
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
                        Prefix + $"{apiKey}/collections/{collection.Id}"),
                    CreateLink(FeedLinkRelation.Image, FeedLinkType.Image,
                        $"/api/image/collection-cover?collectionId={collection.Id}"),
                    CreateLink(FeedLinkRelation.Thumbnail, FeedLinkType.Image,
                        $"/api/image/collection-cover?collectionId={collection.Id}")
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
                    CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, Prefix + $"{apiKey}/reading-list/{readingListDto.Id}"),
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
        if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
            return BadRequest("OPDS is not enabled on this server");
        var feed = new OpenSearchDescription()
        {
            ShortName = "Search",
            Description = "Search for Series, Collections, or Reading Lists",
            Url = new SearchLink()
            {
                Type = FeedLinkType.AtomAcquisition,
                Template = $"{Prefix}{apiKey}/series?query=" + "{searchTerms}"
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
        if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
            return BadRequest("OPDS is not enabled on this server");
        var userId = await GetUser(apiKey);
        var series = await _unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(seriesId, userId);

        var feed = CreateFeed(series.Name + " - Storyline", $"{apiKey}/series/{series.Id}", apiKey);
        SetFeedId(feed, $"series-{series.Id}");
        feed.Links.Add(CreateLink(FeedLinkRelation.Image, FeedLinkType.Image, $"/api/image/series-cover?seriesId={seriesId}"));

        var seriesDetail =  await _seriesService.GetSeriesDetail(seriesId, userId);
        foreach (var volume in seriesDetail.Volumes)
        {
            // If there is only one chapter to the Volume, we will emulate a volume to flatten the amount of hops a user must go through
            if (volume.Chapters.Count == 1)
            {
                var firstChapter = volume.Chapters.First();
                var chapter = CreateChapter(apiKey, volume.Name, firstChapter.Id, volume.Id, seriesId);
                chapter.Id = firstChapter.Id.ToString();
                feed.Entries.Add(chapter);
            }
            else
            {
                feed.Entries.Add(CreateVolume(volume, seriesId, apiKey));
            }

        }

        foreach (var storylineChapter in seriesDetail.StorylineChapters.Where(c => !c.IsSpecial))
        {
            feed.Entries.Add(CreateChapter(apiKey, storylineChapter.Title, storylineChapter.Id, storylineChapter.VolumeId, seriesId));
        }

        foreach (var special in seriesDetail.Specials)
        {
            feed.Entries.Add(CreateChapter(apiKey, special.Title, special.Id, special.VolumeId, seriesId));
        }

        return CreateXmlResult(SerializeXml(feed));
    }

    [HttpGet("{apiKey}/series/{seriesId}/volume/{volumeId}")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetVolume(string apiKey, int seriesId, int volumeId)
    {
        if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
            return BadRequest("OPDS is not enabled on this server");
        var userId = await GetUser(apiKey);
        var series = await _unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(seriesId, userId);
        var libraryType = await _unitOfWork.LibraryRepository.GetLibraryTypeAsync(series.LibraryId);
        var volume = await _unitOfWork.VolumeRepository.GetVolumeAsync(volumeId);
        var chapters =
            (await _unitOfWork.ChapterRepository.GetChaptersAsync(volumeId)).OrderBy(x => double.Parse(x.Number),
                _chapterSortComparer);

        var feed = CreateFeed(series.Name + " - Volume " + volume.Name + $" - {SeriesService.FormatChapterName(libraryType)}s ", $"{apiKey}/series/{seriesId}/volume/{volumeId}", apiKey);
        SetFeedId(feed, $"series-{series.Id}-volume-{volume.Id}-{SeriesService.FormatChapterName(libraryType)}s");
        foreach (var chapter in chapters)
        {
            feed.Entries.Add(new FeedEntry()
            {
                Id = chapter.Id.ToString(),
                Title = SeriesService.FormatChapterTitle(chapter, libraryType),
                Links = new List<FeedLink>()
                {
                    CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, Prefix + $"{apiKey}/series/{seriesId}/volume/{volumeId}/chapter/{chapter.Id}"),
                    CreateLink(FeedLinkRelation.Image, FeedLinkType.Image, $"/api/image/chapter-cover?chapterId={chapter.Id}")
                }
            });
        }

        return CreateXmlResult(SerializeXml(feed));
    }

    [HttpGet("{apiKey}/series/{seriesId}/volume/{volumeId}/chapter/{chapterId}")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetChapter(string apiKey, int seriesId, int volumeId, int chapterId)
    {
        if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
            return BadRequest("OPDS is not enabled on this server");
        var userId = await GetUser(apiKey);
        var series = await _unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(seriesId, userId);
        var libraryType = await _unitOfWork.LibraryRepository.GetLibraryTypeAsync(series.LibraryId);
        var volume = await _unitOfWork.VolumeRepository.GetVolumeAsync(volumeId);
        var chapter = await _unitOfWork.ChapterRepository.GetChapterDtoAsync(chapterId);
        var files = await _unitOfWork.ChapterRepository.GetFilesForChapterAsync(chapterId);

        var feed = CreateFeed(series.Name + " - Volume " + volume.Name + $" - {SeriesService.FormatChapterName(libraryType)}s", $"{apiKey}/series/{seriesId}/volume/{volumeId}/chapter/{chapterId}", apiKey);
        SetFeedId(feed, $"series-{series.Id}-volume-{volumeId}-{SeriesService.FormatChapterName(libraryType)}-{chapterId}-files");
        foreach (var mangaFile in files)
        {
            feed.Entries.Add(await CreateChapterWithFile(seriesId, volumeId, chapterId, mangaFile, series, chapter, apiKey));
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
        if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
            return BadRequest("OPDS is not enabled on this server");
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

    private static FeedEntry CreateSeries(SeriesDto seriesDto, string apiKey)
    {
        return new FeedEntry()
        {
            Id = seriesDto.Id.ToString(),
            Title = $"{seriesDto.Name} ({seriesDto.Format})",
            Summary = seriesDto.Summary,
            Links = new List<FeedLink>()
            {
                CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, Prefix + $"{apiKey}/series/{seriesDto.Id}"),
                CreateLink(FeedLinkRelation.Image, FeedLinkType.Image, $"/api/image/series-cover?seriesId={seriesDto.Id}"),
                CreateLink(FeedLinkRelation.Thumbnail, FeedLinkType.Image, $"/api/image/series-cover?seriesId={seriesDto.Id}")
            }
        };
    }

    private static FeedEntry CreateSeries(SearchResultDto searchResultDto, string apiKey)
    {
        return new FeedEntry()
        {
            Id = searchResultDto.SeriesId.ToString(),
            Title = $"{searchResultDto.Name} ({searchResultDto.Format})",
            Links = new List<FeedLink>()
            {
                CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, Prefix + $"{apiKey}/series/{searchResultDto.SeriesId}"),
                CreateLink(FeedLinkRelation.Image, FeedLinkType.Image, $"/api/image/series-cover?seriesId={searchResultDto.SeriesId}"),
                CreateLink(FeedLinkRelation.Thumbnail, FeedLinkType.Image, $"/api/image/series-cover?seriesId={searchResultDto.SeriesId}")
            }
        };
    }

    private static FeedEntry CreateVolume(VolumeDto volumeDto, int seriesId, string apiKey)
    {
        return new FeedEntry()
        {
            Id = volumeDto.Id.ToString(),
            Title = volumeDto.Name,
            Links = new List<FeedLink>()
            {
                CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation,
                    Prefix + $"{apiKey}/series/{seriesId}/volume/{volumeDto.Id}"),
                CreateLink(FeedLinkRelation.Image, FeedLinkType.Image,
                    $"/api/image/volume-cover?volumeId={volumeDto.Id}"),
                CreateLink(FeedLinkRelation.Thumbnail, FeedLinkType.Image,
                    $"/api/image/volume-cover?volumeId={volumeDto.Id}")
            }
        };
    }

    private static FeedEntry CreateChapter(string apiKey, string title, int chapterId, int volumeId, int seriesId)
    {
        return new FeedEntry()
        {
            Id = chapterId.ToString(),
            Title = title,
            Links = new List<FeedLink>()
            {
                CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation,
                    Prefix + $"{apiKey}/series/{seriesId}/volume/{volumeId}/chapter/{chapterId}"),
                CreateLink(FeedLinkRelation.Image, FeedLinkType.Image,
                    $"/api/image/chapter-cover?chapterId={chapterId}"),
                CreateLink(FeedLinkRelation.Thumbnail, FeedLinkType.Image,
                    $"/api/image/chapter-cover?chapterId={chapterId}")
            }
        };
    }

    private async Task<FeedEntry> CreateChapterWithFile(int seriesId, int volumeId, int chapterId, MangaFile mangaFile, SeriesDto series, ChapterDto chapter, string apiKey)
    {
        var fileSize =
            DirectoryService.GetHumanReadableBytes(_directoryService.GetTotalSize(new List<string>()
                {mangaFile.FilePath}));
        var fileType = _downloadService.GetContentTypeFromFile(mangaFile.FilePath);
        var filename = Uri.EscapeDataString(Path.GetFileName(mangaFile.FilePath) ?? string.Empty);
        var libraryType = await _unitOfWork.LibraryRepository.GetLibraryTypeAsync(series.LibraryId);
        var volume = await _unitOfWork.VolumeRepository.GetVolumeDtoAsync(volumeId, await GetUser(apiKey));

        var title = $"{series.Name} - ";
        if (volume.Chapters.Count == 1)
        {
            SeriesService.RenameVolumeName(volume.Chapters.First(), volume, libraryType);
            title += $"{volume.Name}";
        }
        else
        {
            title = $"{series.Name} - {SeriesService.FormatChapterTitle(chapter, libraryType)}";
        }

        // Chunky requires a file at the end. Our API ignores this
        var accLink =
                CreateLink(FeedLinkRelation.Acquisition, fileType,
                    $"{Prefix}{apiKey}/series/{seriesId}/volume/{volumeId}/chapter/{chapterId}/download/{filename}",
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
                CreateLink(FeedLinkRelation.Image, FeedLinkType.Image, $"/api/image/chapter-cover?chapterId={chapterId}"),
                CreateLink(FeedLinkRelation.Thumbnail, FeedLinkType.Image, $"/api/image/chapter-cover?chapterId={chapterId}"),
                // We can't not include acc link in the feed, panels doesn't work with just page streaming option. We have to block download directly
                accLink,
                CreatePageStreamLink(seriesId, volumeId, chapterId, mangaFile, apiKey)
            },
            Content = new FeedEntryContent()
            {
                Text = fileType,
                Type = "text"
            }
        };

        return entry;
    }

    [HttpGet("{apiKey}/image")]
    public async Task<ActionResult> GetPageStreamedImage(string apiKey, [FromQuery] int seriesId, [FromQuery] int volumeId,[FromQuery] int chapterId, [FromQuery] int pageNumber)
    {
        if (pageNumber < 0) return BadRequest("Page cannot be less than 0");
        var chapter = await _cacheService.Ensure(chapterId);
        if (chapter == null) return BadRequest("There was an issue finding image file for reading");

        try
        {
            var path = _cacheService.GetCachedPagePath(chapter, pageNumber);
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return BadRequest($"No such image for page {pageNumber}");

            var content = await _directoryService.ReadFileAsync(path);
            var format = Path.GetExtension(path).Replace(".", "");

            // Calculates SHA1 Hash for byte[]
            Response.AddCacheHeader(content);

            // Save progress for the user
            await _readerService.SaveReadingProgress(new ProgressDto()
            {
                ChapterId = chapterId,
                PageNum = pageNumber,
                SeriesId = seriesId,
                VolumeId = volumeId
            }, await GetUser(apiKey));

            return File(content, "image/" + format);
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
        var files = _directoryService.GetFilesWithExtension(Path.Join(Directory.GetCurrentDirectory(), ".."), @"\.ico");
        if (files.Length == 0) return BadRequest("Cannot find icon");
        var path = files[0];
        var content = await _directoryService.ReadFileAsync(path);
        var format = Path.GetExtension(path).Replace(".", "");

        return File(content, "image/" + format);
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
        throw new KavitaException("User does not exist");
    }

    private static FeedLink CreatePageStreamLink(int seriesId, int volumeId, int chapterId, MangaFile mangaFile, string apiKey)
    {
        var link = CreateLink(FeedLinkRelation.Stream, "image/jpeg", $"{Prefix}{apiKey}/image?seriesId={seriesId}&volumeId={volumeId}&chapterId={chapterId}&pageNumber=" + "{pageNumber}");
        link.TotalPages = mangaFile.Pages;
        return link;
    }

    private static FeedLink CreateLink(string rel, string type, string href, string title = null)
    {
        return new FeedLink()
        {
            Rel = rel,
            Href = href,
            Type = type,
            Title = string.IsNullOrEmpty(title) ? string.Empty : title
        };
    }

    private static Feed CreateFeed(string title, string href, string apiKey)
    {
        var link = CreateLink(FeedLinkRelation.Self, string.IsNullOrEmpty(href) ?
            FeedLinkType.AtomNavigation :
            FeedLinkType.AtomAcquisition, Prefix + href);

        return new Feed()
        {
            Title = title,
            Icon = Prefix + $"{apiKey}/favicon",
            Links = new List<FeedLink>()
            {
                link,
                CreateLink(FeedLinkRelation.Start, FeedLinkType.AtomNavigation, Prefix + apiKey),
                CreateLink(FeedLinkRelation.Search, FeedLinkType.AtomSearch, Prefix + $"{apiKey}/search")
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
