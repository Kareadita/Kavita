using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

public class ChapterController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;

    public ChapterController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [HttpGet]
    public async Task<ActionResult<ChapterDto>> GetChapter(int chapterId)
    {
        var chapter =
            await _unitOfWork.ChapterRepository.GetChapterDtoAsync(chapterId,
                ChapterIncludes.People | ChapterIncludes.Files);

        return Ok(chapter);
    }


}
