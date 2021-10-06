namespace API.DTOs.Uploads
{
    public class UploadFileDto
    {
        /// <summary>
        /// Id of the Entity
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Url of the file to download from (can be null)
        /// </summary>
        public string Url { get; set; }
    }
}
