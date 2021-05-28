using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.Interfaces;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data
{
    public class CollectionTagRepository : ICollectionTagRepository
    {
        private readonly DataContext _context;
        private readonly IMapper _mapper;

        public CollectionTagRepository(DataContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<IEnumerable<CollectionTagDto>> GetAllTagDtos()
        {
            return await _context.CollectionTag
                .Select(c => c)
                .AsNoTracking()
                .ProjectTo<CollectionTagDto>(_mapper.ConfigurationProvider)
                .ToListAsync();
        }
        
        public async Task<IEnumerable<CollectionTagDto>> SearchTagDtos(string searchQuery)
        {
            return await _context.CollectionTag
                .Where(s => EF.Functions.Like(s.Title, $"%{searchQuery}%") 
                            || EF.Functions.Like(s.NormalizedTitle, $"%{searchQuery}%"))
                .OrderBy(s => s.Title)
                .AsNoTracking()
                .ProjectTo<CollectionTagDto>(_mapper.ConfigurationProvider)
                .ToListAsync();
        }


        public async Task<IEnumerable<SeriesDto>> GetSeriesForTag(int tagId)
        {
            throw new System.NotImplementedException();
        }

        public Task<bool> DoesTagExist(string name)
        {
            throw new System.NotImplementedException();
        }

        public Task<byte[]> GetCoverImageAsync(int collectionTagId)
        {
            return _context.CollectionTag
                .Where(c => c.Id == collectionTagId)
                .Select(c => c.CoverImage)
                .AsNoTracking()
                .SingleOrDefaultAsync();
        }
    }
}