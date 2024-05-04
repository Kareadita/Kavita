using System;

namespace Kavita.Common;

/// <summary>
/// When a rate limit is hit
/// </summary>
public class RateLimitException : Exception
{
    public RateLimitException()
    { }

    public RateLimitException(string message) : base(message)
    { }

    public RateLimitException(string message, Exception inner)
        : base(message, inner) { }
}
