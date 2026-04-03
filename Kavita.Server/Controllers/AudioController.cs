using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.Models.DTOs.Reader;
using Kavita.Models.Entities.Enums;
using Kavita.Server.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kavita.Server.Controllers;

/// <summary>
/// Handles audiobook streaming and metadata retrieval.
/// Audio files are served directly from disk (not cached) with HTTP range request support
/// so the HTML5 audio element can seek efficiently in the browser.
/// </summary>
[Authorize]
public class AudioController(
    IUnitOfWork unitOfWork,
    ILocalizationService localizationService)
    : BaseApiController
{
    /// <summary>
    /// Returns metadata for the audiobook reader: title, author, narrator, duration (in seconds).
    /// </summary>
    /// <param name="chapterId"></param>
    [HttpGet("{chapterId}/audio-info")]
    public async Task<ActionResult<AudioInfoDto>> GetAudioInfo(int chapterId)
    {
        var dto = await unitOfWork.ChapterRepository.GetChapterInfoDtoAsync(chapterId);
        if (dto == null) return BadRequest(await localizationService.Translate(UserId, "chapter-doesnt-exist"));

        var chapter = await unitOfWork.ChapterRepository.GetChapterAsync(chapterId, ChapterIncludes.Files | ChapterIncludes.People);
        if (chapter == null) return BadRequest(await localizationService.Translate(UserId, "chapter-doesnt-exist"));

        var author = string.Empty;
        var narrator = string.Empty;

        // Pull author/narrator from chapter people if available
        if (chapter.People != null)
        {
            var writers = chapter.People
                .Where(p => p.Role == PersonRole.Writer)
                .Select(p => p.Person.Name);
            author = string.Join(", ", writers);

            // Narrators are stored as PersonRole.Narrator; legacy entries may use Penciller
            var narrators = chapter.People
                .Where(p => p.Role is PersonRole.Narrator or PersonRole.Penciller)
                .Select(p => p.Person.Name)
                .Distinct();
            narrator = string.Join(", ", narrators);
        }

        var info = new AudioInfoDto
        {
            ChapterNumber = dto.ChapterNumber,
            VolumeNumber = dto.VolumeNumber,
            VolumeId = dto.VolumeId,
            BookTitle = chapter.TitleName ?? dto.SeriesName,
            SeriesName = dto.SeriesName,
            SeriesFormat = dto.SeriesFormat,
            SeriesId = dto.SeriesId,
            LibraryId = dto.LibraryId,
            IsSpecial = dto.IsSpecial,
            Pages = dto.Pages,
            ChapterTitle = chapter.TitleName ?? string.Empty,
            Author = author,
            Narrator = narrator
        };

        return Ok(info);
    }

    /// <summary>
    /// Streams an audio file with HTTP range request support for efficient seeking.
    /// The audio file is served directly from disk — not cached — since audio files
    /// can be very large (multiple GB).
    /// </summary>
    /// <param name="chapterId">Chapter to stream</param>
    /// <param name="apiKey">User API key for authentication</param>
    [ChapterAccess]
    [HttpGet("{chapterId}/stream")]
    public async Task<ActionResult> StreamAudio(int chapterId, [FromQuery] string apiKey)
    {
        var files = await unitOfWork.ChapterRepository.GetFilesForChapterAsync(chapterId);
        var file = files.FirstOrDefault();
        if (file == null) return NotFound();

        if (!System.IO.File.Exists(file.FilePath))
        {
            return NotFound();
        }

        // Serve directly from disk with range request support — do not cache large audio files
        var contentType = GetAudioContentType(file.FilePath);
        return PhysicalFile(file.FilePath, contentType, Path.GetFileName(file.FilePath), enableRangeProcessing: true);
    }

    private static string GetAudioContentType(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".m4b" or ".m4a" => "audio/mp4",
            ".mp3"           => "audio/mpeg",
            ".flac"          => "audio/flac",
            ".ogg" or ".oga" or ".opus" => "audio/ogg",
            ".aac"           => "audio/aac",
            ".wma"           => "audio/x-ms-wma",
            ".wav"           => "audio/wav",
            _                => "application/octet-stream"
        };
    }
}
