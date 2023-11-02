namespace API.Helpers;
#nullable enable

public static class NumberHelper
{
    public static bool IsValidMonth(int number) => number is > 0 and <= 12;
    public static bool IsValidYear(int number) => number is >= 1000;
}
