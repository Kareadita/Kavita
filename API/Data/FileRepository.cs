using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace API.Data
{
    public class FileRepository : IFileRepository
    {
        private readonly DataContext _dbContext;

        public FileRepository(DataContext context)
        {
            _dbContext = context;
        }

        public async Task<IEnumerable<string>> GetFileExtensions()
        {
            var fileExtensions = await _dbContext.MangaFile
                .AsNoTracking()
                .Select(x => x.FilePath.ToLower())
                .Distinct()
                .ToArrayAsync();

            var uniqueFileTypes = fileExtensions
                .Select(Path.GetExtension)
                .Where(x => x is not null)
                .Distinct();

            return uniqueFileTypes;
        }
    }
}
