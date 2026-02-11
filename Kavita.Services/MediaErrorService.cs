using System;
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
        BackgroundJob.Enqueue(() => ReportMediaIssueAsync(filename, producer, errorMessage, ex.Message));
    }

    public void ReportMediaIssue(string filename, MediaErrorProducer producer, string errorMessage, string details)
    {
        // To avoid overhead on commits, do async. We don't need to wait.
        BackgroundJob.Enqueue(() => ReportMediaIssueAsync(filename, producer, errorMessage, details));
    }

    public async Task ReportMediaIssueAsync(string filename, MediaErrorProducer producer, string errorMessage, Exception ex)
    {
        await ReportMediaIssueAsync(filename, producer, errorMessage, ex.Message);
    }

    public async Task ReportMediaIssueAsync(string filename, MediaErrorProducer producer, string errorMessage, string details)
    {
        var error = new MediaErrorBuilder(filename)
            .WithComment(errorMessage)
            .WithDetails(details)
            .Build();

        if (await unitOfWork.MediaErrorRepository.ExistsAsync(error))
        {
            return;
        }


        unitOfWork.MediaErrorRepository.Attach(error);
        await unitOfWork.CommitAsync();
    }

}
