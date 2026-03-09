using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Kavita.API.Attributes;
using Kavita.API.Services;
using Kavita.API.Services.Reading;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.ReadingLists.CBL;
using Kavita.Server.Attributes;
using Kavita.Services.Reading;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Kavita.Server.Controllers;

/// <summary>
/// Responsible for the CBL import flow
/// </summary>
public class CblController(
    IReadingListService readingListService,
    IDirectoryService directoryService)
    : BaseApiController
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
