namespace API.DTOs.Settings;

public class SmtpConfigDto
{
    public string SenderAddress { get; set; } = string.Empty;
    public string SenderDisplayName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 0;
    public bool EnableSsl { get; set; } = true;
    /// <summary>
    /// Limit in bytes for allowing files to be added as attachments. Defaults to 25MB
    /// </summary>
    public int SizeLimit { get; set; } = 26_214_400;
    /// <summary>
    /// Should Kavita use config/templates for Email templates or the default ones
    /// </summary>
    public bool CustomizedTemplates { get; set; } = false;
}
