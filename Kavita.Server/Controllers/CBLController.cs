using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Kavita.API.Services;
using Kavita.API.Services.ReadingLists;
using Kavita.Database;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.ReadingLists.CBL;
using Kavita.Models.DTOs.ReadingLists.CBL.V1;
using Kavita.Models.Entities.Enums.ReadingList;
using Kavita.Server.Attributes;
using Kavita.Services.Reading;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace Kavita.Server.Controllers;

/// <summary>
/// Responsible for the CBL import flow
/// </summary>
public class CblController(IReadingListService readingListService, IDirectoryService directoryService,
    ICblGithubService cblGithubService, DataContext dataContext, ICblImportService cblImporterService) : BaseApiController
{
    /// <summary>
    /// The first step in a cbl import. This validates the cbl file that if an import occured, would it be successful.
    /// If this returns errors, the cbl will always be rejected by Kavita.
    /// </summary>
    /// <param name="cbl">FormBody with parameter name of cbl</param>
    /// <param name="useComicVineMatching">Use comic vine matching or not. Defaults to false</param>
    /// <returns></returns>
    [HttpPost("validate")]
    [SwaggerIgnore]
    public async Task<ActionResult<CblImportSummaryDto>> ValidateCbl(IFormFile cbl, [FromQuery] bool useComicVineMatching = false)
    {
        var userId = UserId;
        try
        {
            var cblReadingList = await SaveAndLoadCblFile(cbl);
            var importSummary = await readingListService.ValidateCblFile(userId, cblReadingList, useComicVineMatching);
            importSummary.FileName = cbl.FileName;

            return Ok(importSummary);
        }
        catch (ArgumentNullException)
        {
            return Ok(new CblImportSummaryDto
            {
                FileName = cbl.FileName,
                Success = CblImportResult.Fail,
                Results =
                [
                    new CblBookResult
                    {
                        Reason = CblImportReason.InvalidFile
                    }
                ]
            });
        }
        catch (InvalidOperationException)
        {
            return Ok(new CblImportSummaryDto
            {
                FileName = cbl.FileName,
                Success = CblImportResult.Fail,
                Results =
                [
                    new CblBookResult
                    {
                        Reason = CblImportReason.InvalidFile
                    }
                ]
            });
        }
    }


    /// <summary>
    /// Performs the actual import (assuming dryRun = false)
    /// </summary>
    /// <param name="cbl">FormBody with parameter name of cbl</param>
    /// <param name="dryRun">If true, will only emulate the import but not perform. This should be done to preview what will happen</param>
    /// <param name="useComicVineMatching">Use comic vine matching or not. Defaults to false</param>
    /// <returns></returns>
    [SwaggerIgnore]
    [HttpPost("import")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult<CblImportSummaryDto>> ImportCbl(IFormFile cbl, [FromQuery] bool dryRun = false, [FromQuery] bool useComicVineMatching = false)
    {
        try
        {
            var userId = UserId;
            var cblReadingList = await SaveAndLoadCblFile(cbl);
            var importSummary = await readingListService.CreateReadingListFromCbl(userId, cblReadingList, dryRun, useComicVineMatching);
            importSummary.FileName = cbl.FileName;

            return Ok(importSummary);
        } catch (ArgumentNullException)
        {
            return Ok(new CblImportSummaryDto
            {
                FileName = cbl.FileName,
                Success = CblImportResult.Fail,
                Results =
                [
                    new CblBookResult
                    {
                        Reason = CblImportReason.InvalidFile
                    }
                ]
            });
        }
        catch (InvalidOperationException)
        {
            return Ok(new CblImportSummaryDto
            {
                FileName = cbl.FileName,
                Success = CblImportResult.Fail,
                Results =
                [
                    new CblBookResult
                    {
                        Reason = CblImportReason.InvalidFile
                    }
                ]
            });
        }
    }

    /// <summary>
    /// Imports from the Repo browser. Downloads selected CBL files from GitHub and upserts reading lists.
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    [HttpPost("repo-import")]
    [DisallowRole(PolicyConstants.ReadOnlyRole)]
    public async Task<ActionResult> ImportFromCblRepo([FromBody] CblRepoImportRequestDto request)
    {
        var userId = UserId;

        foreach (var item in request.Items)
        {
            var content = await cblGithubService.GetFileContent(item.Path);
            var fullPath = SaveCblFileFromContent(content, userId, item.Name);

            try
            {
                await cblImporterService.UpsertReadingList(userId, fullPath, new CblImportOptions(),
                    new CblImportDecisions());

                // Set sync tracking fields on the reading list
                var readingList = await dataContext.ReadingList
                    .FirstOrDefaultAsync(rl => rl.AppUserId == userId && rl.SourcePath == item.Path);

                if (readingList != null)
                {
                    readingList.Provider = ReadingListProvider.Url;
                    readingList.SourcePath = item.Path;
                    readingList.DownloadUrl = item.DownloadUrl;
                    readingList.ShaHash = item.Sha;
                    readingList.LastSyncedUtc = DateTime.UtcNow;
                    readingList.LastSyncCheckUtc = DateTime.UtcNow;
                    await dataContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {

            }


        }

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

    private async Task<string> SaveCblFile(IFormFile file, int userId, string filename)
    {
        var dir = Path.Join(directoryService.TempDirectory, $"{userId}", "cbl-manager-download");
        Directory.CreateDirectory(dir);
        var outputFile = Path.Join(dir, filename);
        await using var stream = System.IO.File.Create(outputFile);
        await file.CopyToAsync(stream);
        stream.Close();
        return outputFile;
    }

    private string SaveCblFileFromContent(string content, int userId, string filename)
    {
        var dir = Path.Join(directoryService.TempDirectory, $"{userId}", "cbl-manager-download");
        Directory.CreateDirectory(dir);
        var outputFile = Path.Join(dir, filename);
        System.IO.File.WriteAllText(outputFile, content);
        return outputFile;
    }

    private async Task<CblReadingList> SaveAndLoadCblFile(IFormFile file)
    {
        var filename = Path.GetRandomFileName();
        var outputFile = Path.Join(directoryService.TempDirectory, filename);
        await using var stream = System.IO.File.Create(outputFile);
        await file.CopyToAsync(stream);
        stream.Close();
        return ReadingListService.LoadCblFromPath(outputFile);
    }
}
