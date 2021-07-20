using System;

namespace API.DTOs.Stats
{
    public class ClientInfoDto
    {
        public ClientInfoDto()
        {
            CollectedAt = DateTime.UtcNow;
        }

        public string KavitaUiVersion { get; set; }
        public string ScreenResolution { get; set; }
        public string PlatformType { get; set; }
        public DetailsVersion Browser { get; set; }
        public DetailsVersion Os { get; set; }

        public DateTime? CollectedAt { get; set; }
        public bool UsingDarkTheme { get; set; }

        public bool IsTheSameDevice(ClientInfoDto clientInfoDto)
        {
            return (clientInfoDto.ScreenResolution ?? string.Empty).Equals(ScreenResolution) &&
                   (clientInfoDto.PlatformType ?? string.Empty).Equals(PlatformType) &&
                   (clientInfoDto.Browser?.Name ?? string.Empty).Equals(Browser?.Name) &&
                   (clientInfoDto.Os?.Name ?? string.Empty).Equals(Os?.Name) &&
                   clientInfoDto.CollectedAt.GetValueOrDefault().ToString("yyyy-MM-dd")
                       .Equals(CollectedAt.GetValueOrDefault().ToString("yyyy-MM-dd"));
        }
    }

    public class DetailsVersion
    {
        public string Name { get; set; }
        public string Version { get; set; }
    }
}
