using System;
using System.Threading.Tasks;
using Kavita.Models.Entities.Enums;

namespace Kavita.API.Services;

public interface IMediaErrorService
{
    void ReportMediaIssue(string filename, MediaErrorProducer producer, string errorMessage, string details);
    void ReportMediaIssue(string filename, MediaErrorProducer producer, string errorMessage, Exception ex);
    Task ReportMediaIssueAsync(string filename, MediaErrorProducer producer, string errorMessage, string details);
    Task ReportMediaIssueAsync(string filename, MediaErrorProducer producer, string errorMessage, Exception ex);
}
