using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using API.Constants;
using API.DTOs.ReadingLists.CBL;
using API.Extensions;
using API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace API.Controllers;

#nullable enable

/// <summary>
/// Responsible for the CBL import flow
/// </summary>
public class CblController : BaseApiController
{
    private readonly IReadingListService _readingListService;
    private readonly IDirectoryService _directoryService;
    private readonly ILocalizationService _localizationService;

    public CblController(IReadingListService readingListService, IDirectoryService directoryService, ILocalizationService localizationService)
    {
        _readingListService = readingListService;
        _directoryService = directoryService;
        _localizationService = localizationService;
    }

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
        var userId = User.GetUserId();
        try
        {
            var cblReadingList = await SaveAndLoadCblFile(cbl);
            var importSummary = await _readingListService.ValidateCblFile(userId, cblReadingList, useComicVineMatching);
            importSummary.FileName = cbl.FileName;

            return Ok(importSummary);
        }
        catch (ArgumentNullException)
        {
            return Ok(new CblImportSummaryDto()
            {
                FileName = cbl.FileName,
                Success = CblImportResult.Fail,
                Results = new List<CblBookResult>()
                {
                    new CblBookResult()
                    {
                        Reason = CblImportReason.InvalidFile
                    }
                }
            });
        }
        catch (InvalidOperationException)
        {
            return Ok(new CblImportSummaryDto()
            {
                FileName = cbl.FileName,
                Success = CblImportResult.Fail,
                Results = new List<CblBookResult>()
                {
                    new CblBookResult()
                    {
                        Reason = CblImportReason.InvalidFile
                    }
                }
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
    [HttpPost("import")]
    [SwaggerIgnore]
    public async Task<ActionResult<CblImportSummaryDto>> ImportCbl(IFormFile cbl, [FromQuery] bool dryRun = false, [FromQuery] bool useComicVineMatching = false)
    {
        if (User.IsInRole(PolicyConstants.ReadOnlyRole)) return BadRequest(await _localizationService.Translate(User.GetUserId(), "permission-denied"));

        try
        {
            var userId = User.GetUserId();
            var cblReadingList = await SaveAndLoadCblFile(cbl);
            var importSummary = await _readingListService.CreateReadingListFromCbl(userId, cblReadingList, dryRun, useComicVineMatching);
            importSummary.FileName = cbl.FileName;

            return Ok(importSummary);
        } catch (ArgumentNullException)
        {
            return Ok(new CblImportSummaryDto()
            {
                FileName = cbl.FileName,
                Success = CblImportResult.Fail,
                Results = new List<CblBookResult>()
                {
                    new CblBookResult()
                    {
                        Reason = CblImportReason.InvalidFile
                    }
                }
            });
        }
        catch (InvalidOperationException)
        {
            return Ok(new CblImportSummaryDto()
            {
                FileName = cbl.FileName,
                Success = CblImportResult.Fail,
                Results = new List<CblBookResult>()
                {
                    new CblBookResult()
                    {
                        Reason = CblImportReason.InvalidFile
                    }
                }
            });
        }

    }

    private async Task<CblReadingList> SaveAndLoadCblFile(IFormFile file)
    {
        var filename = Path.GetRandomFileName();
        var outputFile = Path.Join(_directoryService.TempDirectory, filename);
        await using var stream = System.IO.File.Create(outputFile);
        await file.CopyToAsync(stream);
        stream.Close();
        return ReadingListService.LoadCblFromPath(outputFile);
    }
}
