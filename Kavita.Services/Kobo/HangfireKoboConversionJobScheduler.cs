using System.Threading;
using Hangfire;
using Kavita.API.Services;

namespace Kavita.Services.Kobo;

public class HangfireKoboConversionJobScheduler : IKoboConversionJobScheduler
{
    public void EnqueueBackgroundConvert(int chapterId)
    {
        object[] args = [chapterId, CancellationToken.None];
        if (TaskScheduler.HasAlreadyEnqueuedTask(KoboConversionService.Name, "ConvertChapterInBackgroundAsync",
                args) ||
            TaskScheduler.HasAlreadyEnqueuedTask("IKoboConversionService", "ConvertChapterInBackgroundAsync", args))
        {
            return;
        }

        BackgroundJob.Enqueue<IKoboConversionService>(s =>
            s.ConvertChapterInBackgroundAsync(chapterId, CancellationToken.None));
    }
}
