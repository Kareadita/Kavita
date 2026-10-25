using Kavita.Models.Scanner;

namespace Kavita.API.Services.Scanner;

public interface IMountDetectorService
{
    /// <returns>Null when the mount cannot be read, for example on macOS or for a UNC path</returns>
    MountInfo? Detect(string folder);
}
