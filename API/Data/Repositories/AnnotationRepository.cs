using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.Annotations;
using API.DTOs.Reader;
using API.Entities;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;
#nullable enable

public interface IAnnotationRepository
{
    void Attach(AppUserAnnotation annotation);
    void Update(AppUserAnnotation annotation);
    void Remove(AppUserAnnotation annotation);
    Task<AnnotationDto?> GetAnnotationDto(int id);
    Task<AppUserAnnotation?> GetAnnotation(int id);
    Task<IList<FullAnnotationDto>> GetFullAnnotationsByUserIdAsync(int userId);
}

public class AnnotationRepository(DataContext context, IMapper mapper) : IAnnotationRepository
{
    public void Attach(AppUserAnnotation annotation)
    {
        context.AppUserAnnotation.Attach(annotation);
    }

    public void Update(AppUserAnnotation annotation)
    {
        context.AppUserAnnotation.Entry(annotation).State = EntityState.Modified;
    }

    public void Remove(AppUserAnnotation annotation)
    {
        context.AppUserAnnotation.Remove(annotation);
    }

    public async Task<AnnotationDto?> GetAnnotationDto(int id)
    {
        return await context.AppUserAnnotation
            .ProjectTo<AnnotationDto>(mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<AppUserAnnotation?> GetAnnotation(int id)
    {
        return await context.AppUserAnnotation
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    /// <summary>
    /// This does not track!
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<IList<FullAnnotationDto>> GetFullAnnotationsByUserIdAsync(int userId)
    {
        return await context.AppUserAnnotation
            .Where(a => a.AppUserId == userId)
            .Select(a => new FullAnnotationDto
            {
                Id = a.Id,
                SelectedText = a.SelectedText,
                Comment = a.Comment,
                Context = a.Context,
                ChapterTitle = a.ChapterTitle,
                PageNumber = a.PageNumber,
                SelectedSlotIndex = a.SelectedSlotIndex,
                ContainsSpoiler = a.ContainsSpoiler,
                CreatedUtc = a.CreatedUtc,
                LastModifiedUtc = a.LastModifiedUtc,
                LibraryId = a.LibraryId,
                LibraryName = a.Chapter.Volume.Series.Library.Name,
                SeriesId = a.SeriesId,
                SeriesName = a.Chapter.Volume.Series.Name,
                VolumeId = a.VolumeId,
                VolumeName = a.Chapter.Volume.Name,
                ChapterId = a.ChapterId
            })
            .OrderBy(a => a.SeriesId)
            .ThenBy(a => a.VolumeId)
            .ThenBy(a => a.ChapterId)
            .ThenBy(a => a.PageNumber)
            .ToListAsync();
    }
}
