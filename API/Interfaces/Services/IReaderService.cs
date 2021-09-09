using System.Threading.Tasks;
using API.DTOs;
using API.Entities;

namespace API.Interfaces.Services
{
    public interface IReaderService
    {
        Task<bool> SaveReadingProgress(ProgressDto progressDto, int userId);
    }
}
