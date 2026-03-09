using System;
using System.Threading.Tasks;

namespace Kavita.Services.ReadingLists;

/// <summary>
/// Responsible for reading v1 and v2 specs into a common format
/// </summary>
public class CblParserService
{
    Task ParseV1(string filePath)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    ///
    /// </summary>
    /// <remarks>https://github.com/ComicReadingLists/json-cbl-standard/blob/main/schema/1.0/comic-reading-list.schema.json</remarks>
    /// <param name="filePath"></param>
    /// <returns></returns>
    Task ParseV2(string filePath)
    {
        throw new NotImplementedException();
    }
}
