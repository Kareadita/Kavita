﻿using System.ComponentModel.DataAnnotations;

namespace API.DTOs
{
    public class ResetPasswordDto
    {
        [Required]
        public string UserName { get; init; }
        [Required]
        [StringLength(16, MinimumLength = 4)]
        public string Password { get; init; }
    }
}