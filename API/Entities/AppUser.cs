﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using API.Entities.Interfaces;
using Microsoft.AspNetCore.Identity;


namespace API.Entities
{
    public class AppUser : IdentityUser<int>, IHasConcurrencyToken
    {
        public DateTime Created { get; set; } = DateTime.Now;
        public DateTime LastActive { get; set; }
        public ICollection<Library> Libraries { get; set; }
        public ICollection<AppUserRole> UserRoles { get; set; }
        public ICollection<AppUserProgress> Progresses { get; set; }
        public ICollection<AppUserRating> Ratings { get; set; }
        public AppUserPreferences UserPreferences { get; set; }
        public ICollection<AppUserBookmark> Bookmarks { get; set; }
        /// <summary>
        /// An API Key to interact with external services, like OPDS
        /// </summary>
        public string ApiKey { get; set; }


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
