using System.Linq;
using System.Threading.Tasks;
using API.DTOs.MediaErrors;
using API.Entities;
using API.Helpers;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public interface IMediaErrorRepository
{
    void Attach(MediaError error);
    void Remove(MediaError error);
    Task<MediaError> Find(string filename);
    Task<PagedList<MediaErrorDto>> GetAllErrorDtosAsync(UserParams userParams);
    Task<bool> ExistsAsync(MediaError error);
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

    public Task<MediaError?> Find(string filename)
    {
        return _context.MediaError.Where(e => e.FilePath == filename).SingleOrDefaultAsync();
    }

    public Task<PagedList<MediaErrorDto>> GetAllErrorDtosAsync(UserParams userParams)
    {
        var query = _context.MediaError
            .OrderByDescending(m => m.Created)
            .ProjectTo<MediaErrorDto>(_mapper.ConfigurationProvider)
            .AsNoTracking();
        return PagedList<MediaErrorDto>.CreateAsync(query, userParams.PageNumber, userParams.PageSize);
    }

    public Task<bool> ExistsAsync(MediaError error)
    {
        return _context.MediaError.AnyAsync(m => m.FilePath.Equals(error.FilePath)
                                                 && m.Comment.Equals(error.Comment)
                                                 && m.Details.Equals(error.Details)
        );
    }
}
