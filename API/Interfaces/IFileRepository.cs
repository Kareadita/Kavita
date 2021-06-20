using System.Collections.Generic;
using System.Threading.Tasks;

namespace API.Interfaces
{
    public interface IFileRepository
    {
        Task<IEnumerable<string>> GetFileExtensions();
    }
}