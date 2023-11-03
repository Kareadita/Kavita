namespace API.Helpers;
#nullable enable

public class UserParams
{
    private const int MaxPageSize = int.MaxValue;
    public int PageNumber { get; init; } = 1;
    private readonly int _pageSize = MaxPageSize;

    /// <summary>
    /// If set to 0, will set as MaxInt
    /// </summary>
    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = (value == 0) ? MaxPageSize : value;
    }

    public static readonly UserParams Default = new()
    {
        PageSize = 20,
        PageNumber = 1
    };
}
