using System.Threading;
using Hangfire;
using Kavita.API.Services;

namespace Kavita.Services.Kobo;

public class HangfireKoboConversionJobScheduler : IKoboConversionJobScheduler
{
    public void EnqueueBackgroundConvert(int chapterId)
    {
        BackgroundJob.Enqueue<IKoboConversionService>(s =>
            s.ConvertChapterInBackgroundAsync(chapterId, CancellationToken.None));
    }
}
