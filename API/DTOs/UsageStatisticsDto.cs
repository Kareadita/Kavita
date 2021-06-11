using System;
using System.Collections.Generic;
using API.Entities.Enums;

namespace API.DTOs
{
    public class UsageStatisticsDto
    {
        public UsageStatisticsDto()
        {
            UpdateTime();
            ClientsInfo = new List<ClientInfo>();
        }
        
        public Guid Id { get; set; }
        public DateTime LastUpdate { get; set; }

        public int UsersCount { get; set; }
        public ServerInfo ServerInfo { get; set; }
        public List<ClientInfo> ClientsInfo { get; set; }
        public IEnumerable<string> FileTypes { get; set; }
        public IEnumerable<LibInfo> LibraryTypesCreated { get; set; }

        public void UpdateTime()
        {
            LastUpdate = DateTime.UtcNow;
        }

        public void AddClientInfo(ClientInfo clientInfo)
        {
            ClientsInfo.Add(clientInfo);
        }
    }

    public class LibInfo
    {
        public LibraryType Type { get; set; }
        public int Count { get; set; }
    }

    public class ClientInfo
    {
        public string Os { get; set; }
        public string Browser { get; set; }
        public string Device { get; set; }
        public DeviceType DeviceType { get; set; }
        public string ScreenSize { get; set; }
        public string ScreenResolution { get; set; }
        public string KavitaUiVersion { get; set; }
        public string BuildBranch { get; set; }

        public DateTime? CollectedAt { get; set; }
    }

    public class ServerInfo
    {
        public string Os { get; set; }
        public string DotNetVersion { get; set; }
        public string RunTimeVersion { get; set; }
        public string KavitaVersion { get; set; }
        public string BuildBranch { get; set; }
        public string Locale { get; set; }
    }

    public enum DeviceType
    {
        Desktop = 0,
        Laptop = 1,
        Tablet = 2,
        Mobile = 3
    }
}