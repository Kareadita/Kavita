using System;
using System.Collections.Generic;
using API.Entities.Interfaces;

namespace API.Entities
{
    public class Volume : IEntityDate
    {
        public int Id { get; set; }
        /// <summary>
        /// A String representation of the volume number. Allows for floats
        /// </summary>
        public string Name { get; set; }
        public int Number { get; set; }
        public IList<Chapter> Chapters { get; set; }
        public DateTime Created { get; set; }
        public DateTime LastModified { get; set; }
        /// <summary>
        /// Absolute path to the (managed) image file
        /// </summary>
        /// <remarks>The file is managed internally to Kavita's APPDIR</remarks>
        public string CoverImage { get; set; }
        public int Pages { get; set; }



        // Relationships
        public Series Series { get; set; }
        public int SeriesId { get; set; }
    }
}
