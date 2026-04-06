using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.ReadingLists;
using Kavita.Common.Extensions;
using Kavita.Database;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.ReadingLists.CBL;
using Kavita.Models.Entities.Enums.ReadingList;
using Kavita.Models.Entities.ReadingLists;
using Kavita.Server.Attributes;
using Flurl.Http;
using Kavita.Services;
using Kavita.Models.DTOs.ReadingLists.CBL.Import;
using Kavita.Models.DTOs.ReadingLists.CBL.RemapRules;
using Kavita.Models.DTOs.Uploads;
using AutoMapper;
using Hangfire;
using Kavita.Models.DTOs.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Server.Controllers;

/// <summary>
/// Responsible for the CBL import flow
/// </summary>
public class CblController(IReadingListService readingListService, IDirectoryService directoryService,
    ICblGithubService cblGithubService, DataContext dataContext, ICblImportService cblImporterService,
    IUnitOfWork unitOfWork, IMapper mapper, ILocalizationService localizationService,
    IUrlValidationService urlValidationService) : BaseApiController
{

    /// <summary>
    /// Enqueues the Reading List to be synced on a background thread. UI will be informed from <see cref="MessageFactory.ReadingListUpdated"/> event
    /// </summary>
    /// <param name="readingListId"></param>
    /// <param name="force">Ignore Hash and force sync flow</param>
    /// <returns></returns>
    [HttpPost("sync")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    [ReadingListAccess]
    public ActionResult SyncReadingList([FromQuery] int readingListId, bool force = false)
    {
        BackgroundJob.Enqueue(() => cblImporterService.SyncReadingListAsync(UserId, readingListId, force));
        return Ok();
    }

    /// <summary>
    /// Saves an uploaded CBL file to disk without importing. Returns the saved file info.
    /// </summary>
    [HttpPost("file-import")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult<CblSavedFileDto>> SaveCblFromFile(IFormFile cblFile)
    {
        var userId = UserId;
        var filename = cblFile.FileName;

        var ext = Path.GetExtension(filename);
        if (!ext.Equals(".cbl", StringComparison.OrdinalIgnoreCase)
            && !ext.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Only .cbl and .json files are allowed");
        }

        if (filename.Contains(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Invalid filename");
        }

        await SaveCblFile(cblFile, userId, filename);

        return Ok(new CblSavedFileDto
        {
            Name = filename,
            FileName = filename,
            Provider = ReadingListProvider.File
        });
    }

    /// <summary>
    /// Downloads a CBL file from a URL and saves it to disk without importing.
    /// </summary>
    [HttpPost("upload-cbl-file")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult<CblSavedFileDto>> SaveCblFromUrl(UploadUrlDto dto)
    {
        try
        {
            await urlValidationService.ValidateUrlAsync(dto.Url);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }

        var dir = GetCblManagerFolder(UserId);
        Directory.CreateDirectory(dir);

        string fullPath;
        string filename;
        try
        {
            fullPath = await dto.Url.DownloadFileAsync(dir);
            filename = Path.GetFileName(fullPath);
        }
        catch (FlurlHttpException)
        {
            return BadRequest("Unable to download file from URL");
        }

        var ext = Path.GetExtension(filename);
        if (!ext.Equals(".cbl", StringComparison.OrdinalIgnoreCase)
            && !ext.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
            return BadRequest("Only .cbl and .json files are allowed");
        }

        return Ok(new CblSavedFileDto
        {
            Name = filename,
            FileName = filename,
            Provider = ReadingListProvider.Url,
            DownloadUrl = dto.Url
        });
    }


    /// <summary>
    /// Downloads selected CBL files from the GitHub repo and saves them to disk without importing.
    /// </summary>
    [HttpPost("repo-import")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult<IList<CblSavedFileDto>>> SaveCblFromRepo([FromBody] CblRepoImportRequestDto request)
    {
        var userId = UserId;
        var savedFiles = new List<CblSavedFileDto>();

        foreach (var item in request.Items)
        {
            var content = await cblGithubService.GetFileContent(item.Path);
            SaveCblFileFromContent(content, userId, item.Name);

            savedFiles.Add(new CblSavedFileDto
            {
                Name = item.Name,
                FileName = item.Name,
                Provider = ReadingListProvider.Url,
                RepoPath = item.Path,
                DownloadUrl = item.DownloadUrl,
                Sha = item.Sha
            });
        }

        return Ok(savedFiles);
    }

    /// <summary>
    /// Validates an already-saved CBL file on disk. Called by the import modal after remap rule changes.
    /// </summary>
    [HttpPost("re-validate")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult<CblImportSummaryDto>> ReValidate([FromBody] CblReValidateRequestDto dto)
    {
        if (!ValidateFilename(dto.FileName)) return BadRequest("Invalid filename");

        var userId = UserId;
        var fullPath = Path.Join(GetCblManagerFolder(userId), dto.FileName);

        if (!System.IO.File.Exists(fullPath))
        {
            return BadRequest("File not found on server");
        }

        var summary = await cblImporterService.ValidateList(userId, fullPath);
        summary.FileName = dto.FileName;
        return Ok(summary);
    }

    /// <summary>
    /// Finalizes the import of a saved CBL file with user decisions
    /// </summary>
    [HttpPost("finalize-import")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult<CblImportSummaryDto>> FinalizeImport([FromBody] CblFinalizeRequestDto dto)
    {
        if (!ValidateFilename(dto.FileName)) return BadRequest("Invalid filename");

        var userId = UserId;
        var fullPath = Path.Join(GetCblManagerFolder(userId), dto.FileName);

        if (!System.IO.File.Exists(fullPath))
        {
            return BadRequest("File not found on server");
        }

        try
        {
            var summary = await cblImporterService.UpsertReadingList(
                userId, fullPath, dto.Decisions);
            summary.FileName = dto.FileName;

            // Set provider and sync tracking fields
            if (summary.Success != CblImportResult.Fail && dto.Provider != ReadingListProvider.None)
            {
                var readingList = await unitOfWork.ReadingListRepository
                    .GetReadingListByTitleAsync(summary.CblName, userId);

                if (readingList != null)
                {
                    readingList.Provider = dto.Provider;

                    // Repo-specific sync tracking
                    if (!string.IsNullOrEmpty(dto.RepoPath))
                    {
                        readingList.SourcePath = dto.RepoPath;
                        readingList.DownloadUrl = dto.DownloadUrl;
                        readingList.ShaHash = dto.Sha;
                        readingList.LastSyncedUtc = DateTime.UtcNow;
                        readingList.LastSyncCheckUtc = DateTime.UtcNow;
                    }
                    else if (!string.IsNullOrEmpty(dto.DownloadUrl))
                    {
                        // URL-only import — compute SHA from file content for change detection
                        var fileContent = await directoryService.FileSystem.File.ReadAllTextAsync(fullPath);
                        readingList.DownloadUrl = dto.DownloadUrl;
                        readingList.ShaHash = FileService.ComputeSha256(fileContent);
                        readingList.LastSyncedUtc = DateTime.UtcNow;
                        readingList.LastSyncCheckUtc = DateTime.UtcNow;
                    }

                    await readingListService.CalculateReadingListAgeRating(readingList);
                    await readingListService.CalculateStartAndEndDates(readingList);

                    await unitOfWork.CommitAsync();
                }
            }

            return Ok(summary);
        }
        finally
        {
            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }
    }

    /// <summary>
    /// Returns all remap rules accessible to the current user (own rules + global/admin rules).
    /// </summary>
    [HttpGet("remap-rules")]
    public async Task<ActionResult<IList<RemapRuleDto>>> GetRemapRules()
    {
        var rules = await unitOfWork.RemapRuleRepository.GetRuleDtosForUserAsync(UserId);
        return Ok(mapper.Map<IList<RemapRuleDto>>(rules));
    }

    /// <summary>
    /// Returns all rules across all users
    /// </summary>
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    [HttpGet("remap-rules/all")]
    public async Task<ActionResult<IList<RemapRuleDto>>> GetAllRemapRules()
    {
        return Ok(await unitOfWork.RemapRuleRepository.GetAllRuleDtosAsync());
    }

    /// <summary>
    /// Creates a new remap rule, or updates an existing one if a rule with the same
    /// CBL matching key (normalized name + volume + number) already exists for this user.
    /// When no explicit VolumeId is provided, attempts to auto-resolve a matching volume
    /// on the target series from the CBL volume string.
    /// </summary>
    [HttpPost("remap-rules")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult<RemapRuleDto>> CreateRemapRule([FromBody] CreateRemapRuleDto dto)
    {
        var ct = HttpContext.RequestAborted;
        var series = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(dto.SeriesId, ct: ct);
        if (series == null) return BadRequest(await localizationService.Translate(UserId, "series-doesnt-exist"));

        var normalizedName = dto.CblSeriesName.ToNormalized();

        // Auto-resolve VolumeId when the caller didn't provide one and there's a CBL volume string
        var volumeId = dto.VolumeId;
        if (volumeId == null && dto.ChapterId == null && !string.IsNullOrEmpty(dto.CblVolume) && series.Volumes != null)
        {
            var realVolumes = series.Volumes
                .Where(v => v.MinNumber is not (ParserConstants.LooseLeafVolumeNumber or ParserConstants.SpecialVolumeNumber))
                .ToList();

            if (realVolumes.Count > 0)
            {
                var matched = realVolumes.FirstOrDefault(v =>
                    v.Name.Equals(dto.CblVolume, StringComparison.OrdinalIgnoreCase)
                    || v.LookupName.Equals(dto.CblVolume, StringComparison.OrdinalIgnoreCase));
                volumeId = matched?.Id;
            }
        }

        // Check for an existing rule with the same CBL matching key for this user
        var existing = await unitOfWork.RemapRuleRepository.GetExactRuleAsync(normalizedName, dto.CblVolume, dto.CblNumber, UserId, ct);

        if (existing != null)
        {
            existing.SeriesId = dto.SeriesId;
            existing.VolumeId = volumeId;
            existing.ChapterId = dto.ChapterId;
            existing.CblSeriesName = dto.CblSeriesName;
            existing.SeriesNameAtMapping = series.Name;
            existing.CreatedUtc = DateTime.UtcNow;
        }
        else
        {
            existing = new ReadingListRemapRule
            {
                NormalizedCblSeriesName = normalizedName,
                CblSeriesName = dto.CblSeriesName,
                SeriesId = dto.SeriesId,
                CblVolume = dto.CblVolume,
                CblNumber = dto.CblNumber,
                VolumeId = volumeId,
                ChapterId = dto.ChapterId,
                SeriesNameAtMapping = series.Name,
                AppUserId = UserId,
                IsGlobal = false,
                CreatedUtc = DateTime.UtcNow
            };
            unitOfWork.RemapRuleRepository.Add(existing);
        }

        await unitOfWork.CommitAsync(ct);

        var resultDto = await unitOfWork.RemapRuleRepository.GetDtoByIdAsync(existing.Id, ct);
        return Ok(resultDto);
    }

    /// <summary>
    /// Promotes a remap rule to global scope. Admin-only.
    /// </summary>
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    [HttpPost("remap-rules/{id}/promote")]
    public async Task<ActionResult<RemapRuleDto>> PromoteRemapRule(int id)
    {
        var rule = await unitOfWork.RemapRuleRepository.GetByIdAsync(id, HttpContext.RequestAborted);
        if (rule == null) return NotFound();
        rule.IsGlobal = true;
        await unitOfWork.CommitAsync();
        return Ok(await unitOfWork.RemapRuleRepository.GetDtoByIdAsync(id, HttpContext.RequestAborted));
    }

    /// <summary>
    /// Demotes a global remap rule back to user-scoped. Admin-only.
    /// </summary>
    [Authorize(Policy = PolicyGroups.AdminPolicy)]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    [HttpPost("remap-rules/{id}/demote")]
    public async Task<ActionResult<RemapRuleDto>> DemoteRemapRule(int id)
    {
        var rule = await unitOfWork.RemapRuleRepository.GetByIdAsync(id, HttpContext.RequestAborted);
        if (rule == null) return NotFound();

        rule.IsGlobal = false;
        await unitOfWork.CommitAsync();

        return Ok(await unitOfWork.RemapRuleRepository.GetDtoByIdAsync(id, HttpContext.RequestAborted));
    }

    /// <summary>
    /// Updates a remap rule with issue-level detail (volume/chapter).
    /// </summary>
    [HttpPost("remap-rules/{id}")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult<RemapRuleDto>> UpdateRemapRule(int id, [FromBody] UpdateRemapRuleDto dto)
    {
        var ct = HttpContext.RequestAborted;
        var rule = await unitOfWork.RemapRuleRepository.GetByIdAsync(id, ct);
        if (rule == null) return NotFound();
        if (rule.AppUserId != UserId) return Forbid();

        if (dto.SeriesId.HasValue && dto.SeriesId.Value != rule.SeriesId)
        {
            var series = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(dto.SeriesId.Value, ct: ct);
            if (series == null) return BadRequest(await localizationService.Translate(UserId, "series-doesnt-exist"));

            rule.SeriesId = dto.SeriesId.Value;
            rule.SeriesNameAtMapping = series.Name;

            if (!string.IsNullOrEmpty(dto.CblSeriesName))
            {
                rule.CblSeriesName = dto.CblSeriesName;
                rule.NormalizedCblSeriesName = dto.CblSeriesName.ToNormalized();
            }

            // Auto-resolve VolumeId when not explicitly provided
            if (dto.VolumeId == null && dto.ChapterId == null && !string.IsNullOrEmpty(dto.CblVolume) && series.Volumes != null)
            {
                var realVolumes = series.Volumes
                    .Where(v => v.MinNumber is not (ParserConstants.LooseLeafVolumeNumber or ParserConstants.SpecialVolumeNumber))
                    .ToList();

                if (realVolumes.Count > 0)
                {
                    var matched = realVolumes.FirstOrDefault(v =>
                        v.Name.Equals(dto.CblVolume, StringComparison.OrdinalIgnoreCase)
                        || v.LookupName.Equals(dto.CblVolume, StringComparison.OrdinalIgnoreCase));
                    dto.VolumeId = matched?.Id;
                }
            }
        }

        rule.VolumeId = dto.VolumeId;
        rule.ChapterId = dto.ChapterId;
        rule.CblVolume = dto.CblVolume;
        rule.CblNumber = dto.CblNumber;
        if (!string.IsNullOrEmpty(dto.CblSeriesName))
        {
            rule.CblSeriesName = dto.CblSeriesName;
        }

        await unitOfWork.CommitAsync(ct);

        return Ok(await unitOfWork.RemapRuleRepository.GetDtoByIdAsync(id, ct));
    }

    /// <summary>
    /// Deletes a remap rule. Users can only delete their own rules.
    /// </summary>
    [HttpDelete("remap-rules/{id}")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> DeleteRemapRule(int id)
    {
        var rule = await unitOfWork.RemapRuleRepository.GetByIdAsync(id);
        if (rule == null) return NotFound();
        if (rule.AppUserId != UserId) return Forbid();

        unitOfWork.RemapRuleRepository.Remove(rule);
        await unitOfWork.CommitAsync();

        return Ok();
    }

    /// <summary>
    /// Provides the browse CBL Repo interface. Requires Download role.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    [HttpGet("browse")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult<CblRepoBrowseResultDto>> BrowseCblRepo([FromQuery] string path = "")
    {
        if (path.Contains("..") || path.Contains("http://")) return BadRequest();

        var result = await cblGithubService.BrowseRepo(path);

        // TODO: Refactor into CblService - Update Browse Results with sync details from what's on disk
        var syncedPaths = await dataContext.ReadingList
            .Where(rl => rl.AppUserId == UserId
                         && rl.Provider == ReadingListProvider.Url
                         && rl.SourcePath != null)
            .Select(rl => new { rl.SourcePath, rl.Id })
            .ToDictionaryAsync(x => x.SourcePath!, x => x.Id);

        foreach (var item in result.Items.Where(i => !i.IsDirectory))
        {
            if (syncedPaths.TryGetValue(item.Path, out var readingListId))
            {
                item.ExistingReadingListId = readingListId;
            }
        }

        return Ok(result);
    }

    private async Task SaveCblFile(IFormFile file, int userId, string filename)
    {
        var dir = GetCblManagerFolder(userId);
        Directory.CreateDirectory(dir);
        var outputFile = Path.Join(dir, filename);
        await using var stream = System.IO.File.Create(outputFile);
        await file.CopyToAsync(stream);
        stream.Close();
    }

    private void SaveCblFileFromContent(string content, int userId, string filename)
    {
        var dir = GetCblManagerFolder(userId);
        Directory.CreateDirectory(dir);
        var outputFile = Path.Join(dir, filename);
        System.IO.File.WriteAllText(outputFile, content);
    }

    private string GetCblManagerFolder(int userId)
    {
        return Path.Join(directoryService.TempDirectory, $"{userId}", "cbl-manager-download");
    }
}
