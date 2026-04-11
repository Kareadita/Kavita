using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Kavita.Common.Helpers;
using Kavita.Models.AutoMapper.Converters;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.Account;
using Kavita.Models.DTOs.Annotations;
using Kavita.Models.DTOs.Collection;
using Kavita.Models.DTOs.Dashboard;
using Kavita.Models.DTOs.Device.EmailDevice;
using Kavita.Models.DTOs.Email;
using Kavita.Models.DTOs.Font;
using Kavita.Models.DTOs.KavitaPlus.Manage;
using Kavita.Models.DTOs.KavitaPlus.Metadata;
using Kavita.Models.DTOs.MediaErrors;
using Kavita.Models.DTOs.Metadata;
using Kavita.Models.DTOs.Person;
using Kavita.Models.DTOs.Progress;
using Kavita.Models.DTOs.Reader;
using Kavita.Models.DTOs.ReadingLists;
using Kavita.Models.DTOs.ReadingLists.CBL.RemapRules;
using Kavita.Models.DTOs.Recommendation;
using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.DTOs.Search;
using Kavita.Models.DTOs.SeriesDetail;
using Kavita.Models.DTOs.Settings;
using Kavita.Models.DTOs.SideNav;
using Kavita.Models.DTOs.Stats;
using Kavita.Models.DTOs.Theme;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Metadata;
using Kavita.Models.Entities.MetadataMatching;
using Kavita.Models.Entities.Person;
using Kavita.Models.Entities.Progress;
using Kavita.Models.Entities.ReadingLists;
using Kavita.Models.Entities.Scrobble;
using Kavita.Models.Entities.User;

