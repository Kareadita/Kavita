using System;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.Models.Builders;
using Kavita.Models.Entities.Enums;

namespace Kavita.Services;



public class MediaErrorService(IUnitOfWork unitOfWork) : IMediaErrorService
{
    public void ReportMediaIssue(string filename, MediaErrorProducer producer, string errorMessage, Exception ex)
    {
        // TODO: Localize all these messages
        // To avoid overhead on commits, do async. We don't need to wait.
        BackgroundJob.Enqueue(() => ReportMediaIssueAsync(filename, producer, errorMessage, ex.Message, CancellationToken.None));
    }

    public void ReportMediaIssue(string filename, MediaErrorProducer producer, string errorMessage, string details)
    {
        // To avoid overhead on commits, do async. We don't need to wait.
        BackgroundJob.Enqueue(() => ReportMediaIssueAsync(filename, producer, errorMessage, details, CancellationToken.None));
    }

    public async Task ReportMediaIssueAsync(string filename, MediaErrorProducer producer, string errorMessage, Exception ex, CancellationToken ct = default)
    {
        await ReportMediaIssueAsync(filename, producer, errorMessage, ex.Message, ct);
    }

    public async Task ReportMediaIssueAsync(string filename, MediaErrorProducer producer, string errorMessage,
        string details, CancellationToken ct = default)
    {
        var error = new MediaErrorBuilder(filename)
            .WithComment(errorMessage)
            .WithDetails(details)
            .Build();

        if (await unitOfWork.MediaErrorRepository.ExistsAsync(error, ct))
        {
            return;
        }


        unitOfWork.MediaErrorRepository.Attach(error);
        await unitOfWork.CommitAsync(ct);
    }

}
