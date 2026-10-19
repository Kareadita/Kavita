using System.Collections.Generic;
using Kavita.Models.Scanner;

namespace Kavita.API.Services.Scanner;

public interface IFolderPollerService
{
    /// <summary>
    /// Polls exactly these folders from now on. A folder that was already polled keeps its folder list, so it is not walked again
    /// </summary>
    void Poll(IReadOnlyList<PolledFolder> folders);
    void Stop();
}
