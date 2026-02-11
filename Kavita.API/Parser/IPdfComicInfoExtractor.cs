using Kavita.Models.Metadata;

namespace Kavita.API.Parser;

public interface IPdfComicInfoExtractor
{
    ComicInfo? GetComicInfo(string filePath);
}
