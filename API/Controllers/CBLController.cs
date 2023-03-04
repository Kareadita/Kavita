using System.IO;
using System.Threading.Tasks;
using API.DTOs.ReadingLists.CBL;
using API.Extensions;
using API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Responsible for the CBL import flow
/// </summary>
public class CblController : BaseApiController
{
    private readonly IReadingListService _readingListService;
    private readonly IDirectoryService _directoryService;

    public CblController(IReadingListService readingListService, IDirectoryService directoryService)
    {
        _readingListService = readingListService;
        _directoryService = directoryService;
    }

    /// <summary>
    /// The first step in a cbl import. This validates the cbl file that if an import occured, would it be successful.
    /// If this returns errors, the cbl will always be rejected by Kavita.
    /// </summary>
    /// <param name="file">FormBody with parameter name of cbl</param>
    /// <returns></returns>
    [HttpPost("validate")]
    public async Task<ActionResult<CblImportSummaryDto>> ValidateCbl([FromForm(Name = "cbl")] IFormFile file)
    {
        var userId = User.GetUserId();
        var cbl = await SaveAndLoadCblFile(userId, file);

        var importSummary = await _readingListService.ValidateCblFile(userId, cbl);
        return Ok(importSummary);
    }


    /// <summary>
    /// Performs the actual import (assuming dryRun = false)
    /// </summary>
    /// <param name="file">FormBody with parameter name of cbl</param>
    /// <param name="dryRun">If true, will only emulate the import but not perform. This should be done to preview what will happen</param>
    /// <returns></returns>
    [HttpPost("import")]
    public async Task<ActionResult<CblImportSummaryDto>> ImportCbl([FromForm(Name = "cbl")] IFormFile file, [FromForm(Name = "dryRun")] bool dryRun = false)
    {
        var userId = User.GetUserId();
        var cbl = await SaveAndLoadCblFile(userId, file);

        return Ok(await _readingListService.CreateReadingListFromCbl(userId, cbl, dryRun));
    }

    private async Task<CblReadingList> SaveAndLoadCblFile(int userId, IFormFile file)
    {
        var filename = Path.GetRandomFileName();
        var outputFile = Path.Join(_directoryService.TempDirectory, filename);
        await using var stream = System.IO.File.Create(outputFile);
        await file.CopyToAsync(stream);
        stream.Close();
        return ReadingListService.LoadCblFromPath(outputFile);
    }
}
