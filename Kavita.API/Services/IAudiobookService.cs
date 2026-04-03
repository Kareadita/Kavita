using Kavita.Models.DTOs.Reader;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Parser;

namespace Kavita.API.Services;

public interface IAudiobookService
{
    /// <summary>
    /// Parses metadata from an audio file and returns a ParserInfo.
    /// Falls back to filename-based parsing if tag metadata is unavailable.
    /// </summary>
    ParserInfo? ParseInfo(string filePath);

    /// <summary>
    /// Returns the duration of the audio file in seconds.
    /// Stored in Chapter.Pages to reuse the existing progress tracking infrastructure.
    /// </summary>
    int GetDurationInSeconds(string filePath);

    /// <summary>
    /// Extracts embedded cover art from an audio file and saves it as a cover image.
    /// Returns the saved filename, or empty string if no cover art is available.
    /// </summary>
    string GetCoverImage(string filePath, string fileName, string outputDirectory, EncodeFormat encodeFormat, CoverImageSize size = CoverImageSize.Default);
}
