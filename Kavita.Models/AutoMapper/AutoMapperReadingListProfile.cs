using System;
using System.Linq;
using AutoMapper;
using Kavita.Models.DTOs.ReadingLists;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.ReadingLists;

namespace Kavita.Models.AutoMapper;

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

            // Volume info (legacy)
            .ForMember(dest => dest.VolumeNumber,
                opt => opt.MapFrom(src => src.Volume.Name))

            // Chapter info (legacy)
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

            // Nested Chapter DTO
            .ForMember(dest => dest.Chapter, opt => opt.MapFrom(src => new ReadingListItemChapterDto
            {
                Id = src.Chapter.Id,
                Range = src.Chapter.Range,
                TitleName = src.Chapter.TitleName,
                MinNumber = src.Chapter.MinNumber,
                MaxNumber = src.Chapter.MaxNumber,
                SortOrder = src.Chapter.SortOrder,
                Pages = src.Chapter.Pages,
                IsSpecial = src.Chapter.IsSpecial,
                ReleaseDate = src.Chapter.ReleaseDate,
                Summary = src.Chapter.Summary,
                WriterName = src.Chapter.People
                    .Where(p => p.Role == PersonRole.Writer)
                    .OrderBy(p => p.OrderWeight)
                    .Select(p => p.Person.Name)
                    .FirstOrDefault(),
                WriterId = src.Chapter.People
                    .Where(p => p.Role == PersonRole.Writer)
                    .OrderBy(p => p.OrderWeight)
                    .Select(p => (int?)p.PersonId)
                    .FirstOrDefault(),
                PencillerName = src.Chapter.People
                    .Where(p => p.Role == PersonRole.Penciller)
                    .OrderBy(p => p.OrderWeight)
                    .Select(p => p.Person.Name)
                    .FirstOrDefault(),
                PencillerId = src.Chapter.People
                    .Where(p => p.Role == PersonRole.Penciller)
                    .OrderBy(p => p.OrderWeight)
                    .Select(p => (int?)p.PersonId)
                    .FirstOrDefault()
            }))

            // Nested Volume DTO
            .ForMember(dest => dest.Volume, opt => opt.MapFrom(src => new ReadingListItemVolumeDto
            {
                Id = src.Volume.Id,
                Name = src.Volume.Name,
                MinNumber = src.Volume.MinNumber,
                MaxNumber = src.Volume.MaxNumber,
                SeriesId = src.Volume.SeriesId
            }))

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
