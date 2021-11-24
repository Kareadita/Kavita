using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.DTOs.Metadata;
using API.Entities;
using API.Entities.Enums;
using API.Interfaces.Repositories;
using AutoMapper;
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
                .SingleOrDefaultAsync();

            if (metadata == null) return null;

            var personProjection = new Func<Person, PersonDto>((p) => new PersonDto()
            {
                Name = p.Name,
                Role = p.Role
            });

            return new ChapterMetadataDto()
            {
                Title = metadata.Title,
                Id = metadata.Id,
                ChapterId = metadata.ChapterId,
                Writers = metadata.People.Where(p => p.Role == PersonRole.Writer).Select(personProjection).ToList(),
                Colorist = metadata.People.Where(p => p.Role == PersonRole.Colorist).Select(personProjection).ToList(),
                Editor = metadata.People.Where(p => p.Role == PersonRole.Editor).Select(personProjection).ToList(),
                Inker = metadata.People.Where(p => p.Role == PersonRole.Inker).Select(personProjection).ToList(),
                Letterer = metadata.People.Where(p => p.Role == PersonRole.Letterer).Select(personProjection).ToList(),
                Penciller = metadata.People.Where(p => p.Role == PersonRole.Penciller).Select(personProjection).ToList(),
                Publisher = metadata.People.Where(p => p.Role == PersonRole.Publisher).Select(personProjection).ToList(),
                CoverArtist = metadata.People.Where(p => p.Role == PersonRole.CoverArtist).Select(personProjection).ToList(),
            };
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
