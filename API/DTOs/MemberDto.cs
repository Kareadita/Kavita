using System;

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
        public bool IsAdmin { get; set; }
    }
}