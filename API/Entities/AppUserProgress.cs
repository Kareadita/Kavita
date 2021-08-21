﻿
using System;
using System.ComponentModel.DataAnnotations;
using API.Entities.Interfaces;

namespace API.Entities
{
    /// <summary>
    /// Represents the progress a single user has on a given Chapter.
    /// </summary>
    //[Index(nameof(SeriesId), nameof(VolumeId), nameof(ChapterId), nameof(AppUserId), IsUnique = true)]
    public class AppUserProgress : IEntityDate, IHasConcurrencyToken
    {
        /// <summary>
        /// Id of Entity
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Pages Read for given Chapter
        /// </summary>
        public int PagesRead { get; set; }
        /// <summary>
        /// Volume belonging to Chapter
        /// </summary>
        public int VolumeId { get; set; }
        /// <summary>
        /// Series belonging to Chapter
        /// </summary>
        public int SeriesId { get; set; }
        /// <summary>
        /// Chapter
        /// </summary>
        public int ChapterId { get; set; }
        /// <summary>
        /// For Book Reader, represents the nearest passed anchor on the screen that can be used to resume scroll point
        /// on next load
        /// </summary>
        public string BookScrollId { get; set; }

        // Relationships
        /// <summary>
        /// Navigational Property for EF. Links to a unique AppUser
        /// </summary>
        public AppUser AppUser { get; set; }
        /// <summary>
        /// User this progress belongs to
        /// </summary>
        public int AppUserId { get; set; }

        /// <summary>
        /// When this was first created
        /// </summary>
        public DateTime Created { get; set; }
        /// <summary>
        /// Last date this was updated
        /// </summary>
        public DateTime LastModified { get; set; }

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
