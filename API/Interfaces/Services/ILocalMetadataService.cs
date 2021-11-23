using System.Threading.Tasks;
using API.Entities;

namespace API.Interfaces.Services
{
    public interface ILocalMetadataService
    {
        Task RefreshMetadataForSeries(Series series, bool forceUpdate);
    }
}
