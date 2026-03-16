using System.Collections.Generic;
using System.Threading.Tasks;
using Kavita.Models.DTOs;

namespace Kavita.API.Services;

public interface ILocalizationService
{
    Task<string> Get(string locale, string key, params object[] args);
    Task<string> Translate(int userId, string key, params object[] args);
    IEnumerable<KavitaLocale> GetLocales();
}