namespace Kavita.Models.AutoMapper;

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
        CreateMap<MangaFile, MangaFileDto>();
        CreateMap<Series, SeriesDto>();
        CreateMap<AppUserCollection, AppUserCollectionDto>()
            .ForMember(dest => dest.Owner, opt => opt.MapFrom(src => src.AppUser.UserName))
            .ForMember(dest => dest.ItemCount, opt => opt.MapFrom(src => src.Items.Count));
        CreateMap<Person, PersonDto>()
            .ForMember(dest => dest.Aliases, opt => opt.MapFrom(src => src.Aliases.Select(s => s.Alias)));
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
            .ForMember(dest => dest.UserId,
                opt =>
                    opt.MapFrom(src => src.AppUser.Id))
            .ForMember(dest => dest.Username,
                opt =>
                    opt.MapFrom(src => src.AppUser.UserName));
        CreateMap<AppUserChapterRating, UserReviewDto>()
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

        CreateMap<AppUser, UserDto>()
            .ForMember(dest => dest.Roles,
                opt =>
                    opt.MapFrom(src => src.UserRoles.Select(r => r.Role.Name)))
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
                    opt.MapFrom(src => src.Theme));

        CreateMap<AppUserReadingProfile, UserReadingProfileDto>()
            .ForMember(dest => dest.BookReaderThemeName,
                opt =>
                    opt.MapFrom(src => src.BookThemeName));

        CreateMap<EpubFont, EpubFontDto>();


        CreateMap<AppUserBookmark, BookmarkDto>();

        CreateMap<ReadingListTag, ReadingListTagDto>();
        CreateMap<ReadingList, ReadingListDto>()
            .ForMember(dest => dest.ItemCount, opt => opt.MapFrom(src => src.Items.Count))
            .ForMember(dest => dest.OwnerUserName, opt => opt.MapFrom(src => src.AppUser.UserName))
            .ForMember(dest => dest.Tags, opt => opt.MapFrom(src => src.Tags));
        CreateMap<ScrobbleError, ScrobbleErrorDto>();
        CreateMap<ChapterDto, TachiyomiChapterDto>();
        CreateMap<Chapter, TachiyomiChapterDto>();

        CreateMap<Series, SearchResultDto>()
            .ForMember(dest => dest.SeriesId,
                opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.LibraryName,
                opt => opt.MapFrom(src => src.Library.Name))
            .ForMember(dest => dest.ReleaseYear,
                opt => opt.MapFrom(src => src.Metadata.ReleaseYear))
            .ForMember(dest => dest.VolumeCount,
                opt => opt.MapFrom(src => src.Volumes.Count))
            .ForMember(dest => dest.ChapterCount,
                opt => opt.MapFrom(src => src.Volumes.SelectMany(v => v.Chapters).Count()));

        CreateMap<Library, LiteLibraryDto>();
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
        CreateMap<AppUser, MemberInfoDto>();

        CreateMap<IList<ServerSetting>, ServerSettingDto>()
            .ConvertUsing<ServerSettingConverter>();

        CreateMap<IEnumerable<ServerSetting>, ServerSettingDto>()
            .ConvertUsing<ServerSettingConverter>();

        CreateMap<Device, EmailDeviceDto>();
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
                    opt.MapFrom(src => HtmlHelper.GetCharacters(src.Body)));

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

        CreateMap<MetadataFieldMapping, MetadataFieldMappingDto>();

        CreateMap<MetadataSettings, MetadataSettingsDto>()
            .ForMember(dest => dest.Blacklist, opt => opt.MapFrom(src => src.Blacklist ?? new List<string>()))
            .ForMember(dest => dest.Whitelist, opt => opt.MapFrom(src => src.Whitelist ?? new List<string>()))
            .ForMember(dest => dest.Overrides, opt => opt.MapFrom(src => src.Overrides ?? new List<MetadataSettingField>()))
            .ForMember(dest => dest.AgeRatingMappings, opt => opt.MapFrom(src => src.AgeRatingMappings ?? new Dictionary<string, AgeRating>()));

        CreateMap<AppUserAnnotation, AnnotationDto>()
            .ForMember(dest => dest.OwnerUsername, opt => opt.MapFrom(src => src.AppUser.UserName))
            .ForMember(dest => dest.OwnerUserId, opt => opt.MapFrom(src => src.AppUserId))
            .ForMember(dest => dest.SeriesName, opt => opt.MapFrom(src => src.Series.Name))
            .ForMember(dest => dest.LibraryName, opt => opt.MapFrom(src => src.Library.Name))
            .ForMember(dest => dest.AgeRating, opt => opt.MapFrom(src => src.Series.Metadata.AgeRating));

        CreateMap<AppUserAnnotation, FullAnnotationDto>()
            .ForMember(dest => dest.SeriesName, opt => opt.MapFrom(src => src.Series.Name))
            .ForMember(dest => dest.VolumeName, opt => opt.MapFrom(src => src.Chapter.Volume.Name))
            .ForMember(dest => dest.LibraryName, opt => opt.MapFrom(src => src.Library.Name))
            .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.AppUserId));

        CreateMap<OidcConfigDto, OidcPublicConfigDto>();

        CreateMap<AppUserReadingSessionActivityData, ReadingActivityDataDto>();
        CreateMap<ClientInfoData, ClientInfoDto>();
        CreateMap<AppUserReadingSession, ReadingSessionDto>()
            .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.AppUserId))
            .ForMember(dest => dest.Username, opt => opt.MapFrom(src => src.AppUser.UserName));

        CreateMap<ClientDevice, ClientDeviceDto>()
            .ForMember(dest => dest.OwnerUserId, opt => opt.MapFrom(src => src.AppUserId))
            .ForMember(dest => dest.OwnerUsername, opt => opt.MapFrom(src => src.AppUser.UserName));

        CreateMap<ReadingListRemapRule, RemapRuleDto>()
            .ForMember(dest => dest.CreatedByUserName,
                opt => opt.MapFrom(src => src.AppUser != null ? src.AppUser.UserName : string.Empty))
            .ForMember(dest => dest.ChapterRange,
                opt => opt.MapFrom(src => src.Chapter != null ? src.Chapter.Range : string.Empty))
            .ForMember(dest => dest.ChapterTitleName,
                opt => opt.MapFrom(src => src.Chapter != null ? src.Chapter.TitleName : string.Empty))
            .ForMember(dest => dest.ChapterIsSpecial,
                opt => opt.MapFrom(src => src.Chapter != null && src.Chapter.IsSpecial))
            .ForMember(dest => dest.LibraryType,
                opt => opt.MapFrom(src => src.Series.Library != null ? src.Series.Library.Type : LibraryType.Comic))
            .ForMember(dest => dest.VolumeNumber,
                opt => opt.MapFrom(src => src.Volume != null ? src.Volume.Name : string.Empty))
            .ForMember(dest => dest.Kind,
                opt => opt.MapFrom(src => src.GetKind()));

        CreateMap<AppUserRating, UserReviewExtendedDto>()
            .ForMember(dest => dest.Body,
            opt => opt.MapFrom(src => src.Review))
            .ForMember(dest => dest.SeriesId,
            opt => opt.MapFrom(src => src.SeriesId))
            .ForMember(dest => dest.ChapterId,
            opt => opt.MapFrom(src => (int?)null))
            .ForMember(dest => dest.LibraryId,
            opt => opt.MapFrom(src => src.Series.LibraryId))
            .ForMember(dest => dest.Username,
            opt => opt.MapFrom(src => src.AppUser.UserName))
            .ForMember(dest => dest.Rating,
            opt => opt.MapFrom(src => src.Rating))
            .ForMember(dest => dest.Series,
            opt => opt.MapFrom(src => src.Series))
            .ForMember(dest => dest.Writers,
                opt =>
                    opt.MapFrom(src =>
                        src.Series.Metadata.People
                            .Where(p => p.Role == PersonRole.Writer)
                                .OrderBy(p => p.OrderWeight)
                                .Select(p => p.Person))
                    )
            .ForMember(dest => dest.Chapter,
            opt => opt.MapFrom(src => (ChapterDto?)null));

        // Map from AppUserChapterRating (chapter-level reviews)
        CreateMap<AppUserChapterRating, UserReviewExtendedDto>()
            .ForMember(dest => dest.Body,
            opt => opt.MapFrom(src => src.Review))
            .ForMember(dest => dest.SeriesId,
            opt => opt.MapFrom(src => src.SeriesId))
            .ForMember(dest => dest.ChapterId,
            opt => opt.MapFrom(src => src.ChapterId))
            .ForMember(dest => dest.LibraryId,
            opt => opt.MapFrom(src => src.Series.LibraryId))
            .ForMember(dest => dest.Username,
                opt => opt.MapFrom(src => src.AppUser.UserName))
            .ForMember(dest => dest.Rating,
                opt => opt.MapFrom(src => src.Rating))
            .ForMember(dest => dest.Series,
                opt => opt.MapFrom(src => src.Series))
            .ForMember(dest => dest.Writers,
                opt => opt.MapFrom(src =>
                    src.Chapter.People
                        .Where(p => p.Role == PersonRole.Writer)
                        .OrderBy(p => p.OrderWeight)
                        .Select(p => p.Person))
                )
            .ForMember(dest => dest.Chapter,
                opt => opt.MapFrom(src => src.Chapter));

        CreateMap<AppUserAuthKey, AuthKeyDto>();


        #region Deprecated Code

        CreateMap<Chapter, ChapterMetadataDto>();

        #endregion

    }
}
