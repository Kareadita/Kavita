using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using API.Converters;

namespace API.DTOs
{
    public class RegisterDto
    {
        [Required]
        public string Username { get; set; }
        [Required]
        [StringLength(8, MinimumLength = 4)]
        public string Password { get; set; }
        [JsonConverter(typeof(JsonBoolNumberConverter))]
        public bool IsAdmin { get; set; }
    }
}