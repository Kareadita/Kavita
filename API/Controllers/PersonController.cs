using API.Data;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

public class PersonController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;

    public PersonController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [HttpGet("{:personId}")]
    public ActionResult GetPerson(int personId)
    {
        return Ok();
    }
}
