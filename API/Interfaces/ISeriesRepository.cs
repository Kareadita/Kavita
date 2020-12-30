using System.Threading.Tasks;
using API.Entities;

namespace API.Interfaces
{
    public interface ISeriesRepository
    {
        void Update(Series series);
        Task<bool> SaveAllAsync();
        Task<Series> GetSeriesByNameAsync(string name);
        Series GetSeriesByName(string name);
        bool SaveAll();
    }
}