using System.Linq;
using System.Threading.Tasks;
using Kavita.API.Services;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.BookUpload;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kavita.Server.Controllers;

[Authorize(Policy = PolicyGroups.AdminPolicy)]
[Route("api/book-upload")]
public class BookUploadController(IBookUploadService bookUploadService) : BaseApiController
{
    [HttpGet("options")]
    public async Task<ActionResult<BookUploadOptionsDto>> GetOptions(int libraryId)
    {
        var options = await bookUploadService.GetOptionsAsync(libraryId, HttpContext.RequestAborted);
        if (options == null) return BadRequest("Library does not exist");

        return Ok(options);
    }

    [HttpPost("files")]
    [RequestSizeLimit(ControllerConstants.MaxBookUploadSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = ControllerConstants.MaxBookUploadSizeBytes)]
    public async Task<ActionResult<BookUploadResponseDto>> UploadFiles([FromForm] BookUploadRequestDto request,
        [FromForm] IFormFile[] files)
    {
        if (files.Length == 0) return BadRequest("No files were uploaded");

        var uploadFiles = files
            .Select(file => new BookUploadFile(file.FileName, file.Length, file.OpenReadStream))
            .ToArray();

        return Ok(await bookUploadService.UploadFilesAsync(request, uploadFiles, HttpContext.RequestAborted));
    }
}
