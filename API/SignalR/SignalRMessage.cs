namespace API.SignalR
{
    /// <summary>
    /// Payload for SignalR messages to Frontend
    /// </summary>
    public class SignalRMessage
    {
        public object Body { get; set; }
        public string Name { get; set; }

        //[JsonIgnore]
        //public ModelAction Action { get; set; } // This will be for when we add new flows
    }
}
