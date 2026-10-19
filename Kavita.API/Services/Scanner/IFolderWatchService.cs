using System.Threading.Tasks;

namespace Kavita.API.Services.Scanner;

public interface IFolderWatchService
{
    /// <summary>
    /// Start watching or polling all library folders
    /// </summary>
    Task StartWatching();
    /// <summary>
    /// Stop watching and polling all folders
    /// </summary>
    void StopWatching();
    /// <summary>
    /// Essentially stops then starts watching. Useful if there is a change in folders or libraries
    /// </summary>
    Task RestartWatching();
}
