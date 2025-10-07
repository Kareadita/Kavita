using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.DTOs.Filtering;
using API.DTOs.Filtering.v2;
using API.DTOs.OPDS;
using API.DTOs.OPDS.Requests;
using API.DTOs.Person;
using API.DTOs.ReadingLists;
using API.DTOs.Search;
using API.Entities;
using API.Entities.Enums;
using API.Exceptions;
using API.Helpers;
using AutoMapper;

namespace API.Services;
#nullable enable

public interface IOpdsService
{
    Task<Feed> GetCatalogue(OpdsCatalogueRequest request);
    Task<Feed> GetSmartFilters(OpdsPaginatedCatalogueRequest request);
    Task<Feed> GetLibraries(OpdsPaginatedCatalogueRequest request);
    Task<Feed> GetWantToRead(OpdsPaginatedCatalogueRequest request);
    Task<Feed> GetCollections(OpdsPaginatedCatalogueRequest request);
    Task<Feed> GetReadingLists(OpdsPaginatedCatalogueRequest request);
    Task<Feed> GetRecentlyAdded(OpdsPaginatedCatalogueRequest request);
    Task<Feed> GetRecentlyUpdated(OpdsPaginatedCatalogueRequest request);
    Task<Feed> GetOnDeck(OpdsPaginatedCatalogueRequest request);

    Task<Feed> GetMoreInGenre(OpdsItemsFromEntityIdRequest request);
    Task<Feed> GetSeriesFromSmartFilter(OpdsItemsFromEntityIdRequest request);
    Task<Feed> GetSeriesFromCollection(OpdsItemsFromEntityIdRequest request);
    Task<Feed> GetSeriesFromLibrary(OpdsItemsFromEntityIdRequest request);
    Task<Feed> GetReadingListItems(OpdsItemsFromEntityIdRequest request);
    Task<Feed> GetSeriesDetail(OpdsItemsFromEntityIdRequest request);
    Task<Feed> GetItemsFromVolume(OpdsItemsFromCompoundEntityIdsRequest request);
    Task<Feed> GetItemsFromChapter(OpdsItemsFromCompoundEntityIdsRequest request);

    Task<Feed> Search(OpdsSearchRequest request);

    string SerializeXml(Feed? feed);
}

