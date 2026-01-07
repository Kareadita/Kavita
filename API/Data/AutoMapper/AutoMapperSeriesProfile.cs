using System;
using System.Linq;
using API.DTOs;
using API.Entities;
using AutoMapper;

namespace API.Data.AutoMapper;

/// <summary>
/// This is a way to attach progress at the DB level via a JOIN. Critical for healthy response time.
/// </summary>
public class AutoMapperSeriesProfile : Profile
{
    public AutoMapperSeriesProfile()
    {
        int userId = 0; // Placeholder, will be replaced at runtime

        CreateMap<Series, SeriesDto>()
            .ForMember(dest => dest.LibraryName,
                opt => opt.MapFrom(src => src.Library.Name))
            .ForMember(dest => dest.PagesRead,
                opt => opt.MapFrom(src =>
                    src.Progress
                        .Where(p => p.AppUserId == userId)
                        .Sum(p => (int?)p.PagesRead) ?? 0))
            .ForMember(dest => dest.LatestReadDate,
                opt => opt.MapFrom(src =>
                    src.Progress
                        .Where(p => p.AppUserId == userId)
                        .Max(p => (DateTime?)p.LastModified) ?? DateTime.MinValue))
            .ForMember(dest => dest.TotalReads,
                opt => opt.MapFrom(src =>
                    src.Progress
                        .Where(p => p.AppUserId == userId)
                        .Min(p => (int?)p.TotalReads) ?? 0))
            .ForMember(dest => dest.UserRating,
                opt => opt.MapFrom(src =>
                    src.Ratings
                        .Where(r => r.AppUserId == userId)
                        .Select(r => (float?)r.Rating)
                        .FirstOrDefault() ?? 0f))
            .ForMember(dest => dest.HasUserRated,
                opt => opt.MapFrom(src =>
                    src.Ratings
                        .Any(r => r.AppUserId == userId && r.HasBeenRated)));
    }
}
