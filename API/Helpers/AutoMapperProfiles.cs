using System.Collections.Generic;
using System.Linq;
using API.DTOs;
using API.DTOs.Account;
using API.DTOs.CollectionTags;
using API.DTOs.Device;
using API.DTOs.Metadata;
using API.DTOs.Reader;
using API.DTOs.ReadingLists;
using API.DTOs.Search;
using API.DTOs.Settings;
using API.DTOs.Theme;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Helpers.Converters;
using AutoMapper;

namespace API.Helpers;

public class AutoMapperProfiles : Profile
{
    public AutoMapperProfiles()
    {
        CreateMap<LibraryDto, Library>();
        CreateMap<Volume, VolumeDto>();
        CreateMap<MangaFile, MangaFileDto>();
        CreateMap<Chapter, ChapterDto>();
        CreateMap<Series, SeriesDto>();
        CreateMap<CollectionTag, CollectionTagDto>();
        CreateMap<Person, PersonDto>();
        CreateMap<Genre, GenreTagDto>();
        CreateMap<Tag, TagDto>();
        CreateMap<AgeRating, AgeRatingDto>();
        CreateMap<PublicationStatus, PublicationStatusDto>();

        CreateMap<SeriesMetadata, SeriesMetadataDto>()
            .ForMember(dest => dest.Writers,
                opt =>
                    opt.MapFrom(
                        src => src.People.Where(p => p.Role == PersonRole.Writer).OrderBy(p => p.NormalizedName)))
            .ForMember(dest => dest.CoverArtists,
                opt =>
                    opt.MapFrom(src =>
                        src.People.Where(p => p.Role == PersonRole.CoverArtist).OrderBy(p => p.NormalizedName)))
            .ForMember(dest => dest.Characters,
                opt =>
                    opt.MapFrom(src =>
                        src.People.Where(p => p.Role == PersonRole.Character).OrderBy(p => p.NormalizedName)))
            .ForMember(dest => dest.Publishers,
                opt =>
                    opt.MapFrom(src =>
                        src.People.Where(p => p.Role == PersonRole.Publisher).OrderBy(p => p.NormalizedName)))
            .ForMember(dest => dest.Colorists,
                opt =>
                    opt.MapFrom(src =>
                        src.People.Where(p => p.Role == PersonRole.Colorist).OrderBy(p => p.NormalizedName)))
            .ForMember(dest => dest.Inkers,
                opt =>
                    opt.MapFrom(src =>
                        src.People.Where(p => p.Role == PersonRole.Inker).OrderBy(p => p.NormalizedName)))
            .ForMember(dest => dest.Letterers,
                opt =>
                    opt.MapFrom(src =>
                        src.People.Where(p => p.Role == PersonRole.Letterer).OrderBy(p => p.NormalizedName)))
            .ForMember(dest => dest.Pencillers,
                opt =>
                    opt.MapFrom(src =>
                        src.People.Where(p => p.Role == PersonRole.Penciller).OrderBy(p => p.NormalizedName)))
            .ForMember(dest => dest.Translators,
                opt =>
                    opt.MapFrom(src =>
                        src.People.Where(p => p.Role == PersonRole.Translator).OrderBy(p => p.NormalizedName)))
            .ForMember(dest => dest.Editors,
                opt =>
                    opt.MapFrom(
                        src => src.People.Where(p => p.Role == PersonRole.Editor).OrderBy(p => p.NormalizedName)))
            .ForMember(dest => dest.Genres,
                opt =>
                    opt.MapFrom(
                        src => src.Genres.OrderBy(p => p.NormalizedTitle)))
            .ForMember(dest => dest.CollectionTags,
                opt =>
                    opt.MapFrom(
                        src => src.CollectionTags.OrderBy(p => p.NormalizedTitle)))
            .ForMember(dest => dest.Tags,
                opt =>
                    opt.MapFrom(
                        src => src.Tags.OrderBy(p => p.NormalizedTitle)));

        CreateMap<Chapter, ChapterMetadataDto>()
            .ForMember(dest => dest.Writers,
                opt =>
                    opt.MapFrom(src => src.People.Where(p => p.Role == PersonRole.Writer).OrderBy(p => p.NormalizedName)))
            .ForMember(dest => dest.CoverArtists,
                opt =>
                    opt.MapFrom(src => src.People.Where(p => p.Role == PersonRole.CoverArtist).OrderBy(p => p.NormalizedName)))
            .ForMember(dest => dest.Colorists,
                opt =>
                    opt.MapFrom(src => src.People.Where(p => p.Role == PersonRole.Colorist).OrderBy(p => p.NormalizedName)))
            .ForMember(dest => dest.Inkers,
                opt =>
                    opt.MapFrom(src => src.People.Where(p => p.Role == PersonRole.Inker).OrderBy(p => p.NormalizedName)))
            .ForMember(dest => dest.Letterers,
                opt =>
                    opt.MapFrom(src => src.People.Where(p => p.Role == PersonRole.Letterer).OrderBy(p => p.NormalizedName)))
            .ForMember(dest => dest.Pencillers,
                opt =>
                    opt.MapFrom(src => src.People.Where(p => p.Role == PersonRole.Penciller).OrderBy(p => p.NormalizedName)))
            .ForMember(dest => dest.Publishers,
                opt =>
                    opt.MapFrom(src => src.People.Where(p => p.Role == PersonRole.Publisher).OrderBy(p => p.NormalizedName)))
            .ForMember(dest => dest.Translators,
                opt =>
                    opt.MapFrom(src => src.People.Where(p => p.Role == PersonRole.Translator).OrderBy(p => p.NormalizedName)))
            .ForMember(dest => dest.Characters,
                opt =>
                    opt.MapFrom(src => src.People.Where(p => p.Role == PersonRole.Character).OrderBy(p => p.NormalizedName)))
            .ForMember(dest => dest.Editors,
                opt =>
                    opt.MapFrom(src => src.People.Where(p => p.Role == PersonRole.Editor).OrderBy(p => p.NormalizedName)));

        CreateMap<AppUser, UserDto>()
            .ForMember(dest => dest.AgeRestriction,
            opt =>
                opt.MapFrom(src => new AgeRestrictionDto()
                {
                    AgeRating = src.AgeRestriction,
                    IncludeUnknowns = src.AgeRestrictionIncludeUnknowns
                }));
        CreateMap<SiteTheme, SiteThemeDto>();
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

        CreateMap<ReadingList, ReadingListDto>();
        CreateMap<ReadingListItem, ReadingListItemDto>();

        CreateMap<Series, SearchResultDto>()
            .ForMember(dest => dest.SeriesId,
                opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.LibraryName,
                opt => opt.MapFrom(src => src.Library.Name));


        CreateMap<Library, LibraryDto>()
            .ForMember(dest => dest.Folders,
                opt =>
                    opt.MapFrom(src => src.Folders.Select(x => x.Path).ToList()));

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

    }
}
