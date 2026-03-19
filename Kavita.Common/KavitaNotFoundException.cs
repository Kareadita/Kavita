using System;

namespace Kavita.Common;

/// <summary>
/// Exception that is catched by the exception middleware, and return NotFound
/// </summary>
public class KavitaNotFoundException: Exception
{

    public KavitaNotFoundException()
    {
    }

    public KavitaNotFoundException(string message) : base(message)
    {
    }

    public KavitaNotFoundException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
