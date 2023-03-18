using System.IO;
using API.Entities;

namespace API.Helpers.Builders;

public class FolderPathBuilder : IEntityBuilder<FolderPath>
{
    private readonly FolderPath _folderPath;
    public FolderPath Build() => _folderPath;

    public FolderPathBuilder(string directory)
    {
        _folderPath = new FolderPath()
        {
            Path = directory,
            Id = 0
        };
    }
}
