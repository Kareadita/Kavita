using System.Collections;
using System.Collections.Generic;
using API.Entities.Enums;

namespace API.DTOs.Filtering
{
    public class FilterDto
    {
        /// <summary>
        /// Pass null if you want all formats
        /// </summary>
        public MangaFormat? MangaFormat { get; init; } = null;

        //public ICollection<MangaFormat> Formats { get; init; } = null;

    }
}
