using System.Linq;
using System.Threading.Tasks;
using API.Entities;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public interface IMediaErrorRepository
{
    void Attach(MediaError error);
    void Remove(MediaError error);
    Task<MediaError> Find(string filename);
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
}
