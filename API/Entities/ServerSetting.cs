using System.ComponentModel.DataAnnotations;
using API.Entities.Interfaces;

namespace API.Entities
{
    public class ServerSetting : IHasConcurrencyToken
    {
        [Key]
        public ServerSettingKey Key { get; set; }
        public string Value { get; set; }

        [ConcurrencyCheck]
        public uint RowVersion { get; set; }
        public void OnSavingChanges()
        {
            RowVersion++;
        }
    }
}