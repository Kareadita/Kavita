using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;

namespace API.Interfaces.Repositories
{
    public interface IVolumeRepository
    {
        void Update(Volume volume);
        Task<IList<MangaFile>> GetFilesForVolume(int volumeId);
        Task<byte[]> GetVolumeCoverImageAsync(int volumeId);
    }
}