public class OpdsService : IOpdsService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalizationService _localizationService;
    private readonly ISeriesService _seriesService;
    private readonly IDownloadService _downloadService;
    private readonly IDirectoryService _directoryService;
    private readonly IReaderService _readerService;
    private readonly IMapper _mapper;

    private readonly XmlSerializer _xmlSerializer;

    public const int PageSize = 20;
    public const int FirstPageNumber = 1;
    public const string DefaultApiPrefix = "/api/opds/";

    public const string NoReadingProgressIcon = "⭘";
    public const string QuarterReadingProgressIcon = "◔";
    public const string HalfReadingProgressIcon = "◑";
    public const string AboveHalfReadingProgressIcon = "◕";
    public const string FullReadingProgressIcon = "⬤";

    private readonly FilterV2Dto _filterV2Dto = new();
    private readonly FilterDto _filterDto = new()
    {
        Formats = [],
        Character = [],
        Colorist = [],
        Editor = [],
        Genres = [],
        Inker = [],
        Languages = [],
        Letterer = [],
        Penciller = [],
        Libraries = [],
        Publisher = [],
        Rating = 0,
        Tags = [],
        Translators = [],
        Writers = [],
        AgeRating = [],
        CollectionTags = [],
        CoverArtist = [],
        ReadStatus = new ReadStatus(),
        SortOptions = null,
        PublicationStatus = []
    };

    public OpdsService(IUnitOfWork unitOfWork, ILocalizationService localizationService, ISeriesService seriesService,
        IDownloadService downloadService, IDirectoryService directoryService, IReaderService readerService, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _localizationService = localizationService;
        _seriesService = seriesService;
        _downloadService = downloadService;
        _directoryService = directoryService;
        _readerService = readerService;
        _mapper = mapper;

        _xmlSerializer = new XmlSerializer(typeof(Feed));
    }

    public async Task<Feed> GetCatalogue(OpdsCatalogueRequest request)
    {
        var feed = CreateFeed("Kavita", string.Empty, request.ApiKey, request.Prefix);
        SetFeedId(feed, "root");

        // Get the user's customized dashboard
        var streams = await _unitOfWork.UserRepository.GetDashboardStreams(request.UserId, true);
        foreach (var stream in streams)
        {
            switch (stream.StreamType)
            {
                case DashboardStreamType.OnDeck:
                    feed.Entries.Add(new FeedEntry()
                    {
                        Id = "onDeck",
                        Title = await _localizationService.Translate(request.UserId, "on-deck"),
                        Content = new FeedEntryContent()
                        {
                            Text = await _localizationService.Translate(request.UserId, "browse-on-deck")
                        },
                        Links =
                        [
                            CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, $"{request.Prefix}{request.ApiKey}/on-deck"),
                        ]
                    });
                    break;
                case DashboardStreamType.NewlyAdded:
                    feed.Entries.Add(new FeedEntry()
                    {
                        Id = "recentlyAdded",
                        Title = await _localizationService.Translate(request.UserId, "recently-added"),
                        Content = new FeedEntryContent()
                        {
                            Text = await _localizationService.Translate(request.UserId, "browse-recently-added")
                        },
                        Links =
                        [
                            CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, $"{request.Prefix}{request.ApiKey}/recently-added"),
                        ]
                    });
                    break;
                case DashboardStreamType.RecentlyUpdated:
                    feed.Entries.Add(new FeedEntry()
                    {
                        Id = "recentlyUpdated",
                        Title = await _localizationService.Translate(request.UserId, "recently-updated"),
                        Content = new FeedEntryContent()
                        {
                            Text = await _localizationService.Translate(request.UserId, "browse-recently-updated")
                        },
                        Links =
                        [
                            CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, $"{request.Prefix}{request.ApiKey}/recently-updated"),
                        ]
                    });
                    break;
                case DashboardStreamType.MoreInGenre:
                    var randomGenre = await _unitOfWork.GenreRepository.GetRandomGenre();
                    if (randomGenre == null) break;

                    feed.Entries.Add(new FeedEntry()
                    {
                        Id = "moreInGenre",
                        Title = await _localizationService.Translate(request.UserId, "more-in-genre", randomGenre.Title),
                        Content = new FeedEntryContent()
                        {
                            Text = await _localizationService.Translate(request.UserId, "browse-more-in-genre", randomGenre.Title)
                        },
                        Links =
                        [
                            CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, $"{request.Prefix}{request.ApiKey}/more-in-genre?genreId={randomGenre.Id}"),
                        ]
                    });
                    break;
                case DashboardStreamType.SmartFilter:

                    feed.Entries.Add(new FeedEntry()
                    {
                        Id = "smartFilter-" + stream.Id,
                        Title = stream.Name,
                        Content = new FeedEntryContent()
                        {
                            Text = stream.Name
                        },
                        Links =
                        [
                            CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation,
                                $"{request.Prefix}{request.ApiKey}/smart-filters/{stream.SmartFilterId}/")
                        ]
                    });
                    break;
            }
        }

        feed.Entries.Add(new FeedEntry()
        {
            Id = "readingList",
            Title = await _localizationService.Translate(request.UserId, "reading-lists"),
            Content = new FeedEntryContent()
            {
                Text = await _localizationService.Translate(request.UserId, "browse-reading-lists")
            },
            Links =
            [
                CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, $"{request.Prefix}{request.ApiKey}/reading-list"),
            ]
        });
        feed.Entries.Add(new FeedEntry()
        {
            Id = "wantToRead",
            Title = await _localizationService.Translate(request.UserId, "want-to-read"),
            Content = new FeedEntryContent()
            {
                Text = await _localizationService.Translate(request.UserId, "browse-want-to-read")
            },
            Links =
            [
                CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, $"{request.Prefix}{request.ApiKey}/want-to-read"),
            ]
        });
        feed.Entries.Add(new FeedEntry()
        {
            Id = "allLibraries",
            Title = await _localizationService.Translate(request.UserId, "libraries"),
            Content = new FeedEntryContent()
            {
                Text = await _localizationService.Translate(request.UserId, "browse-libraries")
            },
            Links =
            [
                CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, $"{request.Prefix}{request.ApiKey}/libraries"),
            ]
        });
        feed.Entries.Add(new FeedEntry()
        {
            Id = "allCollections",
            Title = await _localizationService.Translate(request.UserId, "collections"),
            Content = new FeedEntryContent()
            {
                Text = await _localizationService.Translate(request.UserId, "browse-collections")
            },
            Links =
            [
                CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, $"{request.Prefix}{request.ApiKey}/collections"),
            ]
        });

        if ((_unitOfWork.AppUserSmartFilterRepository.GetAllDtosByUserId(request.UserId)).Any())
        {
            feed.Entries.Add(new FeedEntry()
            {
                Id = "allSmartFilters",
                Title = await _localizationService.Translate(request.UserId, "smart-filters"),
                Content = new FeedEntryContent()
                {
                    Text = await _localizationService.Translate(request.UserId, "browse-smart-filters")
                },
                Links =
                [
                    CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, $"{request.Prefix}{request.ApiKey}/smart-filters"),
                ]
            });
        }

        return feed;
    }

    public async Task<Feed> GetSmartFilters(OpdsPaginatedCatalogueRequest request)
    {
        var userId = UnpackRequest(request, out var apiKey, out var prefix, out var baseUrl);

        var filters = await _unitOfWork.AppUserSmartFilterRepository.GetPagedDtosByUserIdAsync(userId, GetUserParams(request.PageNumber));
        var feed = CreateFeed(await _localizationService.Translate(userId, "smartFilters"), $"{apiKey}/smart-filters", apiKey, prefix);
        SetFeedId(feed, "smartFilters");

        foreach (var filter in filters)
        {
            feed.Entries.Add(new FeedEntry()
            {
                Id = filter.Id.ToString(),
                Title = filter.Name,
                Links =
                [
                    CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation,
                        $"{prefix}{apiKey}/smart-filters/{filter.Id}")
                ]
            });
        }

        AddPagination(feed, filters, $"{prefix}{apiKey}/smart-filters");

        return feed;
    }

    public async Task<Feed> GetLibraries(OpdsPaginatedCatalogueRequest request)
    {
        var userId = UnpackRequest(request, out var apiKey, out var prefix, out var baseUrl);
        var feed = CreateFeed(await _localizationService.Translate(userId, "libraries"), $"{apiKey}/libraries", apiKey, prefix);
        SetFeedId(feed, "libraries");

        // TODO: This needs pagination and the query can be optimized

        // Ensure libraries follow SideNav order
        var userSideNavStreams = await _unitOfWork.UserRepository.GetSideNavStreams(userId);
        var libraries = userSideNavStreams.Where(s => s.StreamType == SideNavStreamType.Library)
            .Select(sideNavStream => sideNavStream.Library);

        foreach (var library in libraries)
        {
            feed.Entries.Add(new FeedEntry()
            {
                Id = library!.Id.ToString(),
                Title = library.Name!,
                Links =
                [
                    CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation,
                        $"{prefix}{apiKey}/libraries/{library.Id}"),
                    CreateLink(FeedLinkRelation.Image, FeedLinkType.Image,
                        $"{baseUrl}api/image/library-cover?libraryId={library.Id}&apiKey={apiKey}"),
                    CreateLink(FeedLinkRelation.Thumbnail, FeedLinkType.Image,
                        $"{baseUrl}api/image/library-cover?libraryId={library.Id}&apiKey={apiKey}")
                ]
            });
        }

        //AddPagination(feed, libraries, $"{prefix}{apiKey}/libraries");

        return feed;
    }

    public async Task<Feed> GetWantToRead(OpdsPaginatedCatalogueRequest request)
    {
        var userId = UnpackRequest(request, out var apiKey, out var prefix, out var baseUrl);

        var wantToReadSeries = await _unitOfWork.SeriesRepository.GetWantToReadForUserV2Async(userId, GetUserParams(request.PageNumber), _filterV2Dto);
        var seriesMetadatas = await _unitOfWork.SeriesRepository.GetSeriesMetadataForIds(wantToReadSeries.Select(s => s.Id));

        var feed = CreateFeed(await _localizationService.Translate(userId, "want-to-read"), $"{apiKey}/want-to-read", apiKey, prefix);
        SetFeedId(feed, $"want-to-read");
        AddPagination(feed, wantToReadSeries, $"{prefix}{apiKey}/want-to-read");

        feed.Entries.AddRange(wantToReadSeries.Select(seriesDto =>
            CreateSeries(seriesDto, seriesMetadatas.First(s => s.SeriesId == seriesDto.Id), apiKey, prefix, baseUrl)));

        return feed;
    }

    public async Task<Feed> GetCollections(OpdsPaginatedCatalogueRequest request)
    {
        var userId = UnpackRequest(request, out var apiKey, out var prefix, out var baseUrl);
        var tags = await _unitOfWork.CollectionTagRepository.GetCollectionDtosPagedAsync(userId, GetUserParams(request.PageNumber), true);

        var feed = CreateFeed(await _localizationService.Translate(userId, "collections"), $"{apiKey}/collections", apiKey, prefix);
        SetFeedId(feed, "collections");


        feed.Entries.AddRange(tags.Select(tag => new FeedEntry()
        {
            Id = tag.Id.ToString(),
            Title = tag.Title,
            Summary = tag.Summary,
            Links =
            [
                CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation,
                    $"{prefix}{apiKey}/collections/{tag.Id}"),
                CreateLink(FeedLinkRelation.Image, FeedLinkType.Image,
                    $"{baseUrl}api/image/collection-cover?collectionTagId={tag.Id}&apiKey={apiKey}"),
                CreateLink(FeedLinkRelation.Thumbnail, FeedLinkType.Image,
                    $"{baseUrl}api/image/collection-cover?collectionTagId={tag.Id}&apiKey={apiKey}")
            ]
        }));

        AddPagination(feed, tags, $"{prefix}{apiKey}/collections");

        return feed;
    }

    public async Task<Feed> GetRecentlyAdded(OpdsPaginatedCatalogueRequest request)
    {
        var userId = UnpackRequest(request, out var apiKey, out var prefix, out var baseUrl);

        var recentlyAdded = await _unitOfWork.SeriesRepository.GetRecentlyAddedV2(userId, GetUserParams(request.PageNumber), _filterV2Dto);
        var seriesMetadatas = await _unitOfWork.SeriesRepository.GetSeriesMetadataForIds(recentlyAdded.Select(s => s.Id));

        var feed = CreateFeed(await _localizationService.Translate(userId, "recently-added"), $"{apiKey}/recently-added", apiKey, prefix);
        SetFeedId(feed, "recently-added");
        AddPagination(feed, recentlyAdded, $"{prefix}{apiKey}/recently-added");

        foreach (var seriesDto in recentlyAdded)
        {
            feed.Entries.Add(CreateSeries(seriesDto, seriesMetadatas.First(s => s.SeriesId == seriesDto.Id), apiKey, prefix, baseUrl));
        }

        return feed;
    }

    public async Task<Feed> GetRecentlyUpdated(OpdsPaginatedCatalogueRequest request)
    {
        var userId = UnpackRequest(request, out var apiKey, out var prefix, out var baseUrl);

        var seriesDtos = (await _unitOfWork.SeriesRepository.GetRecentlyUpdatedSeries(userId, GetUserParams(request.PageNumber))).ToList();
        var seriesMetadatas = await _unitOfWork.SeriesRepository.GetSeriesMetadataForIds(seriesDtos.Select(s => s.SeriesId));

        var feed = CreateFeed(await _localizationService.Translate(userId, "recently-updated"), $"{apiKey}/recently-updated", apiKey, prefix);
        SetFeedId(feed, "recently-updated");

        foreach (var groupedSeries in seriesDtos)
        {
            var seriesDto = new SeriesDto()
            {
                Name = $"{groupedSeries.SeriesName} ({groupedSeries.Count})",
                Id = groupedSeries.SeriesId,
                Format = groupedSeries.Format,
                LibraryId = groupedSeries.LibraryId,
            };
            var metadata = seriesMetadatas.First(s => s.SeriesId == seriesDto.Id);
            feed.Entries.Add(CreateSeries(seriesDto, metadata, apiKey, prefix, baseUrl));
        }
        AddPagination(feed, request.PageNumber, 0, PageSize, $"{apiKey}/recently-updated");

        return feed;
    }

    public async Task<Feed> GetOnDeck(OpdsPaginatedCatalogueRequest request)
    {
        var userId = UnpackRequest(request, out var apiKey, out var prefix, out var baseUrl);

        var pagedList = await _unitOfWork.SeriesRepository.GetOnDeck(userId, 0, GetUserParams(request.PageNumber), _filterDto);
        var seriesMetadatas = await _unitOfWork.SeriesRepository.GetSeriesMetadataForIds(pagedList.Select(s => s.Id));

        var feed = CreateFeed(await _localizationService.Translate(userId, "on-deck"), $"{apiKey}/on-deck", apiKey, prefix);
        SetFeedId(feed, "on-deck");
        AddPagination(feed, pagedList, $"{prefix}{apiKey}/on-deck");

        foreach (var seriesDto in pagedList)
        {
            feed.Entries.Add(CreateSeries(seriesDto, seriesMetadatas.First(s => s.SeriesId == seriesDto.Id), apiKey, prefix, baseUrl));
        }

        return feed;
    }

    public async Task<Feed> GetMoreInGenre(OpdsItemsFromEntityIdRequest request)
    {
        var userId = UnpackRequest(request, out var apiKey, out var prefix, out var baseUrl);
        var genreId = request.EntityId;

        var genre = await _unitOfWork.GenreRepository.GetGenreById(genreId);
        if (genre == null)
        {
            throw new OpdsException(await _localizationService.Translate(userId, "genre-doesnt-exist"));
        }
        var seriesDtos = await _unitOfWork.SeriesRepository.GetMoreIn(userId, 0, genreId, GetUserParams(request.PageNumber));
        var seriesMetadatas = await _unitOfWork.SeriesRepository.GetSeriesMetadataForIds(seriesDtos.Select(s => s.Id));

        var feed = CreateFeed(await _localizationService.Translate(userId, "more-in-genre", genre.Title), $"{apiKey}/more-in-genre", apiKey, prefix);
        SetFeedId(feed, "more-in-genre");
        AddPagination(feed, seriesDtos, $"{prefix}{apiKey}/more-in-genre");

        foreach (var seriesDto in seriesDtos)
        {
            feed.Entries.Add(CreateSeries(seriesDto, seriesMetadatas.First(s => s.SeriesId == seriesDto.Id), apiKey, prefix, baseUrl));
        }

        return feed;
    }

    /// <summary>
    /// Returns the Series matching this smart filter.
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public async Task<Feed> GetSeriesFromSmartFilter(OpdsItemsFromEntityIdRequest request)
    {
        var userId = UnpackRequest(request, out var apiKey, out var prefix, out var baseUrl);

        var filter = await _unitOfWork.AppUserSmartFilterRepository.GetById(request.EntityId);
        if (filter == null)
        {
            throw new OpdsException(await _localizationService.Translate(userId, "smart-filter-doesnt-exist"));
        }

        var feed = CreateFeed(await _localizationService.Translate(userId, "smartFilters-" + filter.Id), $"{apiKey}/smart-filters/{filter.Id}/", apiKey, prefix);
        SetFeedId(feed, "smartFilters-" + filter.Id);

        var decodedFilter = SmartFilterHelper.Decode(filter.Filter);
        var series = await _unitOfWork.SeriesRepository.GetSeriesDtoForLibraryIdV2Async(userId, GetUserParams(request.PageNumber),
            decodedFilter);
        var seriesMetadatas = await _unitOfWork.SeriesRepository.GetSeriesMetadataForIds(series.Select(s => s.Id));

        foreach (var seriesDto in series)
        {
            feed.Entries.Add(CreateSeries(seriesDto, seriesMetadatas.First(s => s.SeriesId == seriesDto.Id), apiKey, prefix, baseUrl));
        }

        AddPagination(feed, series, $"{prefix}{apiKey}/smart-filters/{request.EntityId}/");

        return feed;
    }

    public async Task<Feed> GetSeriesFromCollection(OpdsItemsFromEntityIdRequest request)
    {
        var userId = UnpackRequest(request, out var apiKey, out var prefix, out var baseUrl);
        var collectionId = request.EntityId;

        var tag = await _unitOfWork.CollectionTagRepository.GetCollectionAsync(collectionId);
        if (tag == null || (tag.AppUserId != userId && !tag.Promoted))
        {
            throw new OpdsException(await _localizationService.Translate(userId, "collection-doesnt-exist"));
        }

        var series = await _unitOfWork.SeriesRepository.GetSeriesDtoForCollectionAsync(collectionId, userId, GetUserParams(request.PageNumber));
        var seriesMetadatas = await _unitOfWork.SeriesRepository.GetSeriesMetadataForIds(series.Select(s => s.Id));

        var feed = CreateFeed(tag.Title + " Collection", $"{apiKey}/collections/{collectionId}", apiKey, prefix);
        SetFeedId(feed, $"collections-{collectionId}");
        AddPagination(feed, series, $"{prefix}{apiKey}/collections/{collectionId}");

        foreach (var seriesDto in series)
        {
            feed.Entries.Add(CreateSeries(seriesDto, seriesMetadatas.First(s => s.SeriesId == seriesDto.Id), apiKey, prefix, baseUrl));
        }

        return feed;
    }

    public async Task<Feed> GetSeriesFromLibrary(OpdsItemsFromEntityIdRequest request)
    {
        var userId = UnpackRequest(request, out var apiKey, out var prefix, out var baseUrl);
        var libraryId = request.EntityId;

        var library = (await _unitOfWork.LibraryRepository.GetLibrariesForUserIdAsync(userId))
            .SingleOrDefault(l => l.Id == libraryId);

        if (library == null)
        {
            throw new OpdsException(await _localizationService.Translate(userId, "no-library-access"));
        }

        var filter = new FilterV2Dto
        {
            Statements = [
                new FilterStatementDto
                {
                    Comparison = FilterComparison.Equal,
                    Field = FilterField.Libraries,
                    Value = libraryId + string.Empty
                }
            ]
        };

        var series = await _unitOfWork.SeriesRepository.GetSeriesDtoForLibraryIdV2Async(userId, GetUserParams(request.PageNumber), filter);
        var seriesMetadatas = await _unitOfWork.SeriesRepository.GetSeriesMetadataForIds(series.Select(s => s.Id));

        var feed = CreateFeed(library.Name, $"{apiKey}/libraries/{libraryId}", apiKey, prefix);
        SetFeedId(feed, $"library-{library.Name}");
        AddPagination(feed, series, $"{prefix}{apiKey}/libraries/{libraryId}");

        feed.Entries.AddRange(series.Select(seriesDto =>
            CreateSeries(seriesDto, seriesMetadatas.First(s => s.SeriesId == seriesDto.Id), apiKey, prefix, baseUrl)));

        return feed;
    }


    public async Task<Feed> GetReadingListItems(OpdsItemsFromEntityIdRequest request)
    {
        var userId = UnpackRequest(request, out var apiKey, out var prefix, out var baseUrl);
        var readingListId = request.EntityId;

        var readingList = await _unitOfWork.ReadingListRepository.GetReadingListDtoByIdAsync(readingListId, userId);
        if (readingList == null)
        {
            throw new OpdsException(await _localizationService.Translate(request.UserId, "reading-list-restricted"));
        }

        var feed = CreateFeed(readingList.Title + " " + await _localizationService.Translate(userId, "reading-list"), $"{apiKey}/reading-list/{readingListId}", apiKey, prefix);
        SetFeedId(feed, $"reading-list-{readingListId}");

        var items = (await _unitOfWork.ReadingListRepository.GetReadingListItemDtosByIdAsync(readingListId, userId, GetUserParams(request.PageNumber))).ToList();
        var totalItems = (await _unitOfWork.ReadingListRepository.GetReadingListItemDtosByIdAsync(readingListId, userId)).Count();


        // Check if there is reading progress or not, if so, inject a "continue-reading" item
        var anyProgress = await _unitOfWork.ReadingListRepository.AnyUserReadingProgressAsync(readingListId, userId);
        if (anyProgress)
        {
            var firstReadReadingListItem = await _unitOfWork.ReadingListRepository.GetContinueReadingPoint(readingListId, userId);
            if (firstReadReadingListItem != null && request.PageNumber == FirstPageNumber)
            {
                await AddContinueReadingPoint(firstReadReadingListItem, feed, request);
            }
        }



        foreach (var item in items)
        {
            var chapterDto = await _unitOfWork.ChapterRepository.GetChapterDtoAsync(item.ChapterId, userId);

            // If there is only one file underneath, add a direct acquisition link, otherwise add a subsection
            if (chapterDto is {Files.Count: 1})
            {
                var series = await _unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(item.SeriesId, userId);
                feed.Entries.Add(await CreateChapterWithFile(item.SeriesId, item.VolumeId, item.ChapterId,
                    chapterDto.Files.First(), series!, chapterDto, request));
            }
            else
            {
                feed.Entries.Add(
                    CreateChapter($"{item.Order} - {item.SeriesName}: {item.Title}",
                        item.Summary ?? string.Empty, item.ChapterId, item.VolumeId, item.SeriesId, request));
            }
        }

        AddPagination(feed, request.PageNumber, totalItems, UserParams.Default.PageSize, $"{prefix}{apiKey}/reading-list/{readingListId}/");

        return feed;
    }

    public async Task<Feed> GetSeriesDetail(OpdsItemsFromEntityIdRequest request)
    {
        var userId = UnpackRequest(request, out var apiKey, out var prefix, out var baseUrl);
        var seriesId = request.EntityId;

        var series = await _unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(seriesId, userId);

        var feed = CreateFeed(series!.Name + " - Storyline", $"{apiKey}/series/{series.Id}", apiKey, prefix);
        SetFeedId(feed, $"series-{series.Id}");
        feed.Links.Add(CreateLink(FeedLinkRelation.Image, FeedLinkType.Image, $"{baseUrl}api/image/series-cover?seriesId={seriesId}&apiKey={apiKey}"));


        // Check if there is reading progress or not, if so, inject a "continue-reading" item
        var anyUserProgress = await _unitOfWork.AppUserProgressRepository.AnyUserProgressForSeriesAsync(seriesId, userId);
        if (anyUserProgress)
        {
            var chapterDto = await _readerService.GetContinuePoint(seriesId, userId);
            await AddContinueReadingPoint(seriesId, chapterDto, feed, request);
        }


        var chapterDict = new Dictionary<int, short>();
        var fileDict = new Dictionary<int, short>();

        var seriesDetail =  await _seriesService.GetSeriesDetail(seriesId, userId);
        foreach (var volume in seriesDetail.Volumes)
        {
            var chaptersForVolume = await _unitOfWork.ChapterRepository.GetChapterDtosAsync(volume.Id, userId);

            foreach (var chapterDto in chaptersForVolume)
            {
                var chapterId = chapterDto.Id;
                if (!chapterDict.TryAdd(chapterId, 0)) continue;

                foreach (var mangaFile in chapterDto.Files)
                {
                    // If a chapter has multiple files that are within one chapter, this dict prevents duplicate key exception
                    if (!fileDict.TryAdd(mangaFile.Id, 0)) continue;

                    feed.Entries.Add(await CreateChapterWithFile(seriesId, volume.Id, chapterId, _mapper.Map<MangaFileDto>(mangaFile), series,
                        chapterDto, request));
                }
            }
        }

        var chapters = seriesDetail.StorylineChapters;
        if (!seriesDetail.StorylineChapters.Any() && seriesDetail.Chapters.Any())
        {
            chapters = seriesDetail.Chapters;
        }

        foreach (var chapter in chapters.Where(c => !c.IsSpecial && !chapterDict.ContainsKey(c.Id)))
        {
            var files = await _unitOfWork.ChapterRepository.GetFilesForChapterAsync(chapter.Id);
            var chapterDto = _mapper.Map<ChapterDto>(chapter);
            foreach (var mangaFile in files)
            {
                // If a chapter has multiple files that are within one chapter, this dict prevents duplicate key exception
                if (!fileDict.TryAdd(mangaFile.Id, 0)) continue;
                feed.Entries.Add(await CreateChapterWithFile(seriesId, chapter.VolumeId, chapter.Id, _mapper.Map<MangaFileDto>(mangaFile), series,
                    chapterDto, request));
            }
        }

        foreach (var special in seriesDetail.Specials)
        {
            var files = await _unitOfWork.ChapterRepository.GetFilesForChapterAsync(special.Id);
            var chapterDto = _mapper.Map<ChapterDto>(special);
            foreach (var mangaFile in files)
            {
                // If a chapter has multiple files that are within one chapter, this dict prevents duplicate key exception
                if (!fileDict.TryAdd(mangaFile.Id, 0)) continue;

                feed.Entries.Add(await CreateChapterWithFile(seriesId, special.VolumeId, special.Id, _mapper.Map<MangaFileDto>(mangaFile), series,
                    chapterDto, request));
            }
        }

        return feed;
    }

    public async Task<Feed> GetItemsFromVolume(OpdsItemsFromCompoundEntityIdsRequest request)
    {
        var userId = UnpackRequest(request, out var apiKey, out var prefix, out var baseUrl);
        var seriesId = request.SeriesId;
        var volumeId = request.VolumeId;

        var series = await _unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(seriesId, userId);
        if (series == null)
        {
            throw new OpdsException(await _localizationService.Translate(userId, "series-doesnt-exist"));
        }

        var volume = await _unitOfWork.VolumeRepository.GetVolumeAsync(volumeId, VolumeIncludes.Chapters);
        if (volume == null)
        {
            throw new OpdsException(await _localizationService.Translate(userId, "volume-doesnt-exist"));
        }

        var feed = CreateFeed($"{series.Name} - Volume {volume!.Name}",
            $"{apiKey}/series/{seriesId}/volume/{volumeId}", apiKey, prefix);
        SetFeedId(feed, $"series-{series.Id}-volume-{volume.Id}");

        var chapterDtos = await _unitOfWork.ChapterRepository.GetChapterDtoByIdsAsync(volume.Chapters.Select(c => c.Id), userId);

        // Check if there is reading progress or not, if so, inject a "continue-reading" item
        var firstChapterWithProgress = chapterDtos.FirstOrDefault(i => i.PagesRead > 0 && i.PagesRead != i.Pages) ??
                                       chapterDtos.FirstOrDefault(i => i.PagesRead == 0 && i.PagesRead != i.Pages);
        if (firstChapterWithProgress != null && request.PageNumber == FirstPageNumber)
        {
            var chapterDto = await _readerService.GetContinuePoint(seriesId, userId);
            await AddContinueReadingPoint(seriesId, chapterDto, feed, request);
        }

        foreach (var chapterDto in chapterDtos)
        {
            foreach (var mangaFile in chapterDto.Files)
            {
                feed.Entries.Add(await CreateChapterWithFile(seriesId, volumeId, chapterDto.Id, mangaFile, series, chapterDto!, request));
            }
        }

        return feed;
    }

    public async Task<Feed> GetItemsFromChapter(OpdsItemsFromCompoundEntityIdsRequest request)
    {
        var userId = UnpackRequest(request, out var apiKey, out var prefix, out var baseUrl);
        var seriesId = request.SeriesId;
        var volumeId = request.VolumeId;
        var chapterId = request.ChapterId;

        var series = await _unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(seriesId, userId);
        if (series == null)
        {
            throw new OpdsException(await _localizationService.Translate(userId, "series-doesnt-exist"));
        }

        var libraryType = await _unitOfWork.LibraryRepository.GetLibraryTypeAsync(series.LibraryId);
        var chapter = await _unitOfWork.ChapterRepository.GetChapterDtoAsync(chapterId, userId);

        if (chapter == null)
        {
            throw new OpdsException(await _localizationService.Translate(userId, "chapter-doesnt-exist"));
        }

        var volume = await _unitOfWork.VolumeRepository.GetVolumeAsync(volumeId);

        var chapterName = await _seriesService.FormatChapterName(userId, libraryType);
        var feed = CreateFeed( $"{series.Name} - Volume {volume!.Name} - {chapterName} {chapterId}",
            $"{apiKey}/series/{seriesId}/volume/{volumeId}/chapter/{chapterId}", apiKey, prefix);
        SetFeedId(feed, $"series-{series.Id}-volume-{volumeId}-{_seriesService.FormatChapterName(userId, libraryType)}-{chapterId}-files");

        foreach (var mangaFile in chapter.Files)
        {
            feed.Entries.Add(await CreateChapterWithFile(seriesId, volumeId, chapterId, mangaFile, series, chapter, request));
        }

        return feed;
    }

    public async Task<Feed> Search(OpdsSearchRequest request)
    {
        var userId = UnpackRequest(request, out var apiKey, out var prefix, out var baseUrl);
        var query = request.Query;

        var user = await _unitOfWork.UserRepository.GetUserByIdAsync(userId);

        if (string.IsNullOrEmpty(query))
        {
            throw new  OpdsException(await _localizationService.Translate(userId, "query-required"));
        }
        query = query.Replace("%", string.Empty);

        var libraries = (await _unitOfWork.LibraryRepository.GetLibrariesForUserIdAsync(userId)).ToList();
        if (libraries.Count == 0)
        {
            throw new OpdsException(await _localizationService.Translate(userId, "libraries-restricted"));
        }

        var isAdmin = await _unitOfWork.UserRepository.IsUserAdminAsync(user);

        var searchResults = await _unitOfWork.SeriesRepository.SearchSeries(userId, isAdmin,
            libraries.Select(l => l.Id).ToArray(), query, includeChapterAndFiles: false);

        var feed = CreateFeed(query, $"{apiKey}/series?query=" + query, apiKey, prefix);
        SetFeedId(feed, "search-series");
        foreach (var seriesDto in searchResults.Series)
        {
            feed.Entries.Add(CreateSeries(seriesDto, apiKey, prefix, baseUrl));
        }

        foreach (var collection in searchResults.Collections)
        {
            feed.Entries.Add(new FeedEntry()
            {
                Id = collection.Id.ToString(),
                Title = collection.Title,
                Summary = collection.Summary,
                Links =
                [
                    CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation,
                        $"{prefix}{apiKey}/collections/{collection.Id}"),
                    CreateLink(FeedLinkRelation.Image, FeedLinkType.Image,
                        $"{baseUrl}api/image/collection-cover?collectionId={collection.Id}&apiKey={apiKey}"),
                    CreateLink(FeedLinkRelation.Thumbnail, FeedLinkType.Image,
                        $"{baseUrl}api/image/collection-cover?collectionId={collection.Id}&apiKey={apiKey}")
                ]
            });
        }

        foreach (var readingListDto in searchResults.ReadingLists)
        {
            feed.Entries.Add(new FeedEntry()
            {
                Id = readingListDto.Id.ToString(),
                Title = readingListDto.Title,
                Summary = readingListDto.Summary,
                Links =
                [
                    CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, $"{prefix}{apiKey}/reading-list/{readingListDto.Id}"),
                ]
            });
        }

        feed.Total = feed.Entries.Count;

        return feed;
    }

    public async Task<Feed> GetReadingLists(OpdsPaginatedCatalogueRequest request)
    {
        var userId = UnpackRequest(request, out var apiKey, out var prefix, out var baseUrl);

        var readingLists = await _unitOfWork.ReadingListRepository.GetReadingListDtosForUserAsync(userId,
            true, GetUserParams(request.PageNumber), false);


        var feed = CreateFeed("All Reading Lists", $"{apiKey}/reading-list", apiKey, prefix);
        SetFeedId(feed, "reading-list");
        AddPagination(feed, readingLists, $"{prefix}{apiKey}/reading-list/");

        foreach (var readingListDto in readingLists)
        {
            feed.Entries.Add(new FeedEntry()
            {
                Id = readingListDto.Id.ToString(),
                Title = readingListDto.Title,
                Summary = readingListDto.Summary,
                Links =
                [
                    CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation,
                        $"{prefix}{apiKey}/reading-list/{readingListDto.Id}"),
                    CreateLink(FeedLinkRelation.Image, FeedLinkType.Image,
                        $"{baseUrl}api/image/readinglist-cover?readingListId={readingListDto.Id}&apiKey={apiKey}"),
                    CreateLink(FeedLinkRelation.Thumbnail, FeedLinkType.Image,
                        $"{baseUrl}api/image/readinglist-cover?readingListId={readingListDto.Id}&apiKey={apiKey}")
                ]
            });
        }

        return feed;
    }

    private static int UnpackRequest(IOpdsRequest request, out string apiKey, out string prefix,
        out string baseUrl)
    {
        var userId = request.UserId;
        apiKey = request.ApiKey;
        prefix = request.Prefix;
        baseUrl = request.BaseUrl;

        return userId;
    }


    public string SerializeXml(Feed? feed)
    {
        if (feed == null) return string.Empty;

        // Remove invalid XML characters from the feed object
        SanitizeFeed(feed);

        using var sm = new StringWriter();
        _xmlSerializer.Serialize(sm, feed);

        var ret = sm.ToString().Replace("utf-16", "utf-8"); // Chunky cannot accept UTF-16 feeds

        return ret;
    }

    // Recursively sanitize all string properties in the object
    private static void SanitizeFeed(object? obj)
    {
        if (obj == null) return;

        var properties = obj.GetType().GetProperties();
        foreach (var property in properties)
        {
            // Skip properties that require an index (e.g., indexed collections)
            if (property.GetIndexParameters().Length > 0)
                continue;

            if (property.PropertyType == typeof(string) && property.CanWrite)
            {
                var value = (string?)property.GetValue(obj);
                if (!string.IsNullOrEmpty(value))
                {
                    property.SetValue(obj, RemoveInvalidXmlChars(value));
                }
            }
            else if (property.PropertyType.IsClass) // Handle nested objects
            {
                var nestedObject = property.GetValue(obj);
                if (nestedObject != null)
                    SanitizeFeed(nestedObject);
            }
        }
    }

    private static string RemoveInvalidXmlChars(string input)
    {
        return new string(input.Where(XmlConvert.IsXmlChar).ToArray());
    }


    private static void SetFeedId(Feed feed, string id)
    {
        feed.Id = id;
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
            Links =
            [
                link,
                CreateLink(FeedLinkRelation.Start, FeedLinkType.AtomNavigation, $"{prefix}{apiKey}"),
                CreateLink(FeedLinkRelation.Search, FeedLinkType.AtomSearch, $"{prefix}{apiKey}/search")
            ],
        };
    }

    private static void AddPagination<T>(Feed feed, PagedList<T> list, string href)
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

        if (pageNumber > FirstPageNumber)
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

    private static void AddPagination(Feed feed, int currentPage, int totalItems, int pageSize, string href)
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

        var pageNumber = Math.Max(currentPage, 1);
        var totalPages = totalItems / pageSize;

        if (pageNumber > 1)
        {
            feed.Links.Add(CreateLink(FeedLinkRelation.Prev, FeedLinkType.AtomNavigation, url + "pageNumber=" + (pageNumber - 1)));
        }

        if (pageNumber + 1 <= totalPages)
        {
            feed.Links.Add(CreateLink(FeedLinkRelation.Next, FeedLinkType.AtomNavigation, url + "pageNumber=" + (pageNumber + 1)));
        }

        // Update self to point to current page
        var selfLink = feed.Links.SingleOrDefault(l => l.Rel == FeedLinkRelation.Self);
        if (selfLink != null)
        {
            selfLink.Href = url + "pageNumber=" + pageNumber;
        }


        feed.Total = totalItems;
        feed.ItemsPerPage = pageSize;
        feed.StartIndex = (Math.Max(currentPage - 1, 0) * pageSize) + 1;
    }


    private static FeedEntry CreateSeries(SeriesDto seriesDto, SeriesMetadataDto metadata, string apiKey, string prefix, string baseUrl)
    {
        return new FeedEntry()
        {
            Id = seriesDto.Id.ToString(),
            Title = $"{seriesDto.Name}",
            Summary = $"Format: {seriesDto.Format}" + (string.IsNullOrWhiteSpace(metadata.Summary)
                ? string.Empty
                : $"     Summary: {metadata.Summary}"),
            Authors = metadata.Writers.Select(CreateAuthor).ToList(),
            Categories = metadata.Genres.Select(g => new FeedCategory()
            {
                Label = g.Title,
                Term = string.Empty
            }).ToList(),
            Links =
            [
                CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation,
                    $"{prefix}{apiKey}/series/{seriesDto.Id}"),
                CreateLink(FeedLinkRelation.Image, FeedLinkType.Image,
                    $"{baseUrl}api/image/series-cover?seriesId={seriesDto.Id}&apiKey={apiKey}"),
                CreateLink(FeedLinkRelation.Thumbnail, FeedLinkType.Image,
                    $"{baseUrl}api/image/series-cover?seriesId={seriesDto.Id}&apiKey={apiKey}")
            ]
        };
    }

    private static FeedEntry CreateSeries(SearchResultDto searchResultDto, string apiKey, string prefix, string baseUrl)
    {
        return new FeedEntry()
        {
            Id = searchResultDto.SeriesId.ToString(),
            Title = $"{searchResultDto.Name}",
            Summary = $"Format: {searchResultDto.Format}",
            Links =
            [
                CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation,
                    $"{prefix}{apiKey}/series/{searchResultDto.SeriesId}"),
                CreateLink(FeedLinkRelation.Image, FeedLinkType.Image,
                    $"{baseUrl}api/image/series-cover?seriesId={searchResultDto.SeriesId}&apiKey={apiKey}"),
                CreateLink(FeedLinkRelation.Thumbnail, FeedLinkType.Image,
                    $"{baseUrl}api/image/series-cover?seriesId={searchResultDto.SeriesId}&apiKey={apiKey}")
            ]
        };
    }

    private static FeedAuthor CreateAuthor(PersonDto person)
    {
        return new FeedAuthor()
        {
            Name = person.Name,
            Uri = "http://opds-spec.org/author/" + person.Id
        };
    }

    private static FeedEntry CreateChapter(string title, string? summary, int chapterId, int volumeId, int seriesId, IOpdsRequest request)
    {
        var userId = UnpackRequest(request, out var apiKey, out var prefix, out var baseUrl);
        return new FeedEntry()
        {
            Id = chapterId.ToString(),
            Title = title,
            Summary = summary ?? string.Empty,

            Links =
            [
                CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation,
                    $"{prefix}{apiKey}/series/{seriesId}/volume/{volumeId}/chapter/{chapterId}"),
                CreateLink(FeedLinkRelation.Image, FeedLinkType.Image,
                    $"{baseUrl}api/image/chapter-cover?chapterId={chapterId}&apiKey={apiKey}"),
                CreateLink(FeedLinkRelation.Thumbnail, FeedLinkType.Image,
                    $"{baseUrl}api/image/chapter-cover?chapterId={chapterId}&apiKey={apiKey}")
            ]
        };
    }

    private async Task<FeedEntry> CreateContinueReadingFromFile(int seriesId, int volumeId, int chapterId,
        MangaFileDto mangaFile, SeriesDto series, ChapterDto chapter, IOpdsRequest request)
    {
        var entry = await CreateChapterWithFile(seriesId, volumeId, chapterId, mangaFile, series, chapter, request);

        entry.Title = await _localizationService.Translate(request.UserId, "opds-continue-reading-title", entry.Title);

        return entry;
    }

    private async Task<FeedEntry> CreateChapterWithFile(int seriesId, int volumeId, int chapterId,
        MangaFileDto mangaFile, SeriesDto series, ChapterDto chapter, IOpdsRequest request)
    {
        var fileSize =
            mangaFile.Bytes > 0 ? DirectoryService.GetHumanReadableBytes(mangaFile.Bytes) :
            DirectoryService.GetHumanReadableBytes(_directoryService.GetTotalSize((List<string>) [mangaFile.FilePath]));
        var fileType = _downloadService.GetContentTypeFromFile(mangaFile.FilePath);
        var filename = Uri.EscapeDataString(Path.GetFileName(mangaFile.FilePath));
        var libraryType = await _unitOfWork.LibraryRepository.GetLibraryTypeAsync(series.LibraryId);
        var volume = await _unitOfWork.VolumeRepository.GetVolumeDtoAsync(volumeId, request.UserId);


        var title = $"{series.Name}";

        if (volume!.Chapters.Count == 1 && !volume.IsSpecial())
        {
            var volumeLabel = await _localizationService.Translate(request.UserId, "volume-num", string.Empty);
            SeriesService.RenameVolumeName(volume, libraryType, volumeLabel);

            if (!volume.IsLooseLeaf())
            {
                title += $" - {volume.Name}";
            }
        }
        else if (!volume.IsLooseLeaf() && !volume.IsSpecial())
        {
            title = $"{series.Name} -  Volume {volume.Name} - {await _seriesService.FormatChapterTitle(request.UserId, chapter, libraryType)}";
        }
        else
        {
            title = $"{series.Name} - {await _seriesService.FormatChapterTitle(request.UserId, chapter, libraryType)}";
        }

        // Chunky requires a file at the end. Our API ignores this
        var accLink = CreateLink(FeedLinkRelation.Acquisition, fileType,
                    $"{request.Prefix}{request.ApiKey}/series/{seriesId}/volume/{volumeId}/chapter/{chapterId}/download/{filename}",
                    filename);

        accLink.TotalPages = chapter.Pages;

        var entry = new FeedEntry()
        {
            Id = mangaFile.Id.ToString(),
            Title = title,
            Extent = fileSize,
            Summary = $"File Type: {fileType.Split("/")[1]} - {fileSize}" + (string.IsNullOrWhiteSpace(chapter.Summary)
                ? string.Empty
                : $"     Summary: {chapter.Summary}"),
            Format = mangaFile.Format.ToString(),
            Links =
            [
                CreateLink(FeedLinkRelation.Image, FeedLinkType.Image,
                    $"{request.BaseUrl}api/image/chapter-cover?chapterId={chapterId}&apiKey={request.ApiKey}"),
                CreateLink(FeedLinkRelation.Thumbnail, FeedLinkType.Image,
                    $"{request.BaseUrl}api/image/chapter-cover?chapterId={chapterId}&apiKey={request.ApiKey}"),
                // We MUST include acc link in the feed, panels doesn't work with just page streaming option. We have to block download directly
                accLink
            ],
            Content = new FeedEntryContent()
            {
                Text = fileType,
                Type = "text"
            },
            Authors = chapter.Writers.Select(CreateAuthor).ToList()
        };

        var canPageStream = mangaFile.Extension != ".epub";
        if (canPageStream)
        {
            entry.Links.Add(await CreatePageStreamLink(series.LibraryId, seriesId, volumeId, chapterId, mangaFile, request));
        }

        // Patch in reading status on the item (as OPDS is seriously lacking)
        entry.Title = $"{GetReadingProgressIcon(chapter.PagesRead, chapter.Pages)} {entry.Title}";

        return entry;
    }

    private static string GetReadingProgressIcon(int pagesRead, int totalPages)
    {
        if (pagesRead == 0)
        {
            return NoReadingProgressIcon;
        }

        var percentageRead = (double)pagesRead / totalPages;

        return percentageRead switch
        {
            // 100%
            >= 1.0 => FullReadingProgressIcon,
            // > 50% and < 100%
            > 0.5 => AboveHalfReadingProgressIcon,
            // > 25% and <= 50%
            > 0.25 => HalfReadingProgressIcon,
            _ => QuarterReadingProgressIcon
        };
    }

    private async Task<FeedLink> CreatePageStreamLink(int libraryId, int seriesId, int volumeId, int chapterId, MangaFileDto mangaFile, IOpdsRequest request)
    {
        var userId = request.UserId;
        var progress = await _unitOfWork.AppUserProgressRepository.GetUserProgressDtoAsync(chapterId, userId);

        // NOTE: Type could be wrong, there is nothing I can do in the spec
        var link = CreateLink(FeedLinkRelation.Stream, "image/jpeg",
            $"{request.Prefix}{request.ApiKey}/image?libraryId={libraryId}&seriesId={seriesId}&volumeId={volumeId}&chapterId={chapterId}&pageNumber=" + "{pageNumber}");
        link.TotalPages = mangaFile.Pages;
        link.IsPageStream = true;

        if (progress != null)
        {
            link.LastRead = progress.PageNum;
            link.LastReadDate = progress.LastModifiedUtc.ToString("s"); // Adhere to ISO 8601
        }

        return link;
    }

    private static UserParams GetUserParams(int pageNumber)
    {
        return new UserParams()
        {
            PageNumber = pageNumber,
            PageSize = PageSize
        };
    }

    private async Task AddContinueReadingPoint(int seriesId, ChapterDto chapterDto, Feed feed, IOpdsRequest request)
    {
        var series = await _unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(seriesId, request.UserId);
        if (chapterDto is {Files.Count: 1})
        {
            feed.Entries.Add(await CreateContinueReadingFromFile(seriesId, chapterDto.VolumeId, chapterDto.Id,
                chapterDto.Files.First(), series!, chapterDto, request));
        }
    }

    private async Task AddContinueReadingPoint(ReadingListItemDto firstReadReadingListItem, Feed feed, IOpdsRequest request)
    {
        var chapterDto = await _unitOfWork.ChapterRepository.GetChapterDtoAsync(firstReadReadingListItem.ChapterId, request.UserId);
        var series = await _unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(firstReadReadingListItem.SeriesId, request.UserId);
        if (chapterDto is {Files.Count: 1})
        {
            feed.Entries.Add(await CreateContinueReadingFromFile(firstReadReadingListItem.SeriesId, firstReadReadingListItem.VolumeId,
                firstReadReadingListItem.ChapterId, chapterDto.Files.First(), series!, chapterDto, request));
        }
    }
}
