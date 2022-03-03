namespace API.DTOs
{
    public class UpdateSeriesDto
    {
        public int Id { get; init; }
        public string Name { get; init; }
        public string LocalizedName { get; init; }
        public string SortName { get; init; }
        public bool CoverImageLocked { get; set; }

        public bool UnlockName { get; set; }
        public bool UnlockSortName { get; set; }
        public bool UnlockSummary { get; set; }
    }
}
