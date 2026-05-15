using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs.BookUpload;

namespace Kavita.API.Services;

public sealed record BookUploadFile(string FileName, long Length, Func<Stream> OpenReadStream);

public interface IBookUploadService
{
    Task<BookUploadOptionsDto?> GetOptionsAsync(int libraryId, CancellationToken ct = default);
    Task<BookUploadResponseDto> UploadFilesAsync(BookUploadRequestDto request, BookUploadFile[] files, CancellationToken ct = default);
}
