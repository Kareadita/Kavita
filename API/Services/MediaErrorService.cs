using System;
using System.Threading.Tasks;
using API.Data;
using API.Helpers.Builders;
using Hangfire;

namespace API.Services;

public enum MediaErrorProducer
{
    BookService = 0,
    ArchiveService = 1

}

public interface IMediaErrorService
{
    Task ReportMediaIssueAsync(string filename, MediaErrorProducer producer, string errorMessage, Exception ex);
    void ReportMediaIssue(string filename, MediaErrorProducer producer, string errorMessage, Exception ex);
}

public class MediaErrorService : IMediaErrorService
{
    private readonly IUnitOfWork _unitOfWork;

    public MediaErrorService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task ReportMediaIssueAsync(string filename, MediaErrorProducer producer, string errorMessage, Exception ex)
    {
        var error = new MediaErrorBuilder(filename)
            .WithComment(errorMessage)
            .WithDetails(ex.Message)
            .Build();

        _unitOfWork.MediaErrorRepository.Attach(error);
        await _unitOfWork.CommitAsync();
    }

    public void ReportMediaIssue(string filename, MediaErrorProducer producer, string errorMessage, Exception ex)
    {
        // To avoid overhead on commits, do async. We don't need to wait.
        BackgroundJob.Enqueue(() => ReportMediaIssueAsync(filename, producer, errorMessage, ex));
    }
}
