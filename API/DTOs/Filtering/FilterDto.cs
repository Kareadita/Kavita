using System.Collections;
using System.Collections.Generic;
using API.Entities.Enums;

namespace API.DTOs.Filtering
{
    public class FilterDto
    {
        /// <summary>
        /// The type of Formats you want to be returned. An empty list will return all formats back
        /// </summary>
        public IList<MangaFormat> Formats { get; init; } = new List<MangaFormat>();

    }
}
