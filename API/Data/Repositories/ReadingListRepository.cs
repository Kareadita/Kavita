using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.ReadingLists;
using API.Entities;
using API.Interfaces;
using API.Interfaces.Repositories;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories
{
    public class ReadingListRepository : IReadingListRepository
    {
        private readonly DataContext _context;
        private readonly IMapper _mapper;

        public ReadingListRepository(DataContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }


        public async Task<IEnumerable<ReadingListDto>> GetReadingListDtosForUserAsync(int userId, bool includePromoted)
        {
            return await _context.ReadingList
                .Where(l => l.AppUserId == userId || (includePromoted &&  l.Promoted ))
                .OrderBy(l => l.LastModified)
                .ProjectTo<ReadingListDto>(_mapper.ConfigurationProvider)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<ReadingList> GetReadingListByIdAsync(int readingListId)
        {
            return await _context.ReadingList
                .Where(r => r.Id == readingListId)
                .Include(r => r.Items)
                .SingleOrDefaultAsync();
        }
    }
}
