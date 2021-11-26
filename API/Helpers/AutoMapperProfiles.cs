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

            CreateMap<Chapter, ChapterDto>();

            CreateMap<Series, SeriesDto>();

            CreateMap<CollectionTag, CollectionTagDto>();

            CreateMap<SeriesMetadata, SeriesMetadataDto>()
                .ForMember(dest => dest.Writers,
                    opt =>
                        opt.MapFrom(src => src.People.Where(p => p.Role == PersonRole.Writer)))
                .ForMember(dest => dest.Publishers,
                    opt =>
                        opt.MapFrom(src => src.People.Where(p => p.Role == PersonRole.Publisher)));

            CreateMap<Person, PersonDto>();
            CreateMap<Genre, GenreTagDto>();

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


            CreateMap<Person, PersonDto>();
            CreateMap<ChapterMetadata, ChapterMetadataDto>();

            CreateMap<IEnumerable<ServerSetting>, ServerSettingDto>()
                .ConvertUsing<ServerSettingConverter>();

        }
    }
}
