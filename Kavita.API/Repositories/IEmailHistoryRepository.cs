using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Common.Helpers;
using Kavita.Models.DTOs.Email;

namespace Kavita.API.Repositories;

public interface IEmailHistoryRepository
{
    Task<IList<EmailHistoryDto>> GetEmailDtos(UserParams userParams, CancellationToken ct = default);
}
