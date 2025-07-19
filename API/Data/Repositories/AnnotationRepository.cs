using System.Threading.Tasks;
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
}
