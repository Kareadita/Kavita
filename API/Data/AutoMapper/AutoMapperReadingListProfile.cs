using System;
using System.Linq;
using API.DTOs.ReadingLists;
using API.Entities;
using AutoMapper;

namespace API.Data.AutoMapper;

/// <summary>
/// Maps ReadingList and ReadingListItem entities to DTOs with user progress attached at the DB level.
/// </summary>
public class AutoMapperReadingListProfile : Profile
{
    public AutoMapperReadingListProfile()
    {
        int userId = 0;

        CreateMap<ReadingList, ReadingListDto>()
            .ForMember(dest => dest.ItemCount,
                opt => opt.MapFrom(src => src.Items.Count))
            .ForMember(dest => dest.OwnerUserName,
                opt => opt.MapFrom(src => src.AppUser.UserName));

        CreateMap<ReadingListItem, ReadingListItemDto>()
            // Series info
            .ForMember(dest => dest.SeriesName,
                opt => opt.MapFrom(src => src.Series.Name))
            .ForMember(dest => dest.SeriesSortName,
                opt => opt.MapFrom(src => src.Series.SortName))
            .ForMember(dest => dest.SeriesFormat,
                opt => opt.MapFrom(src => src.Series.Format))
            .ForMember(dest => dest.LibraryId,
                opt => opt.MapFrom(src => src.Series.LibraryId))

            // Library info
            .ForMember(dest => dest.LibraryName,
                opt => opt.MapFrom(src => src.Series.Library.Name))
            .ForMember(dest => dest.LibraryType,
                opt => opt.MapFrom(src => src.Series.Library.Type))

            // Volume info
            .ForMember(dest => dest.VolumeNumber,
                opt => opt.MapFrom(src => src.Volume.Name))

            // Chapter info
            .ForMember(dest => dest.ChapterNumber,
                opt => opt.MapFrom(src => src.Chapter.Range))
            .ForMember(dest => dest.ChapterTitleName,
                opt => opt.MapFrom(src => src.Chapter.TitleName))
            .ForMember(dest => dest.PagesTotal,
                opt => opt.MapFrom(src => src.Chapter.Pages))
            .ForMember(dest => dest.ReleaseDate,
                opt => opt.MapFrom(src => src.Chapter.ReleaseDate))
            .ForMember(dest => dest.Summary,
                opt => opt.MapFrom(src => src.Chapter.Summary))
            .ForMember(dest => dest.IsSpecial,
                opt => opt.MapFrom(src => src.Chapter.IsSpecial))
            .ForMember(dest => dest.FileSize,
                opt => opt.MapFrom(src => src.Chapter.Files.Sum(f => f.Bytes)))

            // Progress
            .ForMember(dest => dest.PagesRead,
                opt => opt.MapFrom(src =>
                    src.Chapter.UserProgress
                        .Where(p => p.AppUserId == userId)
                        .Select(p => (int?)p.PagesRead)
                        .FirstOrDefault() ?? 0))
            .ForMember(dest => dest.LastReadingProgressUtc,
                opt => opt.MapFrom(src =>
                    src.Chapter.UserProgress
                        .Where(p => p.AppUserId == userId)
                        .Select(p => (DateTime?)p.LastModifiedUtc)
                        .FirstOrDefault()))

            // Title - computed after projection
            .ForMember(dest => dest.Title, opt => opt.Ignore());
    }
}
