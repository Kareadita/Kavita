using System;
using System.Collections.Generic;
using API.Entities.Interfaces;

namespace API.Entities
{
    public class Volume : IEntityDate
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Number { get; set; }
        public IList<Chapter> Chapters { get; set; }
        public DateTime Created { get; set; }
        public DateTime LastModified { get; set; }
        public byte[] CoverImage { get; set; }
        public int Pages { get; set; }

        /// <summary>
        /// Represents a Side story that is linked to the original Series. Omake, One Shot, etc.
        /// </summary>
        public bool IsSpecial { get; set; } = false;
        
        

        // Relationships
        public Series Series { get; set; }
        public int SeriesId { get; set; }
    }
}