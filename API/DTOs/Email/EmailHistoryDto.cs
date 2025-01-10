using System;

namespace API.DTOs.Email;

public class EmailHistoryDto
{
    public long Id { get; set; }
    public bool Sent { get; set; }
    public DateTime SendDate { get; set; } = DateTime.UtcNow;
    public string EmailTemplate { get; set; }
    public string ErrorMessage { get; set; }
    public string ToUserName { get; set; }

}
