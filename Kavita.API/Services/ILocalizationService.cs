using System.Collections.Generic;
using System.Threading.Tasks;
using Kavita.Models.DTOs;

namespace Kavita.API.Services;

public interface ILocalizationService
{
    Task<string> Get(string locale, string key, params object[] args);
    /// <summary>
    /// Returns a translated string for the currently authenticated user (Via <see cref="Kavita.API.Store.IUserContext"/>).
    /// Falling back to English or the key
    /// </summary>
    /// <param name="key"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    Task<string> Translate(string key, params object[] args);
    /// <summary>
    /// Returns a translated string for a given user's locale, falling back to english or the key if missing
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="key"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    Task<string> Translate(int userId, string key, params object[] args);
    IEnumerable<KavitaLocale> GetLocales();
}
