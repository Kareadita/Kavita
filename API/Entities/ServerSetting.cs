using System.ComponentModel.DataAnnotations;
using API.Entities.Enums;
using API.Entities.Interfaces;

namespace API.Entities
{
    public class ServerSetting : IHasConcurrencyToken
    {
        [Key]
        public ServerSettingKey Key { get; set; }
        public string Value { get; set; }

        /// <inheritdoc />
        [ConcurrencyCheck]
        public uint RowVersion { get; private set; }

        /// <inheritdoc />
        public void OnSavingChanges()
        {
            RowVersion++;
        }
    }
}
