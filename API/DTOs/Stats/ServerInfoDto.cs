namespace API.DTOs.Stats
{
    public class ServerInfoDto
    {
        public string InstallId { get; set; }
        public string Os { get; set; }
        public bool IsDocker { get; set; }
        public string DotnetVersion { get; set; }
        public string KavitaVersion { get; set; }
        public int NumOfCores { get; set; }
    }
}
