using System;
using System.Threading.Tasks;
using API.Data;

namespace API.Services;

public interface IMediaErrorService
{
    Task ReportMediaIssue(string filename, Exception ex);
}

public class MediaErrorService : IMediaErrorService
{
    private readonly IUnitOfWork _unitOfWork;

    public MediaErrorService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public Task ReportMediaIssue(string filename, Exception ex)
    {
        return Task.CompletedTask;
    }
}
