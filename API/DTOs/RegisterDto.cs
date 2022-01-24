﻿using System.Collections;
using System.ComponentModel.DataAnnotations;

namespace API.DTOs
{
    public class RegisterDto
    {
        [Required]
        public string Username { get; init; }
        // [Required]
        // public string Email { get; init; }
        [Required]
        [StringLength(32, MinimumLength = 6)]
        public string Password { get; set; }
        public bool IsAdmin { get; init; }
    }
}
