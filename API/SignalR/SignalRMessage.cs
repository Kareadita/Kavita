namespace API.SignalR
{
    public class SignalRMessage
    {
        public object Body { get; set; }
        public string Name { get; set; }

        //[JsonIgnore]
        //public ModelAction Action { get; set; } // This will be for when we add new flows
    }
}
