using System.Collections.Generic;
using Kavita.Models.Constants;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.DTOs.BookUpload;

public sealed record BookUploadOptionsDto
{
    public int LibraryId { get; init; }
    public ICollection<string> LibraryFolders { get; init; } = new List<string>();
    public ICollection<FileTypeGroup> LibraryFileTypes { get; init; } = new List<FileTypeGroup>();
    public ICollection<string> AcceptableExtensions { get; init; } = new List<string>();
    public long MaxUploadSizeBytes { get; init; } = ControllerConstants.MaxBookUploadSizeBytes;
}
