using System.Linq;
using API.DTOs;
using API.Entities;
using AutoMapper;

namespace API.Data.AutoMapper;

/// <summary>
/// Maps Volume entities to VolumeDto with user progress attached at the DB level via JOIN.
/// Nested Chapters receive progress data through AutoMapperChapterProfile.
/// </summary>
public class AutoMapperVolumeProfile : Profile
{
    public AutoMapperVolumeProfile()
    {
        int userId = 0;

        CreateMap<Volume, VolumeDto>()
            .ForMember(dest => dest.Number,
                opt => opt.MapFrom(src => (int)src.MinNumber))
            .ForMember(dest => dest.PagesRead,
                opt => opt.MapFrom(src =>
                    src.Chapters
                        .SelectMany(c => c.UserProgress)
                        .Where(p => p.AppUserId == userId)
                        .Sum(p => (int?)p.PagesRead) ?? 0))
            .ForMember(dest => dest.Chapters,
                opt => opt.MapFrom(src => src.Chapters.OrderBy(c => c.SortOrder)));
    }
}
