using System.Collections.Generic;

namespace API.Parser
{
    public class ParserInfo
    {
        // This can be multiple
        public string Chapters { get; set; }
        public string Series { get; set; }
        // This can be multiple
        public string Volume { get; set; }
        public IEnumerable<string> Files { get; init; }
    }
}