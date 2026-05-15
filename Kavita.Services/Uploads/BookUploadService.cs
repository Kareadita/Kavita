using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.BookUpload;
using Kavita.Models.Entities.Enums;
using Kavita.Services.Extensions;
using Kavita.Services.Scanner;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Uploads;

public class BookUploadService(
    IUnitOfWork unitOfWork,
    IDirectoryService directoryService,
    ITaskScheduler taskScheduler,
    ILogger<BookUploadService> logger)
    : IBookUploadService
{
    private static readonly IReadOnlyDictionary<FileTypeGroup, string[]> FileTypeExtensions =
        new Dictionary<FileTypeGroup, string[]>
        {
            [FileTypeGroup.Archive] = [".cbz", ".zip", ".rar", ".cbr", ".tar.gz", ".7zip", ".7z", ".cb7", ".cbt"],
            [FileTypeGroup.Epub] = [".epub"],
            [FileTypeGroup.Pdf] = [".pdf"],
            [FileTypeGroup.Images] = [".png", ".jpeg", ".jpg", ".webp", ".gif", ".avif"],
        };

    public async Task<BookUploadOptionsDto?> GetOptionsAsync(int libraryId, CancellationToken ct = default)
    {
        var library = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(libraryId,
            LibraryIncludes.Folders | LibraryIncludes.FileTypes, ct);
        if (library == null) return null;

        var fileTypes = library.LibraryFileTypes.Select(t => t.FileTypeGroup).Distinct().ToList();

        return new BookUploadOptionsDto
        {
            LibraryId = library.Id,
            LibraryFolders = library.Folders.Select(f => f.Path).ToList(),
            LibraryFileTypes = fileTypes,
            AcceptableExtensions = GetExtensions(fileTypes).ToList(),
        };
    }

    public async Task<BookUploadResponseDto> UploadFilesAsync(BookUploadRequestDto request, BookUploadFile[] files,
        CancellationToken ct = default)
    {
        var library = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(request.LibraryId,
            LibraryIncludes.Folders | LibraryIncludes.FileTypes, ct);
        if (library == null)
        {
            return FailedResponse(files, "Library does not exist");
        }

        var libraryFolder = ResolveLibraryFolder(library.Folders.Select(f => f.Path), request.LibraryFolder);
        if (string.IsNullOrEmpty(libraryFolder))
        {
            return FailedResponse(files, "Selected folder does not belong to this library");
        }

        var scanFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<BookUploadFileResultDto>();

        foreach (var file in files)
        {
            var result = await UploadFileAsync(libraryFolder, library.LibraryFileTypes.Select(t => t.FileTypeGroup),
                request.TargetFolderName, request.ConflictMode, file, ct);
            results.Add(result);

            if (result.Success && !string.IsNullOrEmpty(result.DestinationPath))
            {
                scanFolders.Add(directoryService.FileSystem.Path.GetDirectoryName(result.DestinationPath)!);
            }
        }

        foreach (var scanFolder in scanFolders)
        {
            taskScheduler.ScanFolder(scanFolder);
        }

        return new BookUploadResponseDto
        {
            Files = results.Select(r => r with {ScanQueued = r.Success}).ToList(),
        };
    }

    private async Task<BookUploadFileResultDto> UploadFileAsync(string libraryFolder, IEnumerable<FileTypeGroup> fileTypes,
        string? targetFolderName, BookUploadConflictMode conflictMode, BookUploadFile uploadFile, CancellationToken ct)
    {
        if (uploadFile.Length <= 0)
        {
            return Failure(uploadFile.FileName, "File is empty");
        }

        if (uploadFile.Length > ControllerConstants.MaxBookUploadSizeBytes)
        {
            return Failure(uploadFile.FileName, "File is too large");
        }

        var safeFileName = GetSafeFileName(uploadFile.FileName);
        if (string.IsNullOrEmpty(safeFileName))
        {
            return Failure(uploadFile.FileName, "Invalid file name");
        }

        if (!IsAllowedFileType(safeFileName, fileTypes))
        {
            return Failure(uploadFile.FileName, "File type is not enabled for this library");
        }

        var safeTargetFolder = GetSafePathSegment(string.IsNullOrWhiteSpace(targetFolderName)
            ? Parser.RemoveExtensionIfSupported(safeFileName)
            : targetFolderName);
        if (string.IsNullOrEmpty(safeTargetFolder))
        {
            return Failure(uploadFile.FileName, "Invalid target folder");
        }

        var destinationFolder = directoryService.FileSystem.Path.Join(libraryFolder, safeTargetFolder);
        var destinationRoot = NormalizeFullPath(libraryFolder);
        var destinationFolderFullPath = NormalizeFullPath(destinationFolder);
        if (!IsSameOrChildPath(destinationRoot, destinationFolderFullPath))
        {
            return Failure(uploadFile.FileName, "Invalid target folder");
        }

        if (!directoryService.ExistOrCreate(destinationFolderFullPath))
        {
            return Failure(uploadFile.FileName, "Unable to create target folder");
        }

        if (!CanWriteToDirectory(destinationFolderFullPath))
        {
            return Failure(uploadFile.FileName, "Kavita cannot write to the target folder");
        }

        var destinationPath = ResolveDestinationPath(destinationFolderFullPath, safeFileName, conflictMode);
        if (string.IsNullOrEmpty(destinationPath))
        {
            return Failure(uploadFile.FileName, "A file with this name already exists");
        }

        var tempDirectory = directoryService.FileSystem.Path.Join(directoryService.TempDirectory, "uploads",
            Guid.NewGuid().ToString("N"));
        var tempPath = directoryService.FileSystem.Path.Join(tempDirectory, safeFileName);

        try
        {
            directoryService.ExistOrCreate(tempDirectory);

            await using (var input = uploadFile.OpenReadStream())
            await using (var output = directoryService.FileSystem.File.Create(tempPath))
            {
                await input.CopyToAsync(output, ct);
            }

            directoryService.FileSystem.File.Move(tempPath, destinationPath);

            return new BookUploadFileResultDto
            {
                FileName = uploadFile.FileName,
                Success = true,
                DestinationPath = destinationPath,
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to upload book file {FileName}", uploadFile.FileName);
            return Failure(uploadFile.FileName, "Upload failed");
        }
        finally
        {
            directoryService.ClearAndDeleteDirectory(tempDirectory);
        }
    }

    private string? ResolveLibraryFolder(IEnumerable<string> libraryFolders, string selectedFolder)
    {
        var selected = NormalizeFullPath(selectedFolder);
        return libraryFolders
            .Select(NormalizeFullPath)
            .FirstOrDefault(folder => PathsEqual(folder, selected));
    }

    private static IEnumerable<string> GetExtensions(IEnumerable<FileTypeGroup> fileTypes)
    {
        return fileTypes
            .Where(FileTypeExtensions.ContainsKey)
            .SelectMany(type => FileTypeExtensions[type])
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static BookUploadResponseDto FailedResponse(IEnumerable<BookUploadFile> files, string error)
    {
        return new BookUploadResponseDto
        {
            Files = files.Select(file => Failure(file.FileName, error)).ToList(),
        };
    }

    private static BookUploadFileResultDto Failure(string fileName, string error)
    {
        return new BookUploadFileResultDto
        {
            FileName = fileName,
            Success = false,
            Error = error,
        };
    }

    private static bool IsAllowedFileType(string fileName, IEnumerable<FileTypeGroup> fileTypes)
    {
        var allowedRegex = string.Join('|', fileTypes.Distinct().Select(type => type.GetRegex()));
        if (string.IsNullOrEmpty(allowedRegex)) return false;

        return Regex.IsMatch(fileName, $"({allowedRegex})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            Parser.RegexTimeout);
    }

    private string GetSafeFileName(string fileName)
    {
        var safeFileName = directoryService.FileSystem.Path.GetFileName(fileName).Trim();
        return GetSafePathSegment(safeFileName);
    }

    private string GetSafePathSegment(string? pathSegment)
    {
        if (string.IsNullOrWhiteSpace(pathSegment)) return string.Empty;

        var cleaned = pathSegment.Trim();
        if (cleaned is "." or ".." || cleaned.Contains('/') || cleaned.Contains('\\'))
        {
            return string.Empty;
        }

        foreach (var invalidChar in directoryService.FileSystem.Path.GetInvalidFileNameChars())
        {
            cleaned = cleaned.Replace(invalidChar, '_');
        }

        cleaned = cleaned.Trim();
        return string.IsNullOrWhiteSpace(cleaned) || cleaned is "." or ".." ? string.Empty : cleaned;
    }

    private string? ResolveDestinationPath(string destinationFolder, string fileName, BookUploadConflictMode conflictMode)
    {
        var destinationPath = directoryService.FileSystem.Path.Join(destinationFolder, fileName);
        if (!directoryService.FileSystem.File.Exists(destinationPath)) return destinationPath;
        if (conflictMode == BookUploadConflictMode.Reject) return null;

        var (nameWithoutExtension, extension) = SplitExtension(fileName);
        for (var i = 1; i <= 1000; i++)
        {
            destinationPath = directoryService.FileSystem.Path.Join(destinationFolder, $"{nameWithoutExtension} ({i}){extension}");
            if (!directoryService.FileSystem.File.Exists(destinationPath)) return destinationPath;
        }

        return null;
    }

    private static (string NameWithoutExtension, string Extension) SplitExtension(string fileName)
    {
        const string TarGzExtension = ".tar.gz";
        if (fileName.EndsWith(TarGzExtension, StringComparison.OrdinalIgnoreCase))
        {
            return (fileName[..^TarGzExtension.Length], TarGzExtension);
        }

        return (Path.GetFileNameWithoutExtension(fileName), Path.GetExtension(fileName));
    }

    private bool CanWriteToDirectory(string directoryPath)
    {
        var testFile = directoryService.FileSystem.Path.Join(directoryPath, $".kavita-upload-{Guid.NewGuid():N}.tmp");
        try
        {
            var testStream = directoryService.FileSystem.File.Create(testFile);
            testStream.Dispose();

            directoryService.FileSystem.File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private string NormalizeFullPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;

        try
        {
            return Parser.NormalizePath(directoryService.FileSystem.Path.GetFullPath(path)).TrimEnd('/');
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(left.TrimEnd('/'), right.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSameOrChildPath(string root, string candidate)
    {
        root = root.TrimEnd('/');
        candidate = candidate.TrimEnd('/');

        return string.Equals(root, candidate, StringComparison.OrdinalIgnoreCase)
               || candidate.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);
    }
}
