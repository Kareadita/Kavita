using System;
using System.Runtime.Serialization;

namespace Kavita.Common;

/// <summary>
/// These are used for errors to send to the UI that should not be reported to Sentry
/// </summary>
public class KavitaException : Exception
{
    public KavitaException()
    { }

    public KavitaException(string message) : base(message)
    { }

    public KavitaException(string message, Exception inner)
        : base(message, inner) { }
}
