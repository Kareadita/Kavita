using System.Collections.Generic;

namespace Kavita.Models.DTOs.ReadingLists.CBL.Import;

public class CblRepoImportRequestDto
{
    public IList<CblRepoItemDto> Items { get; set; } = [];
}
