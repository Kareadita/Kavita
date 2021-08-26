﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using API.Comparators;
using API.Constants;
using API.DTOs;
using API.DTOs.Filtering;
using API.DTOs.OPDS;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers;
using API.Interfaces;
using API.Interfaces.Services;
using API.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    public class OpdsController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IDownloadService _downloadService;
        private readonly IDirectoryService _directoryService;
        private readonly ICacheService _cacheService;
        private readonly UserManager<AppUser> _userManager;


        private readonly XmlSerializer _xmlSerializer;
        private readonly XmlSerializer _xmlOpenSearchSerializer;
        private const string Prefix = "/api/opds/";
        private readonly FilterDto _filterDto = new FilterDto()
        {
            MangaFormat = null
        };
        private readonly ChapterSortComparer _chapterSortComparer = new ChapterSortComparer();

        public OpdsController(IUnitOfWork unitOfWork, IDownloadService downloadService, IDirectoryService directoryService, ICacheService cacheService, UserManager<AppUser> userManager)
        {
            _unitOfWork = unitOfWork;
            _downloadService = downloadService;
            _directoryService = directoryService;
            _cacheService = cacheService;
            _userManager = userManager;

            _xmlSerializer = new XmlSerializer(typeof(Feed));
            _xmlOpenSearchSerializer = new XmlSerializer(typeof(OpenSearchDescription));

        }

        [HttpPost]
        [HttpGet]
        [Produces("application/xml")]
        public async Task<IActionResult> Get()
        {
            if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
                return BadRequest("OPDS is not enabled on this server");
            var feed = CreateFeed("Kavita OPDS Catalog", string.Empty);
            feed.Id = "root";
            feed.Entries.Add(new FeedEntry()
            {
                Id = "inProgress",
                Title = "In Progress",
                Content = new FeedEntryContent()
                {
                    Text = "Browse by In Progress"
                },
                Links = new List<FeedLink>()
                {
                    CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, Prefix + "in-progress"),
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
                    CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, Prefix + "recently-added"),
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
                    CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, Prefix + "libraries"),
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
                    CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, Prefix + "collections"),
                }
            });
            return CreateXmlResult(SerializeXml(feed));
        }


        [HttpGet("libraries")]
        [Produces("application/xml")]
        public async Task<IActionResult> GetLibraries()
        {
            if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
                return BadRequest("OPDS is not enabled on this server");
            var user = await GetUser();
            var libraries = await _unitOfWork.LibraryRepository.GetLibrariesForUserIdAsync(user.Id);

            var feed = CreateFeed("All Libraries", "libraries");

            foreach (var library in libraries)
            {
                feed.Entries.Add(new FeedEntry()
                {
                    Id = library.Id.ToString(),
                    Title = library.Name,
                    Links = new List<FeedLink>()
                    {
                        CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, Prefix + $"libraries/{library.Id}"),
                    }
                });
            }

            return CreateXmlResult(SerializeXml(feed));
        }

        [HttpGet("collections")]
        [Produces("application/xml")]
        public async Task<IActionResult> GetCollections()
        {
            if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
                return BadRequest("OPDS is not enabled on this server");
            var user = await GetUser();
            var isAdmin = await _userManager.IsInRoleAsync(user, PolicyConstants.AdminRole);

            IEnumerable <CollectionTagDto> tags = null;
            if (isAdmin)
            {
                tags = await _unitOfWork.CollectionTagRepository.GetAllTagDtosAsync();
            }
            else
            {
                tags = await _unitOfWork.CollectionTagRepository.GetAllPromotedTagDtosAsync();
            }


            var feed = CreateFeed("All Collections", "collections");

            foreach (var tag in tags)
            {
                feed.Entries.Add(new FeedEntry()
                {
                    Id = tag.Id.ToString(),
                    Title = tag.Title,
                    Summary = tag.Summary,
                    Links = new List<FeedLink>()
                    {
                        CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, Prefix + $"collections/{tag.Id}"),
                        CreateLink(FeedLinkRelation.Image, FeedLinkType.Image, $"image/collection-cover?collectionId={tag.Id}"),
                        CreateLink(FeedLinkRelation.Thumbnail, FeedLinkType.Image, $"image/collection-cover?collectionId={tag.Id}")
                    }
                });
            }

            return CreateXmlResult(SerializeXml(feed));
        }

        [HttpGet("collections/{collectionId}")]
        [Produces("application/xml")]
        public async Task<IActionResult> GetCollection(int collectionId, [FromQuery] int pageNumber = 0)
        {
            if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
                return BadRequest("OPDS is not enabled on this server");
            var user = await GetUser();
            var isAdmin = await _userManager.IsInRoleAsync(user, PolicyConstants.AdminRole);

            IEnumerable <CollectionTagDto> tags = null;
            if (isAdmin)
            {
                tags = await _unitOfWork.CollectionTagRepository.GetAllTagDtosAsync();
            }
            else
            {
                tags = await _unitOfWork.CollectionTagRepository.GetAllPromotedTagDtosAsync();
            }

            var tag = tags.SingleOrDefault(t => t.Id == collectionId);
            if (tag == null)
            {
                return BadRequest("Collection does not exist or you don't have access");
            }
            //
            // // Apply progress/rating information (I can't work out how to do this in initial query)
            // if (series == null) return BadRequest("Could not get series for collection");
            //
            // await _unitOfWork.SeriesRepository.AddSeriesModifiers(user.Id, series);
            //
            // Response.AddPaginationHeader(series.CurrentPage, series.PageSize, series.TotalCount, series.TotalPages);

            var series = await _unitOfWork.SeriesRepository.GetSeriesDtoForCollectionAsync(collectionId, user.Id, new UserParams()
            {
                PageNumber = pageNumber,
                PageSize = 20
            });

            var feed = CreateFeed(tag.Title + " Collection", $"collections/{collectionId}");
            AddPagination(feed, series, $"{Prefix}collections/{collectionId}");

            foreach (var seriesDto in series)
            {
                feed.Entries.Add(CreateSeries(seriesDto));
            }


            return CreateXmlResult(SerializeXml(feed));
        }

        [HttpGet("libraries/{libraryId}")]
        [Produces("application/xml")]
        public async Task<IActionResult> GetSeriesForLibrary(int libraryId, [FromQuery] int pageNumber = 1)
        {
            if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
                return BadRequest("OPDS is not enabled on this server");
            var user = await GetUser();
            var library =
                (await _unitOfWork.LibraryRepository.GetLibrariesForUserIdAsync(user.Id)).SingleOrDefault(l =>
                    l.Id == libraryId);
            if (library == null)
            {
                return BadRequest("User does not have access to this library");
            }

            var series = await _unitOfWork.SeriesRepository.GetSeriesDtoForLibraryIdAsync(libraryId, user.Id, new UserParams()
            {
                PageNumber = pageNumber,
                PageSize = 20
            }, _filterDto);

            var feed = CreateFeed(library.Name, $"libraries/{libraryId}");
            AddPagination(feed, series, $"{Prefix}libraries/{libraryId}");

            foreach (var seriesDto in series)
            {
                feed.Entries.Add(CreateSeries(seriesDto));
            }

            return CreateXmlResult(SerializeXml(feed));
        }

        [HttpGet("recently-added")]
        [Produces("application/xml")]
        public async Task<IActionResult> GetRecentlyAdded([FromQuery] int pageNumber = 1)
        {
            if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
                return BadRequest("OPDS is not enabled on this server");
            var user = await GetUser();
            var recentlyAdded = await _unitOfWork.SeriesRepository.GetRecentlyAdded(0, user.Id, new UserParams()
            {
                PageNumber = pageNumber,
                PageSize = 20
            }, _filterDto);

            var feed = CreateFeed("Recently Added", "recently-added");
            AddPagination(feed, recentlyAdded, $"{Prefix}recently-added");

            foreach (var seriesDto in recentlyAdded)
            {
                feed.Entries.Add(CreateSeries(seriesDto));
            }


            return CreateXmlResult(SerializeXml(feed));
        }

        [HttpGet("in-progress")]
        [Produces("application/xml")]
        public async Task<IActionResult> GetInProgress([FromQuery] int pageNumber = 1)
        {
            if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
                return BadRequest("OPDS is not enabled on this server");
            var user = await GetUser();
            var userParams = new UserParams()
            {
                PageNumber = pageNumber,
                PageSize = 20
            };
            var results = await _unitOfWork.SeriesRepository.GetInProgress(user.Id, 0, userParams, _filterDto);
            var listResults = results.DistinctBy(s => s.Name).Skip((userParams.PageNumber - 1) * userParams.PageSize)
                .Take(userParams.PageSize).ToList();
            var pagedList = new PagedList<SeriesDto>(listResults, listResults.Count, userParams.PageNumber, userParams.PageSize);

            Response.AddPaginationHeader(pagedList.CurrentPage, pagedList.PageSize, pagedList.TotalCount, pagedList.TotalPages);

            var feed = CreateFeed("In Progress", "in-progress");
            AddPagination(feed, pagedList, $"{Prefix}in-progress");

            foreach (var seriesDto in pagedList)
            {
                feed.Entries.Add(CreateSeries(seriesDto));
            }

            return CreateXmlResult(SerializeXml(feed));
        }

        [HttpGet("series")]
        [Produces("application/xml")]
        public async Task<IActionResult> SearchSeries([FromQuery] string query)
        {
            if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
                return BadRequest("OPDS is not enabled on this server");
            var user = await GetUser();
            if (string.IsNullOrEmpty(query))
            {
                return BadRequest("You must pass a query parameter");
            }
            query = query.Replace(@"%", "");
            // Get libraries user has access to
            var libraries = (await _unitOfWork.LibraryRepository.GetLibrariesForUserIdAsync(user.Id)).ToList();

            if (!libraries.Any()) return BadRequest("User does not have access to any libraries");

            var series = await _unitOfWork.SeriesRepository.SearchSeries(libraries.Select(l => l.Id).ToArray(), query);

            var feed = CreateFeed(query, "series?query=" + query);

            foreach (var seriesDto in series)
            {
                feed.Entries.Add(CreateSeries(seriesDto));
            }


            return CreateXmlResult(SerializeXml(feed));
        }

        [HttpGet("search")]
        [Produces("application/xml")]
        public async Task<IActionResult> GetSearchDescriptor()
        {
            if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
                return BadRequest("OPDS is not enabled on this server");
            var feed = new OpenSearchDescription()
            {
                ShortName = "Search",
                Description = "Search for Series",
                Url = new SearchLink()
                {
                    Type = FeedLinkType.AtomAcquisition,
                    Template = $"{Prefix}series?query=" + "{searchTerms}"
                }
            };

            await using var sm = new StringWriter();
            _xmlOpenSearchSerializer.Serialize(sm, feed);

            return CreateXmlResult(sm.ToString());
        }

        [HttpGet("series/{seriesId}")]
        [Produces("application/xml")]
        public async Task<IActionResult> GetSeries(int seriesId)
        {
            if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
                return BadRequest("OPDS is not enabled on this server");
            var user = await GetUser();
            var series = await _unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(seriesId, user.Id);
            var volumes = await _unitOfWork.SeriesRepository.GetVolumesDtoAsync(seriesId, user.Id);
            var feed = CreateFeed(series.Name + " - Volumes", $"series/{series.Id}");
            feed.Links.Add(CreateLink(FeedLinkRelation.Image, FeedLinkType.Image, $"image/series-cover?seriesId={seriesId}"));
            foreach (var volumeDto in volumes)
            {
                feed.Entries.Add(CreateVolume(volumeDto, seriesId));
            }

            return CreateXmlResult(SerializeXml(feed));
        }

        [HttpGet("series/{seriesId}/volume/{volumeId}")]
        [Produces("application/xml")]
        public async Task<IActionResult> GetVolume(int seriesId, int volumeId)
        {
            if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
                return BadRequest("OPDS is not enabled on this server");
            var user = await GetUser();
            var series = await _unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(seriesId, user.Id);
            var volume = await _unitOfWork.SeriesRepository.GetVolumeAsync(volumeId);
            var chapters =
                (await _unitOfWork.VolumeRepository.GetChaptersAsync(volumeId)).OrderBy(x => double.Parse(x.Number),
                    _chapterSortComparer);

            var feed = CreateFeed(series.Name + " - Volume " + volume.Name + " - Chapters ", $"series/{seriesId}/volume/{volumeId}");
            foreach (var chapter in chapters)
            {
                feed.Entries.Add(new FeedEntry()
                {
                    Id = chapter.Id.ToString(),
                    Title = "Chapter " + chapter.Number,
                    Links = new List<FeedLink>()
                    {
                        CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, Prefix + $"series/{seriesId}/volume/{volumeId}/chapter/{chapter.Id}"),
                        CreateLink(FeedLinkRelation.Image, FeedLinkType.Image, $"image/chapter-cover?chapterId={chapter.Id}")
                    }
                });
            }

            return CreateXmlResult(SerializeXml(feed));
        }

        [HttpGet("series/{seriesId}/volume/{volumeId}/chapter/{chapterId}")]
        [Produces("application/xml")]
        public async Task<IActionResult> GetChapter(int seriesId, int volumeId, int chapterId)
        {
            if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
                return BadRequest("OPDS is not enabled on this server");
            var user = await GetUser();
            var series = await _unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(seriesId, user.Id);
            var volume = await _unitOfWork.SeriesRepository.GetVolumeAsync(volumeId);
            var chapter = await _unitOfWork.VolumeRepository.GetChapterDtoAsync(chapterId);
            var files = await _unitOfWork.VolumeRepository.GetFilesForChapterAsync(chapterId);

            var feed = CreateFeed(series.Name + " - Volume " + volume.Name + " - Chapters ", $"series/{seriesId}/volume/{volumeId}/chapter/{chapterId}");
            foreach (var mangaFile in files)
            {
                feed.Entries.Add(CreateChapter(seriesId, volumeId, chapterId, mangaFile, series, volume, chapter));
            }

            return CreateXmlResult(SerializeXml(feed));
        }

        [HttpGet("series/{seriesId}/volume/{volumeId}/chapter/{chapterId}/download")]
        public async Task<ActionResult> DownloadFile(int seriesId, int volumeId, int chapterId)
        {
            if (!(await _unitOfWork.SettingsRepository.GetSettingsDtoAsync()).EnableOpds)
                return BadRequest("OPDS is not enabled on this server");
            var files = await _unitOfWork.VolumeRepository.GetFilesForChapterAsync(chapterId);
            var (bytes, contentType, fileDownloadName) = await _downloadService.GetFirstFileDownload(files);
            return File(bytes, contentType, fileDownloadName);
        }

        private ContentResult CreateXmlResult(string xml)
        {
            return new ContentResult
            {
                ContentType = "application/xml",
                Content = xml,
                StatusCode = 200
            };
        }

        private void AddPagination(Feed feed, PagedList<SeriesDto> list, string href)
        {
            var url = href;
            if (href.Contains("?"))
            {
                url += "&amp;";
            }
            else
            {
                url += "?";
            }

            if (list.CurrentPage > 1)
            {
                feed.Links.Add(CreateLink(FeedLinkRelation.Prev, FeedLinkType.AtomNavigation, url + "pageNumber=" + (list.CurrentPage - 1)));
            }

            if (list.CurrentPage + 1 < list.TotalPages)
            {
                feed.Links.Add(CreateLink(FeedLinkRelation.Next, FeedLinkType.AtomNavigation, url + "pageNumber=" + (list.CurrentPage + 1)));
            }

            // Update self to point to current page
            var selfLink = feed.Links.SingleOrDefault(l => l.Rel == FeedLinkRelation.Self);
            if (selfLink != null)
            {
                selfLink.Href = url + "pageNumber=" + list.CurrentPage;
            }


            feed.Total = list.TotalPages * list.PageSize;
            feed.ItemsPerPage = list.PageSize;
            feed.StartIndex = ((list.CurrentPage - 1) * list.PageSize) + 1;
        }

        private FeedEntry CreateSeries(SeriesDto seriesDto)
        {
            return new FeedEntry()
            {
                Id = seriesDto.Id.ToString(),
                Title = $"{seriesDto.Name} ({seriesDto.Format})",
                Summary = seriesDto.Summary,
                Links = new List<FeedLink>()
                {
                    CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, Prefix + $"series/{seriesDto.Id}"),
                    CreateLink(FeedLinkRelation.Image, FeedLinkType.Image, $"image/series-cover?seriesId={seriesDto.Id}"),
                    CreateLink(FeedLinkRelation.Thumbnail, FeedLinkType.Image, $"image/series-cover?seriesId={seriesDto.Id}")
                }
            };
        }

        private FeedEntry CreateSeries(SearchResultDto searchResultDto)
        {
            return new FeedEntry()
            {
                Id = searchResultDto.SeriesId.ToString(),
                Title = $"{searchResultDto.Name} ({searchResultDto.Format})",
                Links = new List<FeedLink>()
                {
                    CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, Prefix + $"series/{searchResultDto.SeriesId}"),
                    CreateLink(FeedLinkRelation.Image, FeedLinkType.Image, $"image/series-cover?seriesId={searchResultDto.SeriesId}"),
                    CreateLink(FeedLinkRelation.Thumbnail, FeedLinkType.Image, $"image/series-cover?seriesId={searchResultDto.SeriesId}")
                }
            };
        }

        private FeedEntry CreateVolume(VolumeDto volumeDto, int seriesId)
        {
            return new FeedEntry()
            {
                Id = volumeDto.Id.ToString(),
                Title = "Volume " + volumeDto.Name,
                Links = new List<FeedLink>()
                {
                    CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, Prefix + $"series/{seriesId}/volume/{volumeDto.Id}"),
                    CreateLink(FeedLinkRelation.Image, FeedLinkType.Image, $"image/volume-cover?volumeId={volumeDto.Id}"),
                    CreateLink(FeedLinkRelation.Thumbnail, FeedLinkType.Image, $"image/volume-cover?volumeId={volumeDto.Id}")
                }
            };
        }

        private FeedEntry CreateChapter(int seriesId, int volumeId, int chapterId, MangaFile mangaFile, SeriesDto series, Volume volume, ChapterDto chapter)
        {
            return new FeedEntry()
            {
                Id = mangaFile.Id.ToString(),
                Title = $"{series.Name} - Volume {volume.Name} - Chapter {chapter.Number}",
                Extent = DirectoryService.GetHumanReadableBytes(DirectoryService.GetTotalSize(new List<string>() { mangaFile.FilePath })),
                Format = mangaFile.Format.ToString(),
                Links = new List<FeedLink>()
                {
                    CreateLink(FeedLinkRelation.Image, FeedLinkType.Image, $"image/chapter-cover?chapterId={chapter.Id}"),
                    CreateLink(FeedLinkRelation.Thumbnail, FeedLinkType.Image, $"image/chapter-cover?chapterId={chapter.Id}"),
                    CreateLink(FeedLinkRelation.Acquisition, _downloadService.GetContentTypeFromFile(mangaFile.FilePath), $"{Prefix}series/{seriesId}/volume/{volumeId}/chapter/{chapterId}/download"),
                },
                Content = new FeedEntryContent()
                {
                    Text = Path.GetFileNameWithoutExtension(mangaFile.FilePath),
                    Type = _downloadService.GetContentTypeFromFile(mangaFile.FilePath)
                }
            };
        }

        [HttpGet("image")]
        public async Task<ActionResult> GetImage(int chapterId, int page)
        {
            if (page < 0) return BadRequest("Page cannot be less than 0");
            var chapter = await _cacheService.Ensure(chapterId);
            if (chapter == null) return BadRequest("There was an issue finding image file for reading");

            try
            {
                var (path, _) = await _cacheService.GetCachedPagePath(chapter, page);
                if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return BadRequest($"No such image for page {page}");

                var content = await _directoryService.ReadFileAsync(path);
                var format = Path.GetExtension(path).Replace(".", "");

                // Calculates SHA1 Hash for byte[]
                Response.AddCacheHeader(content);

                return File(content, "image/" + format);
            }
            catch (Exception)
            {
                _cacheService.CleanupChapters(new []{ chapterId });
                throw;
            }
        }

        /// <summary>
        /// This is temporary code to avoid any authentication on OPDS feeds. After debugging, setup a proper claims handle
        /// </summary>
        /// <returns></returns>
        private async Task<AppUser> GetUser(string apiKey = "")
        {
            return await _unitOfWork.UserRepository.GetUserByIdAsync(1);
            //return await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
        }

        private FeedLink CreatePageStreamLink(int chapterId, MangaFile mangaFile)
        {
            var link = CreateLink(FeedLinkRelation.Stream, "image/jpeg", $"{Prefix}image?chapterId={chapterId}&page=" + "{pageNumber}");
            //link.TotalPages = mangaFile.Pages;
            return link;
        }

        private FeedLink CreateLink(string rel, string type, string href)
        {
            return new FeedLink()
            {
                Rel = rel,
                Href = href,
                Type = type
            };
        }

        private Feed CreateFeed(string title, string href)
        {
            FeedLink link = null;
            if (string.IsNullOrEmpty(href))
            {
                link = CreateLink(FeedLinkRelation.Self, FeedLinkType.AtomNavigation, Prefix + href);
            }
            else
            {
                link = CreateLink(FeedLinkRelation.Self, FeedLinkType.AtomAcquisition, Prefix + href);
            }

            return new Feed()
            {
                Title = title,
                Links = new List<FeedLink>()
                {
                    link,
                    CreateLink(FeedLinkRelation.Start, FeedLinkType.AtomNavigation, Prefix),
                    CreateLink(FeedLinkRelation.Search, FeedLinkType.AtomSearch, Prefix + "search")
                },
            };
        }

        private string SerializeXml(Feed feed)
        {
            if (feed == null) return string.Empty;
            // var settings = new XmlWriterSettings
            // {
            //     OmitXmlDeclaration = true,
            //     Indent = true
            // };
            using var sm = new StringWriter();

            //using var xmlWriter = XmlWriter.Create(sm, settings);


            using var xmlWriter = new XmlTextWriter(sm);
            _xmlSerializer.Serialize(sm, feed);
            return sm.ToString();
        }
    }
}
