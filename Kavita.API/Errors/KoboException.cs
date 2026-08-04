using System;

namespace Kavita.API.Errors;

/// <summary>
/// Should be caught in <see cref="KoboController"/> and ONLY used in <see cref="KoboService"/>
/// </summary>
public class KoboException : Exception
{
    public KoboException()
    { }

    public KoboException(string message) : base(message)
    { }

    public KoboException(string message, Exception inner)
        : base(message, inner) { }
}
