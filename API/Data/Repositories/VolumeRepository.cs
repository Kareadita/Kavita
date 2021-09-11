using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.DTOs.Reader;
using API.Entities;
using API.Interfaces.Repositories;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories
{
    public class VolumeRepository : IVolumeRepository
    {
        private readonly DataContext _context;

        public VolumeRepository(DataContext context)
        {
            _context = context;
        }

        public void Update(Volume volume)
        {
            _context.Entry(volume).State = EntityState.Modified;
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

        public async Task<byte[]> GetVolumeCoverImageAsync(int volumeId)
        {
            return await _context.Volume
                .Where(v => v.Id == volumeId)
                .Select(v => v.CoverImage)
                .AsNoTracking()
                .SingleOrDefaultAsync();
        }
    }
}
