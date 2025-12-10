using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.MediaErrors;
using API.Entities;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;
#nullable enable

public interface IMediaErrorRepository
{
    void Attach(MediaError error);
    void Remove(MediaError error);
    void Remove(IList<MediaError> errors);
    Task<MediaError?> Find(string filename);
    IEnumerable<MediaErrorDto> GetAllErrorDtosAsync();
    Task<bool> ExistsAsync(MediaError error);
    Task DeleteAll();
    Task<List<MediaError>> GetAllErrorsAsync(IList<string> comments);
}

public class MediaErrorRepository : IMediaErrorRepository
{
    private readonly DataContext _context;
    private readonly IMapper _mapper;

    public MediaErrorRepository(DataContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public void Attach(MediaError? error)
    {
        if (error == null) return;
        _context.MediaError.Attach(error);
    }

    public void Remove(MediaError? error)
    {
        if (error == null) return;
        _context.MediaError.Remove(error);
    }

    public void Remove(IList<MediaError> errors)
    {
        _context.MediaError.RemoveRange(errors);
    }

    public Task<MediaError?> Find(string filename)
    {
        return _context.MediaError.Where(e => e.FilePath == filename).SingleOrDefaultAsync();
    }

    public IEnumerable<MediaErrorDto> GetAllErrorDtosAsync()
    {
        var query = _context.MediaError
            .OrderByDescending(m => m.Created)
            .ProjectTo<MediaErrorDto>(_mapper.ConfigurationProvider)
            .AsNoTracking();
        return query.AsEnumerable();
    }

    public Task<bool> ExistsAsync(MediaError error)
    {
        return _context.MediaError.AnyAsync(m => m.FilePath.Equals(error.FilePath)
                                                 && m.Comment.Equals(error.Comment)
                                                 && m.Details.Equals(error.Details)
        );
    }

    public async Task DeleteAll()
    {
        _context.MediaError.RemoveRange(await _context.MediaError.ToListAsync());
        await _context.SaveChangesAsync();
    }

    public Task<List<MediaError>> GetAllErrorsAsync(IList<string> comments)
    {
        return _context.MediaError
            .Where(m => comments.Contains(m.Comment))
            .ToListAsync();
    }
}
