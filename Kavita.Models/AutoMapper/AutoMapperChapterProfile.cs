using System;
using System.Linq;
using AutoMapper;
using Kavita.Models.DTOs;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.AutoMapper;

/// <summary>
/// Maps Chapter entities to ChapterDto with user progress attached at the DB level via JOIN.
/// </summary>
public class AutoMapperChapterProfile : Profile
{
    public AutoMapperChapterProfile()
    {
        int userId = 0; // Placeholder will be replaced at runtime

        CreateMap<Chapter, ChapterDto>()
            .MapChapterBase(userId);

        CreateMap<Chapter, StandaloneChapterDto>()
            .MapChapterBase(userId)
            .ForMember(dest => dest.SeriesId, opt => opt.MapFrom(src => src.Volume.SeriesId))
            .ForMember(dest => dest.VolumeTitle, opt => opt.MapFrom(src => src.Volume.Name))
            .ForMember(dest => dest.LibraryId, opt => opt.MapFrom(src => src.Volume.Series.LibraryId))
            .ForMember(dest => dest.LibraryType, opt => opt.MapFrom(src => src.Volume.Series.Library.Type));
    }
}

internal static class AutoMapperChapterProfileBaseExtensions
{
    public static IMappingExpression<Chapter, TDest> MapChapterBase<TDest>(this IMappingExpression<Chapter, TDest> mapping, int userId)
        where TDest: ChapterDto
    {
        return mapping
            .ForMember(dest => dest.PagesRead,
                opt => opt.MapFrom(src =>
                    src.UserProgress
                        .Where(p => p.AppUserId == userId)
                        .Select(p => (int?)p.PagesRead)
                        .FirstOrDefault() ?? 0))
            .ForMember(dest => dest.LastReadingProgressUtc,
                opt => opt.MapFrom(src =>
                    src.UserProgress
                        .Where(p => p.AppUserId == userId)
                        .Select(p => (DateTime?)p.LastModifiedUtc)
                        .FirstOrDefault() ?? DateTime.MinValue))
            .ForMember(dest => dest.LastReadingProgress,
                opt => opt.MapFrom(src =>
                    src.UserProgress
                        .Where(p => p.AppUserId == userId)
                        .Select(p => (DateTime?)p.LastModified)
                        .FirstOrDefault() ?? DateTime.MinValue))
            .ForMember(dest => dest.TotalReads,
                opt => opt.MapFrom(src =>
                    src.UserProgress
                        .Where(p => p.AppUserId == userId)
                        .Select(p => (int?)p.TotalReads)
                        .FirstOrDefault() ?? 0))

            // People mappings by role
            .ForMember(dest => dest.Writers,
                opt => opt.MapFrom(src => src.People
                    .Where(cp => cp.Role == PersonRole.Writer)
                    .Select(cp => cp.Person)
                    .OrderBy(p => p.NormalizedName)))
            .ForMember(dest => dest.CoverArtists,
                opt => opt.MapFrom(src => src.People
                    .Where(cp => cp.Role == PersonRole.CoverArtist)
                    .Select(cp => cp.Person)
                    .OrderBy(p => p.NormalizedName)))
            .ForMember(dest => dest.Publishers,
                opt => opt.MapFrom(src => src.People
                    .Where(cp => cp.Role == PersonRole.Publisher)
                    .Select(cp => cp.Person)
                    .OrderBy(p => p.NormalizedName)))
            .ForMember(dest => dest.Characters,
                opt => opt.MapFrom(src => src.People
                    .Where(cp => cp.Role == PersonRole.Character)
                    .Select(cp => cp.Person)
                    .OrderBy(p => p.NormalizedName)))
            .ForMember(dest => dest.Pencillers,
                opt => opt.MapFrom(src => src.People
                    .Where(cp => cp.Role == PersonRole.Penciller)
                    .Select(cp => cp.Person)
                    .OrderBy(p => p.NormalizedName)))
            .ForMember(dest => dest.Inkers,
                opt => opt.MapFrom(src => src.People
                    .Where(cp => cp.Role == PersonRole.Inker)
                    .Select(cp => cp.Person)
                    .OrderBy(p => p.NormalizedName)))
            .ForMember(dest => dest.Imprints,
                opt => opt.MapFrom(src => src.People
                    .Where(cp => cp.Role == PersonRole.Imprint)
                    .Select(cp => cp.Person)
                    .OrderBy(p => p.NormalizedName)))
            .ForMember(dest => dest.Colorists,
                opt => opt.MapFrom(src => src.People
                    .Where(cp => cp.Role == PersonRole.Colorist)
                    .Select(cp => cp.Person)
                    .OrderBy(p => p.NormalizedName)))
            .ForMember(dest => dest.Letterers,
                opt => opt.MapFrom(src => src.People
                    .Where(cp => cp.Role == PersonRole.Letterer)
                    .Select(cp => cp.Person)
                    .OrderBy(p => p.NormalizedName)))
            .ForMember(dest => dest.Editors,
                opt => opt.MapFrom(src => src.People
                    .Where(cp => cp.Role == PersonRole.Editor)
                    .Select(cp => cp.Person)
                    .OrderBy(p => p.NormalizedName)))
            .ForMember(dest => dest.Translators,
                opt => opt.MapFrom(src => src.People
                    .Where(cp => cp.Role == PersonRole.Translator)
                    .Select(cp => cp.Person)
                    .OrderBy(p => p.NormalizedName)))
            .ForMember(dest => dest.Teams,
                opt => opt.MapFrom(src => src.People
                    .Where(cp => cp.Role == PersonRole.Team)
                    .Select(cp => cp.Person)
                    .OrderBy(p => p.NormalizedName)))
            .ForMember(dest => dest.Locations,
                opt => opt.MapFrom(src => src.People
                    .Where(cp => cp.Role == PersonRole.Location)
                    .Select(cp => cp.Person)
                    .OrderBy(p => p.NormalizedName)));
    }
}
