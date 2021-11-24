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
        public string Year { get; set; } // Only time I can think this will be more than 1 year is for a volume which will be a spread
        public string StoryArc { get; set; } // This might be a list

        /// <summary>
        /// All people attached at a Chapter level. Usually Comics will have different people per issue.
        /// </summary>
        public ICollection<Person> People { get; set; } = new List<Person>();





        // Relationships
        public Chapter Chapter { get; set; }
        public int ChapterId { get; set; }

    }
}
