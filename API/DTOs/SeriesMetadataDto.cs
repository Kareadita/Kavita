using System.Collections.Generic;
using API.Entities;

namespace API.DTOs
{
    public class SeriesMetadataDto
    {
        public int Id { get; set; }
        public virtual ICollection<string> Genres { get; set; }
        public virtual ICollection<string> Tags { get; set; }
        public virtual ICollection<Person> Persons { get; set; }
        public string Publisher { get; set; }
        public int SeriesId { get; set; }
    }
}