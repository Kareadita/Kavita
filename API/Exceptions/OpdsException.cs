using System;
using API.Controllers;
using API.Services;

namespace API.Exceptions;

/// <summary>
/// Should be caught in <see cref="OpdsController"/> and ONLY used in <see cref="OpdsService"/>
/// </summary>
public class OpdsException : Exception
{
    public OpdsException()
    { }

    public OpdsException(string message) : base(message)
    { }

    public OpdsException(string message, Exception inner)
        : base(message, inner) { }
}
