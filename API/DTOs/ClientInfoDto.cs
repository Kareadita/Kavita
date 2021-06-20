using System;

namespace API.DTOs
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

        public bool IsTheSameDevice(ClientInfoDto clientInfoDto)
        {
            return (clientInfoDto.ScreenResolution ?? "").Equals(ScreenResolution) &&
                   (clientInfoDto.PlatformType ?? "").Equals(PlatformType) &&
                   (clientInfoDto.Browser?.Name ?? "").Equals(Browser?.Name) &&
                   (clientInfoDto.Os?.Name ?? "").Equals(Os?.Name) &&
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