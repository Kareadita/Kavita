using System.Collections.Generic;

namespace Kavita.Models.DTOs.ReadingLists.CBL;

public class CblRepoImportRequestDto
{
    public IList<CblRepoItemDto> Items { get; set; } = [];
}
