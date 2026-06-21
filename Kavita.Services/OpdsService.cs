using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Kavita.API.Database;
using Kavita.API.Errors;
using Kavita.API.Services;
using Kavita.API.Services.Reading;
using Kavita.API.Services.ReadingLists;
using Kavita.Common.Helpers;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.Filtering.v2;
using Kavita.Models.DTOs.Filtering.v2.Requests;
using Kavita.Models.DTOs.OPDS;
using Kavita.Models.DTOs.OPDS.Requests;
using Kavita.Models.DTOs.Person;
using Kavita.Models.DTOs.ReadingLists;
using Kavita.Models.DTOs.Search;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Services.Helpers.SmartFilter;

namespace Kavita.Services;

public class OpdsService(
    IUnitOfWork unitOfWork,
    ILocalizationService localizationService,
    ISeriesService seriesService,
    IDownloadService downloadService,
    IDirectoryService directoryService,
    IReaderService readerService,
    IEntityNamingService namingService,
    IReadingListService readingListService)
    : IOpdsService
{
    private readonly XmlSerializer _xmlSerializer = new(typeof(Feed));

    public const int PageSize = 20;
    public const int FirstPageNumber = 1;
    public const string DefaultApiPrefix = "/api/opds/";

    public const string NoReadingProgressIcon = "⭘";
    public const string QuarterReadingProgressIcon = "◔";
    public const string HalfReadingProgressIcon = "◑";
    public const string AboveHalfReadingProgressIcon = "◕";
    public const string FullReadingProgressIcon = "⬤";

    private readonly SeriesFilterV2Dto _seriesFilterV2Dto = new();

    public async Task<Feed> GetCatalogue(OpdsCatalogueRequest request, CancellationToken ct = default)
    {
        var feed = CreateFeed("Kavita", string.Empty, request.ApiKey, request.Prefix);
        SetFeedId(feed, "root");

        // Get the user's customized dashboard
        var streams = await unitOfWork.UserRepository.GetDashboardStreams(request.UserId, true, ct);
        foreach (var stream in streams)
        {
            switch (stream.StreamType)
            {
                case DashboardStreamType.OnDeck:
                    feed.Entries.Add(new FeedEntry()
                    {
                        Id = "onDeck",
                        Title = await localizationService.TranslateAsync(request.UserId, "on-deck"),
                        Content = new FeedEntryContent()
                        {
                            Text = await localizationService.TranslateAsync(request.UserId, "browse-on-deck")
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
                        Title = await localizationService.TranslateAsync(request.UserId, "recently-added"),
                        Content = new FeedEntryContent()
                        {
                            Text = await localizationService.TranslateAsync(request.UserId, "browse-recently-added")
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
                        Title = await localizationService.TranslateAsync(request.UserId, "recently-updated"),
                        Content = new FeedEntryContent()
                        {
                            Text = await localizationService.TranslateAsync(request.UserId, "browse-recently-updated")
                        },
                        Links =
                        [
                            CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, $"{request.Prefix}{request.ApiKey}/recently-updated"),
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
            Title = await localizationService.TranslateAsync(request.UserId, "reading-lists"),
            Content = new FeedEntryContent()
            {
                Text = await localizationService.TranslateAsync(request.UserId, "browse-reading-lists")
            },
            Links =
            [
                CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, $"{request.Prefix}{request.ApiKey}/reading-list"),
            ]
        });
        feed.Entries.Add(new FeedEntry()
        {
            Id = "wantToRead",
            Title = await localizationService.TranslateAsync(request.UserId, "want-to-read"),
            Content = new FeedEntryContent()
            {
                Text = await localizationService.TranslateAsync(request.UserId, "browse-want-to-read")
            },
            Links =
            [
                CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, $"{request.Prefix}{request.ApiKey}/want-to-read"),
            ]
        });
        feed.Entries.Add(new FeedEntry()
        {
            Id = "allLibraries",
            Title = await localizationService.TranslateAsync(request.UserId, "libraries"),
            Content = new FeedEntryContent()
            {
                Text = await localizationService.TranslateAsync(request.UserId, "browse-libraries")
            },
            Links =
            [
                CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, $"{request.Prefix}{request.ApiKey}/libraries"),
            ]
        });
        feed.Entries.Add(new FeedEntry()
        {
            Id = "allCollections",
            Title = await localizationService.TranslateAsync(request.UserId, "collections"),
            Content = new FeedEntryContent()
            {
                Text = await localizationService.TranslateAsync(request.UserId, "browse-collections")
            },
            Links =
            [
                CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, $"{request.Prefix}{request.ApiKey}/collections"),
            ]
        });

        if ((await unitOfWork.AppUserSmartFilterRepository.GetAllDtosByUserId(request.UserId, ct)).Any())
        {
            feed.Entries.Add(new FeedEntry()
            {
                Id = "allSmartFilters",
                Title = await localizationService.TranslateAsync(request.UserId, "smart-filters"),
                Content = new FeedEntryContent()
                {
                    Text = await localizationService.TranslateAsync(request.UserId, "browse-smart-filters")
                },
                Links =
                [
                    CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, $"{request.Prefix}{request.ApiKey}/smart-filters"),
                ]
            });
        }

        return feed;
    }

    public async Task<Feed> GetSmartFilters(OpdsPaginatedCatalogueRequest request, CancellationToken ct = default)
    {
        var userId = UnpackRequest(request, out var apiKey, out var prefix, out _);

        var filters = await unitOfWork.AppUserSmartFilterRepository.GetPagedDtosByUserIdAsync(userId, GetUserParams(request.PageNumber), ct);
        var feed = CreateFeed(await localizationService.TranslateAsync(userId, "smartFilters"), $"{apiKey}/smart-filters", apiKey, prefix);
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

    public async Task<Feed> GetLibraries(OpdsPaginatedCatalogueRequest request, CancellationToken ct = default)
    {
        var userId = UnpackRequest(request, out var apiKey, out var prefix, out var baseUrl);
        var feed = CreateFeed(await localizationService.TranslateAsync(userId, "libraries"), $"{apiKey}/libraries", apiKey, prefix);
        SetFeedId(feed, "libraries");

        // default: This needs pagination and the query can be optimized

        // Ensure libraries follow SideNav order
        var userSideNavStreams = await unitOfWork.UserRepository.GetSideNavStreams(userId, ct: ct);
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

    public async Task<Feed> GetWantToRead(OpdsPaginatedCatalogueRequest request, CancellationToken ct = default)
    {
        var userId = UnpackRequest(request, out var apiKey, out var prefix, out var baseUrl);

        var wantToReadSeries = await unitOfWork.SeriesRepository.GetWantToReadDtosForUserAsync(userId, GetUserParams(request.PageNumber), _seriesFilterV2Dto, ct);
        var seriesMetadatas = await unitOfWork.SeriesRepository.GetSeriesMetadataForIdsAsync(wantToReadSeries.Select(s => s.Id), ct);

        var feed = CreateFeed(await localizationService.TranslateAsync(userId, "want-to-read"), $"{apiKey}/want-to-read", apiKey, prefix);
        SetFeedId(feed, "want-to-read");
        AddPagination(feed, wantToReadSeries, $"{prefix}{apiKey}/want-to-read");

        feed.Entries.AddRange(wantToReadSeries.Select(seriesDto =>
            CreateSeries(seriesDto, seriesMetadatas.First(s => s.SeriesId == seriesDto.Id), apiKey, prefix, baseUrl)));

        return feed;
    }

    public async Task<Feed> GetCollections(OpdsPaginatedCatalogueRequest request, CancellationToken ct = default)
    {
        var userId = UnpackRequest(request, out var apiKey, out var prefix, out var baseUrl);
        var tags = await unitOfWork.CollectionTagRepository.GetCollectionDtosPagedAsync(userId, GetUserParams(request.PageNumber), true, ct);

        var feed = CreateFeed(await localizationService.TranslateAsync(userId, "collections"), $"{apiKey}/collections", apiKey, prefix);
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

    public async Task<Feed> GetRecentlyAdded(OpdsPaginatedCatalogueRequest request, CancellationToken ct = default)
    {
        var userId = UnpackRequest(request, out var apiKey, out var prefix, out var baseUrl);

        var recentlyAdded = await unitOfWork.SeriesRepository.GetRecentlyAddedAsync(userId, GetUserParams(request.PageNumber), _seriesFilterV2Dto, ct);
        var seriesMetadatas = await unitOfWork.SeriesRepository.GetSeriesMetadataForIdsAsync(recentlyAdded.Select(s => s.Id), ct);

        var feed = CreateFeed(await localizationService.TranslateAsync(userId, "recently-added"), $"{apiKey}/recently-added", apiKey, prefix);
        SetFeedId(feed, "recently-added");
        AddPagination(feed, recentlyAdded, $"{prefix}{apiKey}/recently-added");

        foreach (var seriesDto in recentlyAdded)
        {
            feed.Entries.Add(CreateSeries(seriesDto, seriesMetadatas.First(s => s.SeriesId == seriesDto.Id), apiKey, prefix, baseUrl));
        }

        return feed;
    }

    public async Task<Feed> GetRecentlyUpdated(OpdsPaginatedCatalogueRequest request, CancellationToken ct = default)
    {
        var userId = UnpackRequest(request, out var apiKey, out var prefix, out var baseUrl);

        var seriesDtos = (await unitOfWork.SeriesRepository.GetRecentlyUpdatedSeriesAsync(userId, GetUserParams(request.PageNumber), ct)).ToList();
        var seriesMetadatas = await unitOfWork.SeriesRepository.GetSeriesMetadataForIdsAsync(seriesDtos.Select(s => s.SeriesId), ct);

        var feed = CreateFeed(await localizationService.TranslateAsync(userId, "recently-updated"), $"{apiKey}/recently-updated", apiKey, prefix);
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
        // Recently updated is hardcoded to 30 items
        AddPagination(feed, request.PageNumber, 30, PageSize, $"{prefix}{apiKey}/recently-updated");

        return feed;
    }

    public async Task<Feed> GetOnDeck(OpdsPaginatedCatalogueRequest request, CancellationToken ct = default)
    {
        var userId = UnpackRequest(request, out var apiKey, out var prefix, out var baseUrl);

        var pagedList = await unitOfWork.SeriesRepository.GetOnDeckAsync(userId, 0, GetUserParams(request.PageNumber), ct);
        var seriesMetadatas = await unitOfWork.SeriesRepository.GetSeriesMetadataForIdsAsync(pagedList.Select(s => s.Id), ct);

        var feed = CreateFeed(await localizationService.TranslateAsync(userId, "on-deck"), $"{apiKey}/on-deck", apiKey, prefix);
        SetFeedId(feed, "on-deck");
        AddPagination(feed, pagedList, $"{prefix}{apiKey}/on-deck");

        foreach (var seriesDto in pagedList)
        {
            feed.Entries.Add(CreateSeries(seriesDto, seriesMetadatas.First(s => s.SeriesId == seriesDto.Id), apiKey, prefix, baseUrl));
        }

        return feed;
    }

    /// <summary>
    /// Returns the Entities matching this smart filter.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public async Task<Feed> ResolveSmartFilter(OpdsItemsFromEntityIdRequest request, CancellationToken ct = default)
    {
        var userId = UnpackRequest(request, out var apiKey, out var prefix, out var baseUrl);

        var filter = await unitOfWork.AppUserSmartFilterRepository.GetById(request.EntityId, ct);
        if (filter == null)
        {
            throw new OpdsException(await localizationService.TranslateAsync(userId, "smart-filter-doesnt-exist"));
        }

        var feed = CreateFeed(await localizationService.TranslateAsync(userId, "smartFilters-" + filter.Id), $"{apiKey}/smart-filters/{filter.Id}/", apiKey, prefix);
        SetFeedId(feed, "smartFilters-" + filter.Id);

        var decodedFilter = SmartFilterHelper.Decode(filter.Filter);
        var userParams = GetUserParams(request.PageNumber);


        switch (decodedFilter.EntityType)
        {
            case FilterEntityType.Series:
                var series = await unitOfWork.SeriesRepository.GetSeriesDtoForLibraryIdAsync(userId, userParams,
                    (SeriesFilterV2Dto) decodedFilter, ct: ct);
                var seriesMetadatas = await unitOfWork.SeriesRepository.GetSeriesMetadataForIdsAsync(series.Select(s => s.Id), ct);

                foreach (var seriesDto in series)
                {
                    feed.Entries.Add(CreateSeries(seriesDto, seriesMetadatas.First(s => s.SeriesId == seriesDto.Id), apiKey, prefix, baseUrl));
                }
                AddPagination(feed, series, $"{prefix}{apiKey}/smart-filters/{request.EntityId}/");
                break;
            case FilterEntityType.ReadingList:
                var readingLists = await unitOfWork.ReadingListRepository.GetBrowseReadingListDtos(userId, (ReadingListFilterDto) decodedFilter, userParams, ct);
                foreach (var readingList in readingLists)
                {
                    feed.Entries.Add(CreateReadingListFeedEntry(readingList, prefix, apiKey, baseUrl));
                }
                AddPagination(feed, readingLists, $"{prefix}{apiKey}/smart-filters/{request.EntityId}/");
                break;
            case FilterEntityType.Person:
                throw new OpdsException("OPDS feed generation is not implemented for Person smart filters");
            case FilterEntityType.Annotation:
                throw new OpdsException("OPDS feed generation is not implemented for Annotation smart filters");
        }

        return feed;
    }

    public async Task<Feed> GetSeriesFromCollection(OpdsItemsFromEntityIdRequest request, CancellationToken ct = default)
    {
        var userId = UnpackRequest(request, out var apiKey, out var prefix, out var baseUrl);
        var collectionId = request.EntityId;

        var tag = await unitOfWork.CollectionTagRepository.GetCollectionAsync(collectionId, ct: ct);
        if (tag == null || (tag.AppUserId != userId && !tag.Promoted))
        {
            throw new OpdsException(await localizationService.TranslateAsync(userId, "collection-doesnt-exist"));
        }

        var series = await unitOfWork.SeriesRepository.GetSeriesDtoForCollectionAsync(collectionId, userId, GetUserParams(request.PageNumber), ct);
        var seriesMetadatas = await unitOfWork.SeriesRepository.GetSeriesMetadataForIdsAsync(series.Select(s => s.Id), ct);

        var feed = CreateFeed(tag.Title + " Collection", $"{apiKey}/collections/{collectionId}", apiKey, prefix);
        SetFeedId(feed, $"collections-{collectionId}");
        AddPagination(feed, series, $"{prefix}{apiKey}/collections/{collectionId}");

        foreach (var seriesDto in series)
        {
            feed.Entries.Add(CreateSeries(seriesDto, seriesMetadatas.First(s => s.SeriesId == seriesDto.Id), apiKey, prefix, baseUrl));
        }

        return feed;
    }

    public async Task<Feed> GetSeriesFromLibrary(OpdsItemsFromEntityIdRequest request, CancellationToken ct = default)
    {
        var userId = UnpackRequest(request, out var apiKey, out var prefix, out var baseUrl);
        var libraryId = request.EntityId;

        var library = (await unitOfWork.LibraryRepository.GetLibrariesForUserIdAsync(userId, ct))
            .SingleOrDefault(l => l.Id == libraryId);

        if (library == null)
        {
            throw new OpdsException(await localizationService.TranslateAsync(userId, "no-library-access"));
        }

        var filter = new SeriesFilterV2Dto
        {
            Statements = [
                new SeriesFilterStatementDto
                {
                    Comparison = FilterComparison.Equal,
                    Field = SeriesFilterField.Libraries,
                    Value = libraryId + string.Empty
                }
            ]
        };

        var series = await unitOfWork.SeriesRepository.GetSeriesDtoForLibraryIdAsync(userId, GetUserParams(request.PageNumber), filter, ct: ct);
        var seriesMetadatas = await unitOfWork.SeriesRepository.GetSeriesMetadataForIdsAsync(series.Select(s => s.Id), ct);

        var feed = CreateFeed(library.Name, $"{apiKey}/libraries/{libraryId}", apiKey, prefix);
        SetFeedId(feed, $"library-{library.Name}");
        AddPagination(feed, series, $"{prefix}{apiKey}/libraries/{libraryId}");

        feed.Entries.AddRange(series.Select(seriesDto =>
            CreateSeries(seriesDto, seriesMetadatas.First(s => s.SeriesId == seriesDto.Id), apiKey, prefix, baseUrl)));

        return feed;
    }


    public async Task<Feed> GetReadingListItems(OpdsItemsFromEntityIdRequest request, CancellationToken ct = default)
    {
        var userId = UnpackRequest(request, out var apiKey, out var prefix, out _);
        var readingListId = request.EntityId;

        var readingList = await unitOfWork.ReadingListRepository.GetReadingListDtoByIdAsync(readingListId, userId, ct);
        if (readingList == null)
        {
            throw new OpdsException(await localizationService.TranslateAsync(request.UserId, "reading-list-restricted"));
        }

        var feed = CreateFeed(readingList.Title + " " + await localizationService.TranslateAsync(userId, "reading-list"), $"{apiKey}/reading-list/{readingListId}", apiKey, prefix);
        SetFeedId(feed, $"reading-list-{readingListId}");

        var items = await readingListService.GetReadingListItems(readingListId, userId, GetUserParams(request.PageNumber));
        var totalItems = await unitOfWork.ReadingListRepository .GetReadingListItemCountAsync(readingListId, userId, ct);

        var chapterIds = items.Select(i => i.ChapterId).Distinct().ToList();
        var chapters = (await unitOfWork.ChapterRepository .GetChapterDtosAsync(chapterIds, userId, ct))
            .ToDictionary(c => c.Id);

        // Check if there is reading progress or not, if so, inject a "continue-reading" item

        if (request.Preferences.IncludeContinueFrom && request.PageNumber == FirstPageNumber)
        {
            var anyProgress = await unitOfWork.ReadingListRepository.AnyUserReadingProgressAsync(readingListId, userId, ct);
            if (anyProgress)
            {
                var continuePoint = await unitOfWork.ReadingListRepository.GetContinueReadingPoint(readingListId, userId, ct);

                if (continuePoint != null)
                {
                    var continueChapter =
                        await unitOfWork.ChapterRepository.GetChapterDtoAsync(continuePoint.ChapterId, request.UserId, ct);
                    if (continueChapter is {Files.Count: 1})
                    {
                        feed.Entries.Add(await CreateContinueReadingEntryAsync(continuePoint, continueChapter, request));
                    }
                }
            }
        }


        foreach (var item in items)
        {
            if (!chapters.TryGetValue(item.ChapterId, out var chapterDto))
            {
                continue; // Skip if chapter not found (shouldn't happen)
            }

            feed.Entries.Add(CreateReadingListEntry(item, chapterDto, request));
        }

        AddPagination(feed, request.PageNumber, totalItems, UserParams.Default.PageSize, $"{prefix}{apiKey}/reading-list/{readingListId}/");

        return feed;
    }

    public async Task<Feed> GetSeriesDetail(OpdsItemsFromEntityIdRequest request, CancellationToken ct = default)
    {
        var userId = UnpackRequest(request, out var apiKey, out var prefix, out var baseUrl);
        var seriesId = request.EntityId;

        var series = await unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(seriesId, userId, ct);
        if (series == null)
        {
            throw new OpdsException(await localizationService.TranslateAsync(userId, "series-doesnt-exist"));
        }

        var seriesDetailTask = seriesService.GetSeriesDetail(seriesId, userId, ct);
        var libraryTypeTask = unitOfWork.LibraryRepository.GetLibraryTypeAsync(series.LibraryId, ct);

        await Task.WhenAll(seriesDetailTask, libraryTypeTask);

        var seriesDetail = await seriesDetailTask;
        var libraryType = await libraryTypeTask;

        var namingContext = await LocalizedNamingContext.CreateAsync(namingService, localizationService, userId, libraryType);
        var volumesById = seriesDetail.Volumes.ToDictionary(v => v.Id);

        var feed = CreateFeed(series.Name + " - Storyline", $"{apiKey}/series/{series.Id}", apiKey, prefix);
        SetFeedId(feed, $"series-{series.Id}");
        feed.Links.Add(CreateLink(FeedLinkRelation.Image, FeedLinkType.Image, $"{baseUrl}api/image/series-cover?seriesId={seriesId}&apiKey={apiKey}"));


        // Check if there is reading progress or not, if so, inject a "continue-reading" item
        if (request.Preferences.IncludeContinueFrom)
        {
            var anyUserProgress = await unitOfWork.AppUserProgressRepository
                .AnyUserProgressForSeriesAsync(seriesId, userId, ct);
            if (anyUserProgress)
            {
                var continueChapter = await readerService.GetContinuePoint(seriesId, userId);
                if (continueChapter is { Files.Count: 1 })
                {
                    volumesById.TryGetValue(continueChapter.VolumeId, out var continueVolume);
                    feed.Entries.Add(await CreateContinueReadingEntryAsync(series, continueVolume, continueChapter, namingContext, request));
                }
            }
        }

        var chaptersSeen = new Dictionary<int, short>();

        foreach (var volume in seriesDetail.Volumes)
        {
            foreach (var chapter in volume.Chapters)
            {
                if (!chaptersSeen.TryAdd(chapter.Id, 0)) continue;

                feed.Entries.Add(CreateChapterFeedEntry(series, volume, chapter, namingContext, request));
            }
        }

        var chapters = seriesDetail.StorylineChapters.Any()
            ? seriesDetail.StorylineChapters
            : seriesDetail.Chapters;

        foreach (var chapter in chapters.Where(c => !c.IsSpecial && !chaptersSeen.ContainsKey(c.Id)))
        {
            volumesById.TryGetValue(chapter.VolumeId, out var volume);

            feed.Entries.Add(CreateChapterFeedEntry(series, volume, chapter, namingContext, request));
        }

        foreach (var special in seriesDetail.Specials)
        {
            volumesById.TryGetValue(special.VolumeId, out var volume);

            feed.Entries.Add(CreateChapterFeedEntry(series, volume, special, namingContext, request));
        }

        return feed;
    }

    public async Task<Feed> GetItemsFromVolume(OpdsItemsFromCompoundEntityIdsRequest request,
        CancellationToken ct = default)
    {
        var userId = UnpackRequest(request, out var apiKey, out var prefix, out _);
        var seriesId = request.SeriesId;
        var volumeId = request.VolumeId;

        var series = await unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(seriesId, userId, ct);
        if (series == null)
        {
            throw new OpdsException(await localizationService.TranslateAsync(userId, "series-doesnt-exist"));
        }

        var volume = await unitOfWork.VolumeRepository.GetVolumeDtoAsync(volumeId, request.UserId, ct);
        if (volume == null)
        {
            throw new OpdsException(await localizationService.TranslateAsync(userId, "volume-doesnt-exist"));
        }

        var libraryType = await unitOfWork.LibraryRepository.GetLibraryTypeAsync(series.LibraryId, ct);
        var namingContext = await LocalizedNamingContext.CreateAsync( namingService, localizationService, userId, libraryType);

        var feed = CreateFeed($"{series.Name} - Volume {volume.Name}",
            $"{apiKey}/series/{seriesId}/volume/{volumeId}", apiKey, prefix);
        SetFeedId(feed, $"series-{series.Id}-volume-{volume.Id}");

        // Check if there is reading progress or not, if so, inject a "continue-reading" item
        if (request.Preferences.IncludeContinueFrom && request.PageNumber == FirstPageNumber)
        {
            var firstChapterWithProgress = volume.Chapters.FirstOrDefault(i => i.PagesRead > 0 && i.PagesRead != i.Pages)
                                           ?? volume.Chapters.FirstOrDefault(i => i.PagesRead == 0 && i.PagesRead != i.Pages);

            if (firstChapterWithProgress is { Files.Count: 1 })
            {
                feed.Entries.Add(await CreateContinueReadingEntryAsync(series, volume, firstChapterWithProgress, namingContext, request));
            }
        }

        foreach (var chapterDto in volume.Chapters)
        {
            feed.Entries.Add(CreateChapterFeedEntry(series, volume, chapterDto, namingContext, request));
        }

        return feed;
    }

    public async Task<Feed> GetItemsFromChapter(OpdsItemsFromCompoundEntityIdsRequest request,
        CancellationToken ct = default)
    {
        var userId = UnpackRequest(request, out var apiKey, out var prefix, out _);

        var seriesId = request.SeriesId;
        var volumeId = request.VolumeId;
        var chapterId = request.ChapterId;

        var series = await unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(seriesId, userId, ct);
        if (series == null)
        {
            throw new OpdsException(await localizationService.TranslateAsync(userId, "series-doesnt-exist"));
        }

        var volume = await unitOfWork.VolumeRepository.GetVolumeDtoAsync(volumeId,  userId, ct);
        if (volume == null)
        {
            throw new OpdsException(await localizationService.TranslateAsync(userId, "volume-doesnt-exist"));
        }

        var libraryType = await unitOfWork.LibraryRepository.GetLibraryTypeAsync(series.LibraryId, ct);
        var chapter = volume.Chapters.FirstOrDefault(c => c.Id == chapterId);
        if (chapter == null)
        {
            throw new OpdsException(await localizationService.TranslateAsync(userId, "chapter-doesnt-exist"));
        }

        var namingContext = await LocalizedNamingContext.CreateAsync(namingService, localizationService, userId, libraryType);
        var chapterName = namingContext.FormatChapterTitle(chapter);

        var feed = CreateFeed( $"{series.Name} - Volume {volume.Name} - {chapterName} {chapterId}",
            $"{apiKey}/series/{seriesId}/volume/{volumeId}/chapter/{chapterId}", apiKey, prefix);
        SetFeedId(feed, $"series-{series.Id}-volume-{volumeId}-{chapterId}-files");


        feed.Entries.Add(CreateChapterFeedEntry(series, volume, chapter, namingContext, request));

        return feed;
    }

    public async Task<Feed> Search(OpdsSearchRequest request, CancellationToken ct = default)
    {
        var userId = UnpackRequest(request, out var apiKey, out var prefix, out var baseUrl);
        var query = request.Query;

        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId, ct: ct);

        if (string.IsNullOrEmpty(query))
        {
            throw new  OpdsException(await localizationService.TranslateAsync(userId, "query-required"));
        }
        query = query.Replace("%", string.Empty);

        var libraries = (await unitOfWork.LibraryRepository.GetLibrariesForUserIdAsync(userId, ct)).ToList();
        if (libraries.Count == 0)
        {
            throw new OpdsException(await localizationService.TranslateAsync(userId, "libraries-restricted"));
        }

        var isAdmin = await unitOfWork.UserRepository.IsUserAdminAsync(user, ct);

        var searchResults = await unitOfWork.SeriesRepository.SearchSeriesAsync(userId, isAdmin,
            libraries.Select(l => l.Id).ToArray(), query, includeChapterAndFiles: false, ct: ct);

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

    public async Task<Feed> GetReadingLists(OpdsPaginatedCatalogueRequest request, CancellationToken ct = default)
    {
        var userId = UnpackRequest(request, out var apiKey, out var prefix, out var baseUrl);

        var readingLists = await unitOfWork.ReadingListRepository.GetReadingListDtosForUserAsync(userId,
            true, GetUserParams(request.PageNumber), false, ct);


        var feed = CreateFeed("All Reading Lists", $"{apiKey}/reading-list", apiKey, prefix);
        SetFeedId(feed, "reading-list");
        AddPagination(feed, readingLists, $"{prefix}{apiKey}/reading-list/");

        foreach (var readingListDto in readingLists)
        {
            feed.Entries.Add(CreateReadingListFeedEntry(readingListDto, prefix, apiKey, baseUrl));
        }

        return feed;
    }

    private static FeedEntry CreateReadingListFeedEntry(ReadingListDto readingListDto, string prefix, string apiKey, string baseUrl)
    {
        return new FeedEntry()
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
        };
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

        // Update self to point to the current page
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
        var totalPages = (int) Math.Ceiling((double) totalItems / pageSize);

        if (pageNumber > 1)
        {
            feed.Links.Add(CreateLink(FeedLinkRelation.Prev, FeedLinkType.AtomNavigation, url + "pageNumber=" + (pageNumber - 1)));
        }

        if (pageNumber + 1 <= totalPages)
        {
            feed.Links.Add(CreateLink(FeedLinkRelation.Next, FeedLinkType.AtomNavigation, url + "pageNumber=" + (pageNumber + 1)));
        }

        // Update self to point to the current page
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

    private FeedEntry CreateChapterFeedEntry(SeriesDto series, VolumeDto? volume, ChapterDto chapter,
        LocalizedNamingContext namingContext, IOpdsRequest request)
    {
        var fileSize = GetFileSize(chapter);

        var file = chapter.Files.First();
        // We know chapters can only contain files from the same format so this is fine
        var fileType = downloadService.GetContentTypeFromFile(file.FilePath);
        // This isn't really file, as it is wrong. But the filename isn't truly used by the api, see OpdsController.DownloadFile
        // So it's good enough
        var filename = Uri.EscapeDataString(Path.GetFileName(file.FilePath));

        var title = namingContext.BuildFullTitle(series, volume, chapter);

        var accLink = CreateLink(
            FeedLinkRelation.Acquisition,
            fileType,
            $"{request.Prefix}{request.ApiKey}/series/{series.Id}/volume/{chapter.VolumeId}/chapter/{chapter.Id}/download/{filename}",
            filename);
        accLink.TotalPages = chapter.Pages;

        var entry = new FeedEntry
        {
            Id = chapter.Id.ToString(),
            Title = title,
            Extent = fileSize,
            Summary = BuildSummary(fileType, fileSize, chapter.Summary),
            Format = chapter.Format.ToString(),
            Links =
            [
                CreateLink(FeedLinkRelation.Image, FeedLinkType.Image,
                    $"{request.BaseUrl}api/image/chapter-cover?chapterId={chapter.Id}&apiKey={request.ApiKey}"),
                CreateLink(FeedLinkRelation.Thumbnail, FeedLinkType.Image,
                    $"{request.BaseUrl}api/image/chapter-cover?chapterId={chapter.Id}&apiKey={request.ApiKey}"),
                accLink
            ],
            Content = new FeedEntryContent
            {
                Text = fileType,
                Type = "text"
            },
            Authors = chapter.Writers.Select(CreateAuthor).ToList()
        };

        // Page streaming (non-epub only)
        if (chapter.Format != MangaFormat.Epub)
        {
            entry.Links.Add(CreatePageStreamLink(series.LibraryId, series.Id, chapter, request));
        }

        if (request.Preferences.EmbedProgressIndicator)
        {
            entry.Title = $"{GetReadingProgressIcon(chapter.PagesRead, chapter.Pages)} {entry.Title}";
        }

        return entry;
    }

    private string GetFileSize(ChapterDto chapter)
    {
        var totalSizeFilesWithBytes = chapter.Files
            .Where(f => f.Bytes > 0)
            .Sum(f => f.Bytes);

        var totalSizeFilesWithOutBytes = directoryService.GetTotalSize(chapter.Files
            .Where(f => f.Bytes == 0)
            .Select(f => f.FilePath)
        );

        return DirectoryService.GetHumanReadableBytes(totalSizeFilesWithBytes + totalSizeFilesWithOutBytes);
    }


    private static string BuildSummary(string fileType, string fileSize, string? chapterSummary)
    {
        var extension = fileType.Split('/') is [_, var ext] ? ext : fileType;

        return string.IsNullOrWhiteSpace(chapterSummary)
            ? $"File Type: {extension} - {fileSize}"
            : $"File Type: {extension} - {fileSize}     Summary: {chapterSummary}";
    }


    private FeedEntry CreateReadingListEntry(ReadingListItemDto item, ChapterDto chapter, IOpdsRequest request)
    {
        var fileSize = GetFileSize(chapter);

        var file = chapter.Files.First();
        // We know chapters can only contain files from the same format so this is fine
        var fileType = downloadService.GetContentTypeFromFile(file.FilePath);
        // This isn't really file, as it is wrong. But the filename isn't truly used by the api, see OpdsController.DownloadFile
        // So it's good enough
        var filename = Uri.EscapeDataString(Path.GetFileName(file.FilePath));

        var title = namingService.FormatReadingListItemTitle(item);
        var displayTitle = $"{item.Order} - {item.SeriesName}: {title}";

        var accLink = CreateLink(
            FeedLinkRelation.Acquisition,
            fileType,
            $"{request.Prefix}{request.ApiKey}/series/{item.SeriesId}/volume/{item.VolumeId}/chapter/{item.ChapterId}/download/{filename}",
            filename);
        accLink.TotalPages = chapter.Pages;

        var entry = new FeedEntry
        {
            Id = chapter.Id.ToString(),
            Title = displayTitle,
            Extent = fileSize,
            Summary = BuildSummary(fileType, fileSize, chapter.Summary),
            Format = chapter.Format.ToString(),
            Links =
            [
                CreateLink(FeedLinkRelation.Image, FeedLinkType.Image,
                    $"{request.BaseUrl}api/image/chapter-cover?chapterId={item.ChapterId}&apiKey={request.ApiKey}"),
                CreateLink(FeedLinkRelation.Thumbnail, FeedLinkType.Image,
                    $"{request.BaseUrl}api/image/chapter-cover?chapterId={item.ChapterId}&apiKey={request.ApiKey}"),
                accLink
            ],
            Content = new FeedEntryContent
            {
                Text = fileType,
                Type = "text"
            },
            Authors = chapter.Writers.Select(CreateAuthor).ToList()
        };

        // Page streaming for non-epub
        if (chapter.Format != MangaFormat.Epub)
        {
            entry.Links.Add(CreatePageStreamLink(item.LibraryId, item.SeriesId, chapter, request));
        }

        if (request.Preferences.EmbedProgressIndicator)
        {
            entry.Title = $"{GetReadingProgressIcon(item.PagesRead, chapter.Pages)} {entry.Title}";
        }

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

    private static FeedLink CreatePageStreamLink(int libraryId, int seriesId, ChapterDto chapter, IOpdsRequest request)
    {
        // NOTE: Type could be wrong, there is nothing I can do in the spec
        var link = CreateLink(FeedLinkRelation.Stream, "image/jpeg",
            $"{request.Prefix}{request.ApiKey}/image?libraryId={libraryId}&seriesId={seriesId}&volumeId={chapter.VolumeId}&chapterId={chapter.Id}&pageNumber=" + "{pageNumber}");
        link.TotalPages = chapter.Pages;
        link.IsPageStream = true;

        if (chapter.LastReadingProgressUtc > DateTime.MinValue)
        {
            link.LastRead = chapter.PagesRead;
            link.LastReadDate = chapter.LastReadingProgressUtc.ToString("s"); // Adhere to ISO 8601
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

    /// <summary>
    /// Creates a continued reading feed entry from a chapter.
    /// </summary>
    private async Task<FeedEntry> CreateContinueReadingEntryAsync( SeriesDto series, VolumeDto? volume, ChapterDto chapter, LocalizedNamingContext namingContext, IOpdsRequest request)
    {
        var entry = CreateChapterFeedEntry(series, volume, chapter, namingContext, request);

        entry.Title = await localizationService.TranslateAsync(
            request.UserId, "opds-continue-reading-title", entry.Title);

        return entry;
    }

    /// <summary>
    /// Creates a continue reading feed entry for a reading list item.
    /// </summary>
    private async Task<FeedEntry> CreateContinueReadingEntryAsync(ReadingListItemDto item, ChapterDto chapter, IOpdsRequest request)
    {
        var entry = CreateReadingListEntry(item, chapter, request);

        var titleWithoutIcon = request.Preferences.EmbedProgressIndicator && entry.Title.Length > 2
            ? entry.Title[2..]
            : entry.Title;

        entry.Title = await localizationService.TranslateAsync(
            request.UserId, "opds-continue-reading-title", titleWithoutIcon);

        return entry;
    }
}
