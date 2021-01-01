using API.Interfaces;
using Hangfire;

namespace API.Services
{
    public class TaskScheduler : ITaskScheduler
    {
        private readonly BackgroundJobServer _client;

        public TaskScheduler()
        {
            _client = new BackgroundJobServer();
        }
        
        
    }
}