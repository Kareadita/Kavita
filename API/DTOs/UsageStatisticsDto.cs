using System;
using System.Collections.Generic;
using System.Linq;

namespace API.DTOs
{
    public class UsageStatisticsDto
    {
        public UsageStatisticsDto()
        {
            MarkAsUpdatedNow();
            ClientsInfo = new List<ClientInfoDto>();
        }

        public Guid Id { get; set; }
        public DateTime LastUpdate { get; set; }
        public ServerInfoDto ServerInfoDto { get; set; }
        public List<ClientInfoDto> ClientsInfo { get; set; }

        public UsageInfoDto UsageInfoDto { get; set; }

        public void MarkAsUpdatedNow()
        {
            LastUpdate = DateTime.UtcNow;
        }

        public void AddClientInfo(ClientInfoDto clientInfoDto)
        {
            if (ClientsInfo.Any(x => x.IsTheSameDevice(clientInfoDto))) return;

            ClientsInfo.Add(clientInfoDto);
        }
    }
}