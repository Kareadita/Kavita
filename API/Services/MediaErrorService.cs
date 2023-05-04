using System;
using System.Threading.Tasks;
using API.Data;

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

    public Task ReportMediaIssueAsync(string filename, MediaErrorProducer producer, string errorMessage, Exception ex)
    {
        return Task.CompletedTask;
    }

    public void ReportMediaIssue(string filename, MediaErrorProducer producer, string errorMessage, Exception ex)
    {

    }
}
