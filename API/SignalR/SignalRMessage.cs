using System;

namespace API.SignalR
{
    /// <summary>
    /// Payload for SignalR messages to Frontend
    /// </summary>
    public class SignalRMessage
    {
        public object Body { get; set; }
        public string Name { get; set; }
        /// <summary>
        /// User friendly Title of the Event
        /// </summary>
        /// <example>Scanning Manga</example>
        public string Title { get; set; } = string.Empty;
        /// <summary>
        /// User friendly subtitle. Should have extra info
        /// </summary>
        /// <example>C:/manga/Accel World V01.cbz</example>
        public string SubTitle { get; set; } = string.Empty;
        /// <summary>
        /// Represents what this represents. started | updated | ended | single
        /// </summary>
        public string EventType { get; set; } = "updated";
        /// <summary>
        /// When event took place
        /// </summary>
        public DateTime EventTime = DateTime.Now;
    }
}
