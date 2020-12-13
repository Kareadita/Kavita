namespace API.DTOs
{
    public class UserDto
    {
        public string Username { get; set; }
        public string Token { get; set; }
        public bool IsAdmin { get; set; }
    }
}