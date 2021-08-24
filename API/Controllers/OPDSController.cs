using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Serialization;
using API.DTOs.Filtering;
using API.DTOs.OPDS;
using API.Entities;
using API.Extensions;
using API.Helpers;
using API.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Sentry;

namespace API.Controllers
{
    public class OpdsController : BaseApiController
    {
        private readonly IUnitOfWork _unitOfWork;

        private readonly XmlSerializer _xmlSerializer = new XmlSerializer(typeof(Feed));
        private const string Prefix = "/api/opds/";
        private const string DefaultContentType = "application/octet-stream";

        public OpdsController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        [HttpGet]
        [Produces("application/xml")]
        public IActionResult Get()
        {
            var feed = CreateFeed("Kavita", string.Empty);
            feed.Id = "root";
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
            return new ContentResult
            {
                ContentType = "application/xml",
                Content = SerializeXml(feed),
                StatusCode = 200
            };
        }

        [HttpGet("libraries")]
        [Produces("application/xml")]
        public async Task<IActionResult> GetLibraries()
        {
            var user = GetUser();
            var libraries = await _unitOfWork.LibraryRepository.GetLibrariesForUserIdAsync(user.Id);

            var feed = CreateFeed("Recently Added", "recently-added");

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


            return new ContentResult
            {
                ContentType = "application/xml",
                Content = SerializeXml(feed),
                StatusCode = 200
            };
        }

        [HttpGet("libraries/{libraryId}")]
        [Produces("application/xml")]
        public async Task<IActionResult> GetSeriesForLibrary(int libraryId)
        {
            //var user = await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
            var user = await GetUser();
            var series = await _unitOfWork.SeriesRepository.GetSeriesForLibraryIdAsync(libraryId); // GetSeriesDtoForLibraryIdAsync

            var feed = CreateFeed("Recently Added", "recently-added");

            foreach (var seriesDto in series)
            {
                feed.Entries.Add(new FeedEntry()
                {
                    Id = seriesDto.Id.ToString(),
                    Title = seriesDto.Name,
                    Links = new List<FeedLink>()
                    {
                        CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation,  $"{Prefix}/series/{seriesDto.Id}"),
                        CreateLink(FeedLinkRelation.Image, FeedLinkType.Image, $"../image/series-cover?seriesId={seriesDto.Id}")
                    }
                });
            }


            return new ContentResult
            {
                ContentType = "application/xml",
                Content = SerializeXml(feed),
                StatusCode = 200
            };
        }

        [HttpGet("recently-added")]
        [Produces("application/xml")]
        public async Task<IActionResult> GetRecentlyAdded([FromQuery] int pageNumber = 1)
        {
            var user = await GetUser();
            var recentlyAdded = await _unitOfWork.SeriesRepository.GetRecentlyAdded(0, user.Id, new UserParams()
            {
                PageNumber = pageNumber,
                PageSize = 20
            }, new FilterDto()
            {
                MangaFormat = null
            });

            var feed = CreateFeed("Recently Added", "recently-added");

            foreach (var seriesDto in recentlyAdded)
            {
                feed.Entries.Add(new FeedEntry()
                {
                    Id = seriesDto.Id.ToString(),
                    Title = seriesDto.Name,
                    Links = new List<FeedLink>()
                    {
                        CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, Prefix + $"series/{seriesDto.Id}"),
                        CreateLink(FeedLinkRelation.Image, FeedLinkType.Image, $"../image/series-cover?seriesId={seriesDto.Id}")
                    }
                });
            }


