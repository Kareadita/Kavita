using System;
using System.Collections.Generic;
using API.Entities.Interfaces;
using Microsoft.EntityFrameworkCore;

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



        // Relationships
        public Series Series { get; set; }
        public int SeriesId { get; set; }
    }
}
