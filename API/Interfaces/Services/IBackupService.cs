using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace API.Interfaces.Services
{
    public interface IBackupService
    {
        void BackupDatabase();
        /// <summary>
        /// Returns a list of full paths of the logs files detailed in <see cref="IConfiguration"/>. 
        /// </summary>
        /// <param name="maxRollingFiles"></param>
        /// <param name="logFileName"></param>
        /// <returns></returns>
        IEnumerable<string> LogFiles(int maxRollingFiles, string logFileName);
    }
}