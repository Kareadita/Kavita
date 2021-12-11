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

        /// <summary>
        /// The progress you want to be returned. This can be bitwise manipulated. Defaults to all applicable states.
        /// </summary>
        public ReadStatus ReadStatus { get; init; } = ReadStatus.All;

        /// <summary>
        /// A list of library ids to restrict search to. Defaults to all libraries by passing empty list
        /// </summary>
        public IList<int> Libraries { get; init; } = new List<int>();

    }
}
