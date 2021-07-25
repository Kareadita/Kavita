using System;

namespace Kavita.Common
{
    /// <summary>
    /// These are used for errors to send to the UI that should not be reported to Sentry
    /// </summary>
    [Serializable]
    public class KavitaException : Exception
    {
        public KavitaException()
        {

        }

        public KavitaException(string message) : base(message)
        {

        }
    }
}
