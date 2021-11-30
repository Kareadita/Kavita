using System.Collections.Generic;
using System.Linq;
using API.DTOs;
using API.DTOs.CollectionTags;
using API.DTOs.Metadata;
using API.DTOs.Reader;
using API.DTOs.ReadingLists;
using API.DTOs.Settings;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Helpers.Converters;
using AutoMapper;

namespace API.Helpers
{
    public class AutoMapperProfiles : Profile
    {
        public AutoMapperProfiles()
        {
            CreateMap<LibraryDto, Library>();

            CreateMap<Volume, VolumeDto>();

            CreateMap<MangaFile, MangaFileDto>();

            CreateMap<Chapter, ChapterDto>()
                .ForMember(dest => dest.Writers,
                    opt =>
                        opt.MapFrom(src => src.People.Where(p => p.Role == PersonRole.Writer)))
                .ForMember(dest => dest.CoverArtist,
                    opt =>
                        opt.MapFrom(src => src.People.Where(p => p.Role == PersonRole.Artist)))
                .ForMember(dest => dest.Colorist,
                    opt =>
                        opt.MapFrom(src => src.People.Where(p => p.Role == PersonRole.Colorist)))
                .ForMember(dest => dest.Inker,
                    opt =>
                        opt.MapFrom(src => src.People.Where(p => p.Role == PersonRole.Inker)))
                .ForMember(dest => dest.Letterer,
                    opt =>
                        opt.MapFrom(src => src.People.Where(p => p.Role == PersonRole.Letterer)))
                .ForMember(dest => dest.Penciller,
                    opt =>
                        opt.MapFrom(src => src.People.Where(p => p.Role == PersonRole.Penciller)))
                .ForMember(dest => dest.Publisher,
                    opt =>
                        opt.MapFrom(src => src.People.Where(p => p.Role == PersonRole.Publisher)))
                .ForMember(dest => dest.Editor,
                    opt =>
                        opt.MapFrom(src => src.People.Where(p => p.Role == PersonRole.Editor)));

            CreateMap<Series, SeriesDto>();

            CreateMap<CollectionTag, CollectionTagDto>();

            CreateMap<Person, PersonDto>();

            CreateMap<Genre, GenreTagDto>();

            CreateMap<SeriesMetadata, SeriesMetadataDto>()
                .ForMember(dest => dest.Writers,
                    opt =>
                        opt.MapFrom(src => src.People.Where(p => p.Role == PersonRole.Writer)))
                .ForMember(dest => dest.Artists,
                    opt =>
                        opt.MapFrom(src => src.People.Where(p => p.Role == PersonRole.Artist)))
                .ForMember(dest => dest.Characters,
                    opt =>
                        opt.MapFrom(src => src.People.Where(p => p.Role == PersonRole.Character)))
                .ForMember(dest => dest.Publishers,
                    opt =>
                        opt.MapFrom(src => src.People.Where(p => p.Role == PersonRole.Publisher)));

            CreateMap<ChapterMetadata, ChapterMetadataDto>()
                .ForMember(dest => dest.Writers,
                    opt =>
                        opt.MapFrom(src => src.People.Where(p => p.Role == PersonRole.Writer)))
                .ForMember(dest => dest.CoverArtist,
                    opt =>
                        opt.MapFrom(src => src.People.Where(p => p.Role == PersonRole.Artist)))
                .ForMember(dest => dest.Colorist,
                    opt =>
                        opt.MapFrom(src => src.People.Where(p => p.Role == PersonRole.Colorist)))
                .ForMember(dest => dest.Inker,
                    opt =>
                        opt.MapFrom(src => src.People.Where(p => p.Role == PersonRole.Inker)))
                .ForMember(dest => dest.Letterer,
                    opt =>
                        opt.MapFrom(src => src.People.Where(p => p.Role == PersonRole.Letterer)))
                .ForMember(dest => dest.Penciller,
                    opt =>
                        opt.MapFrom(src => src.People.Where(p => p.Role == PersonRole.Penciller)))
                .ForMember(dest => dest.Publisher,
                    opt =>
                        opt.MapFrom(src => src.People.Where(p => p.Role == PersonRole.Publisher)))
                .ForMember(dest => dest.Editor,
                    opt =>
                        opt.MapFrom(src => src.People.Where(p => p.Role == PersonRole.Editor)));




            CreateMap<AppUserPreferences, UserPreferencesDto>();

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
                .AfterMap((ps, pst, context) => context.Mapper.Map(ps.Libraries, pst.Libraries));

            CreateMap<RegisterDto, AppUser>();


            CreateMap<IEnumerable<ServerSetting>, ServerSettingDto>()
                .ConvertUsing<ServerSettingConverter>();

        }
    }
}
