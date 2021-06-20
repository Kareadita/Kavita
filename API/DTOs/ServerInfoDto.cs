namespace API.DTOs
{
    public class ServerInfoDto
    {
        public string Os { get; set; }
        public string DotNetVersion { get; set; }
        public string RunTimeVersion { get; set; }
        public string KavitaVersion { get; set; }
        public string BuildBranch { get; set; }
        public string Culture { get; set; }
    }
}