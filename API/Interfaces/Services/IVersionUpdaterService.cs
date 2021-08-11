using System;
using System.Threading.Tasks;

namespace API.Interfaces.Services
{
    public interface IVersionUpdaterService
    {
        public Task CheckForUpdate();

    }
}
