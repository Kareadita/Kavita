namespace API.Entities
{
    public class FTSSeries
    {
        public int RowId { get; set; }
        public Series Series { get; set; }
        
        public string Name { get; set; }
        public string OriginalName { get; set; }
        
        public string Match { get; set; }
        public double? Rank { get; set; }
    }
}