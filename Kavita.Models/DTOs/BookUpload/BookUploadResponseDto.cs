using System.Collections.Generic;
using System.Linq;

namespace Kavita.Models.DTOs.BookUpload;

public sealed record BookUploadResponseDto
{
    public ICollection<BookUploadFileResultDto> Files { get; init; } = new List<BookUploadFileResultDto>();
    public bool Success => Files.Count > 0 && Files.All(f => f.Success);
    public bool ScanQueued => Files.Any(f => f.ScanQueued);
}
