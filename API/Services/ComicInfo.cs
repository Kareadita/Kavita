namespace API.Services
{
    public class ComicInfo
    {
        public string Summary { get; set; }
        public string Title { get; set; }
        public string Series { get; set; }
        public string Notes { get; set; }
        public string Publisher { get; set; }
        public string Genre { get; set; }
        public int PageCount { get; set; }
        // ReSharper disable once InconsistentNaming
        public string LanguageISO { get; set; }
        public string Web { get; set; }
    }
}