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
                .SingleOrDefaultAsync(c => c.Id == chapterId);
        }
        
        
        /// <summary>
        /// Returns Chapters for a volume id.
        /// </summary>
        /// <param name="volumeId"></param>
        /// <returns></returns>
        public async Task<IList<Chapter>> GetChaptersAsync(int volumeId)
        {
            return await _context.Chapter
                .Where(c => c.VolumeId == volumeId)
                .ToListAsync();
        }

        /// <summary>
        /// Returns the cover image for a chapter id.
        /// </summary>
        /// <param name="chapterId"></param>
        /// <returns></returns>
        public async Task<byte[]> GetChapterCoverImageAsync(int chapterId)
        {
            return await _context.Chapter
                .Where(c => c.Id == chapterId)
                .Select(c => c.CoverImage)
                .AsNoTracking()
                .SingleOrDefaultAsync();
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
        
        public async Task<IList<MangaFile>> GetFilesForVolume(int volumeId)
        {
            return await _context.Chapter
                .Where(c => volumeId == c.VolumeId)
                .Include(c => c.Files)
                .SelectMany(c => c.Files)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}