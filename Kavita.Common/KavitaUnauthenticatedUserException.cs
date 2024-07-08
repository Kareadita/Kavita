using System;
using System.Runtime.Serialization;

namespace Kavita.Common;

/// <summary>
/// The user does not exist (aka unauthorized). This will be caught by middleware and Unauthorized() returned to UI
/// </summary>
/// <remarks>This will always log to Security Log</remarks>
public class KavitaUnauthenticatedUserException : Exception
{
    public KavitaUnauthenticatedUserException()
    { }

    public KavitaUnauthenticatedUserException(string message) : base(message)
    { }

    public KavitaUnauthenticatedUserException(string message, Exception inner)
        : base(message, inner) { }
}
