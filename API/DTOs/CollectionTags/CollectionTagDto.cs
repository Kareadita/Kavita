namespace API.DTOs.CollectionTags
{
    public class CollectionTagDto
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        public bool Promoted { get; set; }
        /// <summary>
        /// The cover image string. This is used on Frontend to show or hide the Cover Image
        /// </summary>
        public string CoverImage { get; set; }
        public bool CoverImageLocked { get; set; }
    }
}
