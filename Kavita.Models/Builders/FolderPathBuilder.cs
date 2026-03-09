using Kavita.Models.Entities;

namespace Kavita.Models.Builders;

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
