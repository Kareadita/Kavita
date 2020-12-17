using System.Collections.Generic;
using System.Threading.Tasks;

namespace API.Interfaces
{
    public interface IDirectoryService
    {
        IEnumerable<string> ListDirectory(string rootPath);
    }
}