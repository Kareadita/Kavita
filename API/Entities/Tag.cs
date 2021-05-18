using System.ComponentModel.DataAnnotations;
using API.Entities.Interfaces;

namespace API.Entities
{
    /// <summary>
    /// Represents a user entered field that is used as a tagging and grouping mechanism
    /// </summary>
    public class Tag : IHasConcurrencyToken
    {
        public int Id { get; set; }
        public string Title { get; set; }
        /// <summary>
        /// A normalized string used to check if the tag already exists in the DB
        /// </summary>
        public string NormalizedTitle { get; set; }

        [ConcurrencyCheck]
        public uint RowVersion { get; set; }

        public void OnSavingChanges()
        {
            RowVersion++;
        }
    }
}