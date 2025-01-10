using System.Collections.Generic;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Email;
using API.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[Authorize(Policy = "RequireAdminRole")]
public class EmailController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;

    public EmailController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [HttpGet("all")]
    public async Task<ActionResult<IList<EmailHistoryDto>>> GetEmails()
    {
        return Ok(await _unitOfWork.EmailHistoryRepository.GetEmailDtos(UserParams.Default));
    }
}
