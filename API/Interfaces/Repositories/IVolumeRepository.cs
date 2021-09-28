using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;

namespace API.Interfaces.Repositories
{
    public interface IVolumeRepository
    {
        void Add(Volume volume);
        void Update(Volume volume);
        void Remove(Volume volume);
        Task<IList<MangaFile>> GetFilesForVolume(int volumeId);
        Task<string> GetVolumeCoverImageAsync(int volumeId);
        Task<IList<int>> GetChapterIdsByVolumeIds(IReadOnlyList<int> volumeIds);
    }
}
