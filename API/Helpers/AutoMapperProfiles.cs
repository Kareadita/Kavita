using System;
using System.Collections.Generic;
using System.Linq;
using API.Data.Migrations;
using API.DTOs;
using API.DTOs.Account;
using API.DTOs.Collection;
using API.DTOs.CollectionTags;
using API.DTOs.Dashboard;
using API.DTOs.Device;
using API.DTOs.Email;
using API.DTOs.Filtering;
using API.DTOs.Filtering.v2;
using API.DTOs.KavitaPlus.Manage;
using API.DTOs.KavitaPlus.Metadata;
using API.DTOs.MediaErrors;
using API.DTOs.Metadata;
using API.DTOs.Progress;
using API.DTOs.Reader;
using API.DTOs.ReadingLists;
using API.DTOs.Recommendation;
using API.DTOs.Scrobbling;
using API.DTOs.Search;
using API.DTOs.SeriesDetail;
using API.DTOs.Settings;
using API.DTOs.SideNav;
using API.DTOs.Stats;
using API.DTOs.Theme;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Entities.Scrobble;
using API.Extensions.QueryExtensions.Filtering;
using API.Helpers.Converters;
using API.Services;
using AutoMapper;
using CollectionTag = API.Entities.CollectionTag;
using EmailHistory = API.Entities.EmailHistory;
using ExternalSeriesMetadata = API.Entities.Metadata.ExternalSeriesMetadata;
using MediaError = API.Entities.MediaError;
using PublicationStatus = API.Entities.Enums.PublicationStatus;
using SiteTheme = API.Entities.SiteTheme;

namespace API.Helpers;

