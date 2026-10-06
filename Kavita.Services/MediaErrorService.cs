using System;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.Common.Extensions;
using Kavita.Models.Builders;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Services.Scanner;

namespace Kavita.Services;

public class MediaErrorService(IUnitOfWork unitOfWork, IDirectoryService directoryService) : IMediaErrorService
{
    public void ReportMediaIssue(string filePath, MediaErrorProducer producer, MediaErrorReason reason, Exception ex)
    {
        ReportMediaIssue(filePath, producer, reason, ParseIssues.Describe(ex));
    }

    public void ReportMediaIssue(string filePath, MediaErrorProducer producer, MediaErrorReason reason, string details)
    {
        // To avoid overhead on commits, do async. We don't need to wait.
        BackgroundJob.Enqueue(() => ReportMediaIssueAsync(filePath, producer, reason, details, CancellationToken.None));
    }

    public Task ReportMediaIssueAsync(string filePath, MediaErrorProducer producer, MediaErrorReason reason, Exception ex, CancellationToken ct = default)
    {
        return ReportMediaIssueAsync(filePath, producer, reason, ParseIssues.Describe(ex), ct);
    }

    public async Task ReportMediaIssueAsync(string filePath, MediaErrorProducer producer, MediaErrorReason reason,
        string details, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(filePath)) return;

        var path = filePath.NormalizePath();
        var file = directoryService.FileSystem.FileInfo.New(path);

        var error = await unitOfWork.MediaErrorRepository.GetProducerErrorAsync(path, reason, ct);
        if (error != null && HasSameStamp(error, file)) return;

        if (error == null)
        {
            error = new MediaErrorBuilder(path)
                .WithProducer(producer)
                .WithReason(reason)
                .Build();
            unitOfWork.MediaErrorRepository.Attach(error);
        }
        else
        {
            error.IsDismissed = false;
        }

        if (error.SeriesId == null)
        {
            var owner = await unitOfWork.MediaErrorRepository.GetOwnerAsync(path, ct);
            error.LibraryId = owner.LibraryId ?? error.LibraryId;
            error.SeriesId = owner.SeriesId;
        }

        error.Bytes = file.Exists ? file.Length : null;
        error.FileLastWriteTimeUtc = file.Exists ? file.LastWriteTimeUtc : null;
        error.Details = details.Trim();
        error.LastSeenUtc = DateTime.UtcNow;

        await unitOfWork.CommitAsync(ct);
    }

    /// <summary>Is the Media Info the same as the file based on bytes and last write time</summary>
    private static bool HasSameStamp(MediaError error, IFileInfo file)
    {
        if (!file.Exists) return error.Bytes == null;

        return error.Bytes == file.Length && error.FileLastWriteTimeUtc is { } writeTime &&
               FolderChangeCheck.IsSameWriteTime(writeTime, file.LastWriteTimeUtc);
    }
}
