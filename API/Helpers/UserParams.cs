namespace API.Helpers;
#nullable enable

/// <summary>
/// User params should be used together with [FromQuery] to add optional pagination to endpoint. If no pagination params are
/// provided, the default (int.MaxValue) will be used. When adding pagination to an endpoint, ensure the UI sets the correct
/// query params.
/// </summary>
/// <remarks>[FromQuery] always assigns the object, it'll never return null</remarks>
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
        PageNumber = 1,
    };
}
