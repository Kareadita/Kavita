using System.Threading.Tasks;
using API.Entities;
using API.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace API.Data
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly DataContext _context;
        private readonly IMapper _mapper;
        private readonly UserManager<AppUser> _userManager;
        private readonly ILogger<UnitOfWork> _logger;

        public UnitOfWork(DataContext context, IMapper mapper, UserManager<AppUser> userManager, ILogger<UnitOfWork> logger)
        {
            _context = context;
            _mapper = mapper;
            _userManager = userManager;
            _logger = logger;
        }

        public ISeriesRepository SeriesRepository => new SeriesRepository(_context, _mapper, _logger);
        public IUserRepository UserRepository => new UserRepository(_context, _userManager);
        public ILibraryRepository LibraryRepository => new LibraryRepository(_context, _mapper);

        public IVolumeRepository VolumeRepository => new VolumeRepository(_context, _mapper, _logger);

        public ISettingsRepository SettingsRepository => new SettingsRepository(_context, _mapper);
        
        public async Task<bool> Complete()
        {
            return await _context.SaveChangesAsync() > 0;
        }

        public bool HasChanges()
        {
            return _context.ChangeTracker.HasChanges();
        }
    }
}