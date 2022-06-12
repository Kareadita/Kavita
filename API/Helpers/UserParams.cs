namespace API.Helpers
{
    public class UserParams
    {
        private const int MaxPageSize = int.MaxValue; // TODO: Validate if we need this
        public int PageNumber { get; set; } = 1;
        private int _pageSize = 30;

        /// <summary>
        /// If set to 0, will set as MaxInt
        /// </summary>
        public int PageSize
        {
            get => _pageSize;
            init => _pageSize = (value == 0) ? MaxPageSize : value;
        }
    }
}
