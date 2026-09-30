using Hangfire.Server;

namespace Kavita.Services.SignalR;

public class JobCorrelationFilter : IServerFilter
{
    public void OnPerforming(PerformingContext context)
    {
        JobCorrelation.CurrentJobId = context.BackgroundJob.Id;
    }

    public void OnPerformed(PerformedContext context)
    {
        JobCorrelation.CurrentJobId = null;
    }
}
