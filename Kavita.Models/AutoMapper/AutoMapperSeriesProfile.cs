using System;
using System.Linq;
using AutoMapper;
using Kavita.Models.Constants;
using Kavita.Models.DTOs;
using Kavita.Models.Entities;

namespace Kavita.Models.AutoMapper;

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
                        .Any(r => r.AppUserId == userId && r.HasBeenRated)))
            // Excludes both non-real volume buckets: the loose-leaf volume (ungrouped chapters) and the
            // Special volume
            .ForMember(dest => dest.VolumeCount,
                opt => opt.MapFrom(src => src.Volumes.Count(v =>
                    v.MinNumber != ParserConstants.LooseLeafVolumeNumber &&
                    v.MinNumber != ParserConstants.SpecialVolumeNumber)))
            // Unfiltered on purpose: specials all share Chapter.MinNumber == DefaultChapterNumber by design
            // so filtering it out would silently undercount any series made up of multiple specials.
            .ForMember(dest => dest.ChapterCount,
                opt => opt.MapFrom(src => src.Volumes.SelectMany(v => v.Chapters).Count()));
    }
}
