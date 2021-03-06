﻿using System;
using System.Collections.Generic;
using API.Entities.Interfaces;

namespace API.Entities
{
    public class Chapter : IEntityDate
    {
        public int Id { get; set; }
        /// <summary>
        /// Range of numbers. Chapter 2-4 -> "2-4". Chapter 2 -> "2".
        /// </summary>
        public string Range { get; set; }
        /// <summary>
        /// Smallest number of the Range. Can be a partial like Chapter 4.5
        /// </summary>
        public string Number { get; set; }
        /// <summary>
        /// The files that represent this Chapter
        /// </summary>
        public ICollection<MangaFile> Files { get; set; }
        public DateTime Created { get; set; }
        public DateTime LastModified { get; set; }
        public byte[] CoverImage { get; set; }
        /// <summary>
        /// Total number of pages in all MangaFiles
        /// </summary>
        public int Pages { get; set; }

        // Relationships
        public Volume Volume { get; set; }
        public int VolumeId { get; set; }

    }
}