
using System;
using System.Collections.Generic;
using API.DTOs.Reader;
using API.Entities.Interfaces;

namespace API.DTOs
{
    public class VolumeDto : IHasReadTimeEstimate
    {
        public int Id { get; set; }
        public int Number { get; set; }
        public string Name { get; set; }
        public int Pages { get; set; }
        public int PagesRead { get; set; }
        public DateTime LastModified { get; set; }
        public DateTime Created { get; set; }
        public int SeriesId { get; set; }
        public ICollection<ChapterDto> Chapters { get; set; }
        /// <summary>
        /// Estimated Time Range to read
        /// </summary>
        /// <remarks>This is not set normally, only for Series Detail</remarks>
        [Obsolete]
        public HourEstimateRangeDto TimeEstimate { get; set; }

        /// <inheritdoc cref="IHasReadTimeEstimate.MinHoursToRead"/>
        public int MinHoursToRead { get; set; }
        /// <inheritdoc cref="IHasReadTimeEstimate.MaxHoursToRead"/>
        public int MaxHoursToRead { get; set; }
        /// <inheritdoc cref="IHasReadTimeEstimate.AvgHoursToRead"/>
        public int AvgHoursToRead { get; set; }
    }
}
