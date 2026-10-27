using System.Threading;
using System.Threading.Tasks;

namespace Kavita.API.Services;

/// <summary>
/// This is a singleton
/// </summary>
public interface IKepubifyConverterService
{
    bool KepubifySupported { get; }

    string BuildKepubifyPath(int chapterId);

    Task CheckKepubifySupport(string kepubifyPath, CancellationToken cancellationToken);

    Task ConvertEpubToKepub(string epubPath, string kepubPath, CancellationToken cancellationToken);
}
