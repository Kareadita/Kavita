using System.Collections.Generic;

namespace API.Entities
{
    public class Series
    {
        /// <summary>
        /// The UI visible Name of the Series. This may or may not be the same as the OriginalName
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Original Japanese Name
        /// </summary>
        public string OriginalName { get; set; }
        /// <summary>
        /// The name used to sort the Series. By default, will be the same as Name.
        /// </summary>
        public string SortName { get; set; }
        /// <summary>
        /// Summary information related to the Series
        /// </summary>
        public string Summary { get; set; }
        
        public ICollection<Volume> Volumes { get; set; }
        
        
    }
}