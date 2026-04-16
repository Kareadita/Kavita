using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Kavita.API.Repositories;
using Kavita.Models.DTOs.MediaErrors;
using Kavita.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Database.Repositories;

public class MediaErrorRepository(DataContext context, IMapper mapper) : IMediaErrorRepository
{
    public void Attach(MediaError? error)
    {
        if (error == null) return;
        context.MediaError.Attach(error);
    }

    public void Remove(IList<MediaError> errors)
    {
        context.MediaError.RemoveRange(errors);
    }

    public async Task<IEnumerable<MediaErrorDto>> GetAllErrorDtosAsync(CancellationToken ct = default)
    {
        return await context.MediaError
            .OrderByDescending(m => m.Created)
            .ProjectTo<MediaErrorDto>(mapper.ConfigurationProvider)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public Task<bool> ExistsAsync(MediaError error, CancellationToken ct = default)
    {
        return context.MediaError.AnyAsync(m => m.FilePath.Equals(error.FilePath)
                                                 && m.Comment.Equals(error.Comment)
                                                 && m.Details.Equals(error.Details), ct
        );
    }

    public async Task DeleteAll(CancellationToken ct = default)
    {
        await context.MediaError.ExecuteDeleteAsync(ct);
    }

    public Task<List<MediaError>> GetAllErrorsAsync(IList<string> comments, CancellationToken ct = default)
    {
        return context.MediaError
            .Where(m => comments.Contains(m.Comment))
            .ToListAsync(ct);
    }
}
