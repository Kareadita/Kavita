using System.Collections.Generic;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.Common.Helpers;
using Kavita.Models.Constants;
using Kavita.Models.DTOs.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kavita.Server.Controllers;

[Authorize(Policy = PolicyGroups.AdminPolicy)]
public class EmailController(IUnitOfWork unitOfWork) : BaseApiController
{
    [HttpGet("all")]
    public async Task<ActionResult<IList<EmailHistoryDto>>> GetEmails()
    {
        return Ok(await unitOfWork.EmailHistoryRepository.GetEmailDtos(UserParams.Default));
    }
}
