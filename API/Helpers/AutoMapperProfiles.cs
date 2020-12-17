using System.Linq;
using API.DTOs;
using API.Entities;
using AutoMapper;

namespace API.Helpers
{
    public class AutoMapperProfiles : Profile
    {
        public AutoMapperProfiles()
        {
            CreateMap<AppUser, MemberDto>();
            CreateMap<Library, LibraryDto>()
                .ForMember(dest => dest.Folders,
                    opt => 
                        opt.MapFrom(src => src.Folders.Select(x => x.Path).ToList()));
        }
    }
}