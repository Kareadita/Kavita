using System;
using System.Collections.Generic;
using API.Entities.Interfaces;

namespace API.Entities
{
    /// <summary>
    /// This is a collection of <see cref="ReadingListItem"/> which represent individual chapters and an order.
    /// </summary>
    public class ReadingList : IEntityDate
    {
        public int Id { get; init; }
        public string Title { get; set; }
        public string Summary { get; set; }
        /// <summary>
        /// Reading lists that are promoted are only done by admins
        /// </summary>
        public bool Promoted { get; set; }

        public ICollection<ReadingListItem> Items { get; set; }
        public DateTime Created { get; set; }
        public DateTime LastModified { get; set; }

        // Relationships
        public int AppUserId { get; set; }
        public AppUser AppUser { get; set; }

    }
}
