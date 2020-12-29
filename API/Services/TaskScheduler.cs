using API.Interfaces;
using Hangfire;

namespace API.Services
{
    public class TaskScheduler : ITaskScheduler
    {
        private BackgroundJobServer Client { get; }

        public TaskScheduler()
        {
            Client = new BackgroundJobServer();
        }
        
        
    }
}