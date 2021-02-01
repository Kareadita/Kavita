using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;
using API.Interfaces;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data
{
    public class VolumeRepository : IVolumeRepository
    {
        private readonly DataContext _context;
        private readonly IMapper _mapper;

        public VolumeRepository(DataContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }
        
        public void Update(Volume volume)
        {
            _context.Entry(volume).State = EntityState.Modified;
        }

        /// <summary>
        /// Returns a Chapter for an Id. Includes linked <see cref="MangaFile"/>s.
        /// </summary>
        /// <param name="chapterId"></param>
        /// <returns></returns>
        public async Task<Chapter> GetChapterAsync(int chapterId)
        {
            return await _context.Chapter
                .Include(c => c.Files)
                .AsNoTracking()
                .SingleOrDefaultAsync(c => c.Id == chapterId);
        }

        public async Task<ChapterDto> GetChapterDtoAsync(int chapterId)
        {
            var chapter = await _context.Chapter
                .Include(c => c.Files)
                .ProjectTo<ChapterDto>(_mapper.ConfigurationProvider)
                .AsNoTracking()
                .SingleOrDefaultAsync(c => c.Id == chapterId);

            return chapter;
        }

        public async Task<IList<MangaFile>> GetFilesForChapter(int chapterId)
        {
            return await _context.MangaFile
                .Where(c => chapterId == c.Id)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}