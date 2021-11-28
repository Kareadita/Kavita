using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.DTOs.Metadata;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Interfaces.Repositories;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories
{
    public class ChapterMetadataRepository : IChapterMetadataRepository
    {
        private readonly DataContext _context;
        private readonly IMapper _mapper;

        public ChapterMetadataRepository(DataContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public void Attach(ChapterMetadata metadata)
        {
            _context.ChapterMetadata.Attach(metadata);
        }

        public void Update(ChapterMetadata metadata)
        {
            _context.Entry(metadata).State = EntityState.Modified;
        }

        public async Task<ChapterMetadata> GetMetadataForChapter(int chapterId)
        {
            return await _context.ChapterMetadata
                .Where(cm => cm.ChapterId == chapterId)
                .SingleOrDefaultAsync();
        }

        public async Task<ChapterMetadataDto> GetMetadataDtoForChapter(int chapterId)
        {
            var metadata = await _context.ChapterMetadata
                .Where(cm => cm.ChapterId == chapterId)
                .Include(cm => cm.People)
                .AsNoTracking()
                .ProjectTo<ChapterMetadataDto>(_mapper.ConfigurationProvider)
                .SingleOrDefaultAsync();

            return metadata;
        }

        public async Task<IDictionary<int, IList<ChapterMetadata>>> GetMetadataForChapterIds(IList<int> chapterIds)
        {
            var chapterMetadatas = await _context.ChapterMetadata
                .Where(c => chapterIds.Contains(c.ChapterId))
                .Include(c => c.People)
                .ToListAsync();

            var chapterMetadatasMap = new Dictionary<int, IList<ChapterMetadata>>();
            foreach (var m in chapterMetadatas)
            {
                if (!chapterMetadatasMap.ContainsKey(m.ChapterId))
                {
                    var list = new List<ChapterMetadata>();
                    chapterMetadatasMap.Add(m.ChapterId, list);
                }
                chapterMetadatasMap[m.ChapterId].Add(m);
            }

            return chapterMetadatasMap;
        }
    }
}
