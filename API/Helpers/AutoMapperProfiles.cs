using System.Collections.Generic;
using System.Linq;
using API.DTOs;
using API.Entities;
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
            
            CreateMap<AppUserPreferences, UserPreferencesDto>();
            
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