﻿using System.Collections.Generic;
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

            CreateMap<CollectionTag, CollectionTagDto>();

            CreateMap<SeriesMetadata, SeriesMetadataDto>();

            CreateMap<Person, PersonDto>();

            CreateMap<AppUserPreferences, UserPreferencesDto>();

            CreateMap<AppUserBookmark, BookmarkDto>();

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
