using System.Threading;
using System.Threading.Tasks;

namespace API.Interfaces.Services
{
    public interface IStartupTask
    {
        Task ExecuteAsync(CancellationToken cancellationToken = default);
    }
}