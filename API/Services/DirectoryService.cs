using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using API.Interfaces;

namespace API.Services
{
    public class DirectoryService : IDirectoryService
    {
        public IEnumerable<string> ListDirectory(string rootPath)
        {
            // TODO: Filter out Hidden and System folders
            // DirectoryInfo di = new DirectoryInfo(@path);
            // var dirs = di.GetDirectories()
            //     .Where(dir => (dir.Attributes & FileAttributes.Hidden & FileAttributes.System) == 0).ToImmutableList();
            //

            return Directory.GetDirectories(@rootPath);
        }
    }
}