public class AutoMapperProfiles : Profile
{
    public AutoMapperProfiles()
    {
        CreateMap<BookmarkSeriesPair, BookmarkDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Bookmark.Id))
            .ForMember(dest => dest.Page, opt => opt.MapFrom(src => src.Bookmark.Page))
            .ForMember(dest => dest.VolumeId, opt => opt.MapFrom(src => src.Bookmark.VolumeId))
            .ForMember(dest => dest.SeriesId, opt => opt.MapFrom(src => src.Bookmark.SeriesId))
            .ForMember(dest => dest.ChapterId, opt => opt.MapFrom(src => src.Bookmark.ChapterId))
            .ForMember(dest => dest.Series, opt => opt.MapFrom(src => src.Series));
        CreateMap<LibraryDto, Library>();
        CreateMap<Volume, VolumeDto>()
            .ForMember(dest => dest.Number,
                opt => opt.MapFrom(src => (int) src.MinNumber))
            .ForMember(dest => dest.Chapters,
                opt => opt.MapFrom(src => src.Chapters.OrderBy(c => c.SortOrder)));
        CreateMap<MangaFile, MangaFileDto>();
        CreateMap<Series, SeriesDto>();
        CreateMap<CollectionTag, CollectionTagDto>();
        CreateMap<AppUserCollection, AppUserCollectionDto>()
            .ForMember(dest => dest.Owner, opt => opt.MapFrom(src => src.AppUser.UserName))
            .ForMember(dest => dest.ItemCount, opt => opt.MapFrom(src => src.Items.Count));
        CreateMap<Person, PersonDto>();
        CreateMap<Genre, GenreTagDto>();
        CreateMap<Tag, TagDto>();
        CreateMap<AgeRating, AgeRatingDto>();
        CreateMap<PublicationStatus, PublicationStatusDto>();
        CreateMap<MediaError, MediaErrorDto>();
        CreateMap<AppUserExternalSource, ExternalSourceDto>();
        CreateMap<ScrobbleHold, ScrobbleHoldDto>()
            .ForMember(dest => dest.LibraryId,
                opt =>
                    opt.MapFrom(src => src.Series.LibraryId))
            .ForMember(dest => dest.SeriesName,
                opt =>
                    opt.MapFrom(src => src.Series.Name));

        CreateMap<ScrobbleEvent, ScrobbleEventDto>()
            .ForMember(dest => dest.SeriesName,
            opt =>
                opt.MapFrom(src => src.Series.Name));
        CreateMap<AppUserRating, UserReviewDto>()
            .ForMember(dest => dest.LibraryId,
                opt =>
                    opt.MapFrom(src => src.Series.LibraryId))
            .ForMember(dest => dest.Body,
                opt =>
                    opt.MapFrom(src => src.Review))
            .ForMember(dest => dest.Username,
                opt =>
                    opt.MapFrom(src => src.AppUser.UserName));

        CreateMap<AppUserProgress, ProgressDto>()
            .ForMember(dest => dest.PageNum,
                opt =>
                    opt.MapFrom(
                        src => src.PagesRead));

        CreateMap<SeriesMetadata, SeriesMetadataDto>()
            // Map Writers
            .ForMember(dest => dest.Writers, opt => opt.MapFrom(src => src.People
                .Where(cp => cp.Role == PersonRole.Writer)
                .Select(cp => cp.Person)
                .OrderBy(p => p.NormalizedName)))
            // Map CoverArtists
            .ForMember(dest => dest.CoverArtists, opt => opt.MapFrom(src => src.People
                .Where(cp => cp.Role == PersonRole.CoverArtist)
                .Select(cp => cp.Person)
                .OrderBy(p => p.NormalizedName)))
            // Map Publishers
            .ForMember(dest => dest.Publishers, opt => opt.MapFrom(src => src.People
                .Where(cp => cp.Role == PersonRole.Publisher)
                .Select(cp => cp.Person)
                .OrderBy(p => p.NormalizedName)))
            // Map Characters
            .ForMember(dest => dest.Characters, opt => opt.MapFrom(src => src.People
                .Where(cp => cp.Role == PersonRole.Character)
                .OrderBy(cp => cp.OrderWeight)
                .Select(cp => cp.Person)))
            // Map Pencillers
            .ForMember(dest => dest.Pencillers, opt => opt.MapFrom(src => src.People
                .Where(cp => cp.Role == PersonRole.Penciller)
                .Select(cp => cp.Person)
                .OrderBy(p => p.NormalizedName)))
            // Map Inkers
            .ForMember(dest => dest.Inkers, opt => opt.MapFrom(src => src.People
                .Where(cp => cp.Role == PersonRole.Inker)
                .Select(cp => cp.Person)
                .OrderBy(p => p.NormalizedName)))
            // Map Imprints
            .ForMember(dest => dest.Imprints, opt => opt.MapFrom(src => src.People
                .Where(cp => cp.Role == PersonRole.Imprint)
                .Select(cp => cp.Person)
                .OrderBy(p => p.NormalizedName)))
            // Map Colorists
            .ForMember(dest => dest.Colorists, opt => opt.MapFrom(src => src.People
                .Where(cp => cp.Role == PersonRole.Colorist)
                .Select(cp => cp.Person)
                .OrderBy(p => p.NormalizedName)))
            // Map Letterers
            .ForMember(dest => dest.Letterers, opt => opt.MapFrom(src => src.People
                .Where(cp => cp.Role == PersonRole.Letterer)
                .Select(cp => cp.Person)
                .OrderBy(p => p.NormalizedName)))
            // Map Editors
            .ForMember(dest => dest.Editors, opt => opt.MapFrom(src => src.People
                .Where(cp => cp.Role == PersonRole.Editor)
                .Select(cp => cp.Person)
                .OrderBy(p => p.NormalizedName)))
            // Map Translators
            .ForMember(dest => dest.Translators, opt => opt.MapFrom(src => src.People
                .Where(cp => cp.Role == PersonRole.Translator)
                .Select(cp => cp.Person)
                .OrderBy(p => p.NormalizedName)))
            // Map Teams
            .ForMember(dest => dest.Teams, opt => opt.MapFrom(src => src.People
                .Where(cp => cp.Role == PersonRole.Team)
                .Select(cp => cp.Person)
                .OrderBy(p => p.NormalizedName)))
            // Map Locations
            .ForMember(dest => dest.Locations, opt => opt.MapFrom(src => src.People
                .Where(cp => cp.Role == PersonRole.Location)
                .Select(cp => cp.Person)
                .OrderBy(p => p.NormalizedName)))
            .ForMember(dest => dest.Genres,
                opt =>
                    opt.MapFrom(
                        src => src.Genres.OrderBy(p => p.NormalizedTitle)))
            .ForMember(dest => dest.Tags,
                opt =>
                    opt.MapFrom(
                        src => src.Tags.OrderBy(p => p.NormalizedTitle)));

        CreateMap<Chapter, ChapterDto>()
            // Map Writers
            .ForMember(dest => dest.Writers, opt => opt.MapFrom(src => src.People
                .Where(cp => cp.Role == PersonRole.Writer)
                .Select(cp => cp.Person)
                .OrderBy(p => p.NormalizedName)))
            // Map CoverArtists
            .ForMember(dest => dest.CoverArtists, opt => opt.MapFrom(src => src.People
                .Where(cp => cp.Role == PersonRole.CoverArtist)
                .Select(cp => cp.Person)
                .OrderBy(p => p.NormalizedName)))
            // Map Publishers
            .ForMember(dest => dest.Publishers, opt => opt.MapFrom(src => src.People
                .Where(cp => cp.Role == PersonRole.Publisher)
                .Select(cp => cp.Person)
                .OrderBy(p => p.NormalizedName)))
            // Map Characters
            .ForMember(dest => dest.Characters, opt => opt.MapFrom(src => src.People
                .Where(cp => cp.Role == PersonRole.Character)
                .Select(cp => cp.Person)
                .OrderBy(p => p.NormalizedName)))
            // Map Pencillers
            .ForMember(dest => dest.Pencillers, opt => opt.MapFrom(src => src.People
                .Where(cp => cp.Role == PersonRole.Penciller)
                .Select(cp => cp.Person)
                .OrderBy(p => p.NormalizedName)))
            // Map Inkers
            .ForMember(dest => dest.Inkers, opt => opt.MapFrom(src => src.People
                .Where(cp => cp.Role == PersonRole.Inker)
                .Select(cp => cp.Person)
                .OrderBy(p => p.NormalizedName)))
            // Map Imprints
            .ForMember(dest => dest.Imprints, opt => opt.MapFrom(src => src.People
                .Where(cp => cp.Role == PersonRole.Imprint)
                .Select(cp => cp.Person)
                .OrderBy(p => p.NormalizedName)))
            // Map Colorists
            .ForMember(dest => dest.Colorists, opt => opt.MapFrom(src => src.People
                .Where(cp => cp.Role == PersonRole.Colorist)
                .Select(cp => cp.Person)
                .OrderBy(p => p.NormalizedName)))
            // Map Letterers
            .ForMember(dest => dest.Letterers, opt => opt.MapFrom(src => src.People
                .Where(cp => cp.Role == PersonRole.Letterer)
                .Select(cp => cp.Person)
                .OrderBy(p => p.NormalizedName)))
            // Map Editors
            .ForMember(dest => dest.Editors, opt => opt.MapFrom(src => src.People
                .Where(cp => cp.Role == PersonRole.Editor)
                .Select(cp => cp.Person)
                .OrderBy(p => p.NormalizedName)))
            // Map Translators
            .ForMember(dest => dest.Translators, opt => opt.MapFrom(src => src.People
                .Where(cp => cp.Role == PersonRole.Translator)
                .Select(cp => cp.Person)
                .OrderBy(p => p.NormalizedName)))
            // Map Teams
            .ForMember(dest => dest.Teams, opt => opt.MapFrom(src => src.People
                .Where(cp => cp.Role == PersonRole.Team)
                .Select(cp => cp.Person)
                .OrderBy(p => p.NormalizedName)))
            // Map Locations
            .ForMember(dest => dest.Locations, opt => opt.MapFrom(src => src.People
                .Where(cp => cp.Role == PersonRole.Location)
                .Select(cp => cp.Person)
                .OrderBy(p => p.NormalizedName)));


        CreateMap<AppUser, UserDto>()
            .ForMember(dest => dest.AgeRestriction,
                opt =>
                    opt.MapFrom(src => new AgeRestrictionDto()
                    {
                        AgeRating = src.AgeRestriction,
                        IncludeUnknowns = src.AgeRestrictionIncludeUnknowns
                    }));

        CreateMap<SiteTheme, SiteThemeDto>()
            .ForMember(dest => dest.PreviewUrls,
                opt =>
                    opt.MapFrom(src => (src.PreviewUrls ?? string.Empty).Split('|', StringSplitOptions.TrimEntries)));
        CreateMap<AppUserPreferences, UserPreferencesDto>()
            .ForMember(dest => dest.Theme,
                opt =>
                    opt.MapFrom(src => src.Theme))
            .ForMember(dest => dest.BookReaderThemeName,
                opt =>
                    opt.MapFrom(src => src.BookThemeName))
            .ForMember(dest => dest.BookReaderLayoutMode,
                opt =>
                    opt.MapFrom(src => src.BookReaderLayoutMode));


        CreateMap<AppUserBookmark, BookmarkDto>();

        CreateMap<ReadingList, ReadingListDto>()
            .ForMember(dest => dest.ItemCount, opt => opt.MapFrom(src => src.Items.Count));
        CreateMap<ReadingListItem, ReadingListItemDto>();
        CreateMap<ScrobbleError, ScrobbleErrorDto>();
        CreateMap<ChapterDto, TachiyomiChapterDto>();
        CreateMap<Chapter, TachiyomiChapterDto>();

        CreateMap<Series, SearchResultDto>()
            .ForMember(dest => dest.SeriesId,
                opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.LibraryName,
                opt => opt.MapFrom(src => src.Library.Name));


        CreateMap<Library, LibraryDto>()
            .ForMember(dest => dest.Folders,
                opt =>
                    opt.MapFrom(src => src.Folders.Select(x => x.Path).ToList()))
            .ForMember(dest => dest.LibraryFileTypes,
                opt =>
                    opt.MapFrom(src => src.LibraryFileTypes.Select(l => l.FileTypeGroup)))
            .ForMember(dest => dest.ExcludePatterns,
                opt =>
                    opt.MapFrom(src => src.LibraryExcludePatterns.Select(l => l.Pattern)));

        CreateMap<AppUser, MemberDto>()
            .ForMember(dest => dest.AgeRestriction,
                opt =>
                    opt.MapFrom(src => new AgeRestrictionDto()
                    {
                        AgeRating = src.AgeRestriction,
                        IncludeUnknowns = src.AgeRestrictionIncludeUnknowns
                    }))
            .AfterMap((ps, pst, context) => context.Mapper.Map(ps.Libraries, pst.Libraries));

        CreateMap<RegisterDto, AppUser>();

        CreateMap<IList<ServerSetting>, ServerSettingDto>()
            .ConvertUsing<ServerSettingConverter>();

        CreateMap<IEnumerable<ServerSetting>, ServerSettingDto>()
            .ConvertUsing<ServerSettingConverter>();

        CreateMap<Device, DeviceDto>();
        CreateMap<AppUserTableOfContent, PersonalToCDto>();


        CreateMap<AppUserSmartFilter, SmartFilterDto>();
        CreateMap<AppUserDashboardStream, DashboardStreamDto>();

        // This is for cloning to ensure the records don't get overwritten when setting from SeedData
        CreateMap<AppUserDashboardStream, AppUserDashboardStream>();
        CreateMap<AppUserSideNavStream, AppUserSideNavStream>();

        CreateMap<ExternalRating, RatingDto>();
        CreateMap<RatingDto, ExternalRating>();
        CreateMap<ExternalReview, UserReviewDto>()
            .ForMember(dest => dest.IsExternal,
                opt =>
                    opt.MapFrom(src => true));

        CreateMap<UserReviewDto, ExternalReview>()
            .ForMember(dest => dest.BodyJustText,
                opt =>
                    opt.MapFrom(src => ReviewService.GetCharacters(src.Body)));

        CreateMap<ExternalRecommendation, ExternalSeriesDto>();
        CreateMap<Series, ManageMatchSeriesDto>()
            .ForMember(dest => dest.Series,
                opt =>
                    opt.MapFrom(src => src))
            .ForMember(dest => dest.IsMatched,
                opt =>
                    opt.MapFrom(src => src.ExternalSeriesMetadata != null && src.ExternalSeriesMetadata.AniListId != 0
                                                                          && src.ExternalSeriesMetadata.ValidUntilUtc > DateTime.MinValue))
            .ForMember(dest => dest.ValidUntilUtc,
                opt => opt.MapFrom(src =>
                    src.ExternalSeriesMetadata != null
                        ? src.ExternalSeriesMetadata.ValidUntilUtc
                        : DateTime.MinValue));


        CreateMap<MangaFile, FileExtensionExportDto>();
        CreateMap<EmailHistory, EmailHistoryDto>()
            .ForMember(dest => dest.ToUserName, opt => opt.MapFrom(src => src.AppUser.UserName));

        CreateMap<Chapter, StandaloneChapterDto>()
            .ForMember(dest => dest.SeriesId, opt => opt.MapFrom(src => src.Volume.SeriesId))
            .ForMember(dest => dest.VolumeTitle, opt => opt.MapFrom(src => src.Volume.Name))
            .ForMember(dest => dest.LibraryId, opt => opt.MapFrom(src => src.Volume.Series.LibraryId))
            .ForMember(dest => dest.LibraryType, opt => opt.MapFrom(src => src.Volume.Series.Library.Type));

        CreateMap<MetadataFieldMapping, MetadataFieldMappingDto>();

        CreateMap<MetadataSettings, MetadataSettingsDto>()
            .ForMember(dest => dest.Blacklist, opt => opt.MapFrom(src => src.Blacklist ?? new List<string>()))
            .ForMember(dest => dest.Whitelist, opt => opt.MapFrom(src => src.Whitelist ?? new List<string>()))
            .ForMember(dest => dest.Overrides, opt => opt.MapFrom(src => src.Overrides ?? new List<MetadataSettingField>()))
            .ForMember(dest => dest.AgeRatingMappings, opt => opt.MapFrom(src => src.AgeRatingMappings ?? new Dictionary<string, AgeRating>()));



    }
}
