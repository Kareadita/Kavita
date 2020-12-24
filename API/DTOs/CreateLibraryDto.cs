using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using API.Entities;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;

namespace API.DTOs
{
    public class CreateLibraryDto
    {
        [Required]
        public string Name { get; set; }
        [Required]
        public LibraryType Type { get; set; }
        [Required]
        [MinLength(1)]
        public IEnumerable<string> Folders { get; set; }
    }
}