using System;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.Entities.Enums;

namespace Kavita.API.Services;

public interface IMediaErrorService
{
    void ReportMediaIssue(string filePath, MediaErrorProducer producer, MediaErrorReason reason, string details);
    void ReportMediaIssue(string filePath, MediaErrorProducer producer, MediaErrorReason reason, Exception ex);
    Task ReportMediaIssueAsync(string filePath, MediaErrorProducer producer, MediaErrorReason reason, string details, CancellationToken ct = default);
    Task ReportMediaIssueAsync(string filePath, MediaErrorProducer producer, MediaErrorReason reason, Exception ex, CancellationToken ct = default);
}
