using System;
using System.Collections.Generic;

namespace API.Entities
{
    /// <summary>
    /// Has a 1-to-1 relationship with a Chapter. Represents metadata about a chapter.
    /// </summary>
    public class ChapterMetadata
    {
        public int Id { get; set; }

        /// <summary>
        /// Chapter title
        /// </summary>
        /// <remarks>This should not be confused with Chapter.Title which is used for special filenames.</remarks>
        public string Title { get; set; } = string.Empty;
        public string Year { get; set; }
        public string StoryArc { get; set; } // This might be a list

        //public ICollection<Genre> Genres { get; set; } = new List<Genre>();
        /// <summary>
        /// All people attached at a Chapter level. Usually Comics will have different people per issue.
        /// </summary>
        public ICollection<Person> People { get; set; } = new List<Person>();


        // ChapterTitle, Year, StoryArc, Writer (all comic stuff that is per issue)




        // Relationships
        public Chapter Chapter { get; set; }
        public int ChapterId { get; set; }

    }
}
