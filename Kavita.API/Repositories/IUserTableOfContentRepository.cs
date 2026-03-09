using System.Collections.Generic;
using System.Threading.Tasks;
using Kavita.Models.DTOs.Reader;
using Kavita.Models.Entities.User;

namespace Kavita.API.Repositories;

public interface IUserTableOfContentRepository
{
    void Attach(AppUserTableOfContent toc);
    void Remove(AppUserTableOfContent toc);
    Task<bool> IsUnique(int userId, int chapterId, int page, string title);
    Task<List<PersonalToCDto>> GetPersonalToC(int userId, int chapterId);
    Task<List<PersonalToCDto>> GetPersonalToCForPage(int userId, int chapterId, int page);
    Task<AppUserTableOfContent?> Get(int userId, int chapterId, int pageNum, string title);
}