            return new ContentResult
            {
                ContentType = "application/xml",
                Content = SerializeXml(feed),
                StatusCode = 200
            };
        }


        [HttpGet("series/{seriesId}")]
        [Produces("application/xml")]
        public async Task<ContentResult> GetSeries(int seriesId)
        {
            var user = await GetUser();
            var series = await _unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(seriesId, user.Id);
            var volumes = await _unitOfWork.SeriesRepository.GetVolumesDtoAsync(seriesId, user.Id);
            var feed = CreateFeed(series.Name + " - Volumes", $"series/{series.Id}");
            feed.Links.Add(CreateLink(FeedLinkRelation.Image, FeedLinkType.Image, $"../image/series-cover?seriesId={seriesId}"));
            foreach (var volumeDto in volumes)
            {
                feed.Entries.Add(new FeedEntry()
                {
                    Id = volumeDto.Id.ToString(),
                    Title = "Volume " + volumeDto.Name,
                    Links = new List<FeedLink>()
                    {
                        CreateLink(FeedLinkRelation.SubSection, FeedLinkType.AtomNavigation, Prefix + $"series/{seriesId}/volume/{volumeDto.Id}"),
                        CreateLink(FeedLinkRelation.Image, FeedLinkType.Image, $"../image/volume-cover?volumeId={volumeDto.Id}")
                    }
                });
            }

            return new ContentResult
            {
                ContentType = "application/xml",
                Content = SerializeXml(feed),
                StatusCode = 200
            };
        }

        [HttpGet("series/{seriesId}/volume/{volumeId}")]
        [Produces("application/xml")]
        public async Task<ContentResult> GetVolume(int seriesId, int volumeId)
        {
            var user = await GetUser();
            var series = await _unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(seriesId, user.Id);
            var volume = await _unitOfWork.SeriesRepository.GetVolumeAsync(volumeId);
            var chapters = await _unitOfWork.VolumeRepository.GetChaptersAsync(volumeId);

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
                        CreateLink(FeedLinkRelation.Image, FeedLinkType.Image, $"../image/chapter-cover?chapterId={chapter.Id}")
                    }
                });
            }

            return new ContentResult
            {
                ContentType = "application/xml",
                Content = SerializeXml(feed),
                StatusCode = 200
            };
        }


        [HttpGet("series/{seriesId}/volume/{volumeId}/chapter/{chapterId}")]
        [Produces("application/xml")]
        public async Task<ContentResult> GetChapter(int seriesId, int volumeId, int chapterId)
        {
            var user = await GetUser();
            var series = await _unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(seriesId, user.Id);
            var volume = await _unitOfWork.SeriesRepository.GetVolumeAsync(volumeId);
            var chapter = await _unitOfWork.VolumeRepository.GetChapterDtoAsync(chapterId);
            var files = await _unitOfWork.VolumeRepository.GetFilesForChapterAsync(chapterId);

            var feed = CreateFeed(series.Name + " - Volume " + volume.Name + " - Chapters ", $"series/{seriesId}/volume/{volumeId}/chapter/{chapterId}");
            foreach (var mangaFile in files)
            {
                feed.Entries.Add(new FeedEntry()
                {
                    Id = chapter.Id.ToString(),
                    Title = $"{series.Name} - Volume {volume.Name} - Chapter {chapter.Number}",
                    Links = new List<FeedLink>()
                    {
                        CreateLink(FeedLinkRelation.Image, FeedLinkType.Image, $"../image/chapter-cover?chapterId={chapter.Id}"),
                        CreateLink(DefaultContentType, FeedLinkType.AtomAcquisition, "../download/")
                    },
                    Content = new FeedEntryContent()
                    {
                        Text = Path.GetFileNameWithoutExtension(mangaFile.FilePath),
                        Type = mangaFile.Format.ToString()
                    }
                });
            }

            return new ContentResult
            {
                ContentType = "application/xml",
                Content = SerializeXml(feed),
                StatusCode = 200
            };
        }

        /// <summary>
        /// This is temporary code to avoid any authentication on OPDS feeds. After debugging, setup a proper claimshandle
        /// </summary>
        /// <returns></returns>
        private async Task<AppUser> GetUser()
        {
            return await _unitOfWork.UserRepository.GetUserByIdAsync(1);
            //return await _unitOfWork.UserRepository.GetUserByUsernameAsync(User.GetUsername());
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
                },
            };
        }

        private string SerializeXml(Feed feed)
        {
            if (feed == null) return string.Empty;

            using var sm = new StringWriter();
            _xmlSerializer.Serialize(sm, feed);
            return sm.ToString();
        }
    }
}
