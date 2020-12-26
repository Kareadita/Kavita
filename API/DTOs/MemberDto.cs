using System;
using System.Collections.Generic;

namespace API.DTOs
{
    /// <summary>
    /// Represents a member of a Kavita server. 
    /// </summary>
    public class MemberDto
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public DateTime Created { get; set; }
        public DateTime LastActive { get; set; }
        public IEnumerable<LibraryDto> Libraries { get; set; }
        public IEnumerable<string> Roles { get; set; }
    }
}