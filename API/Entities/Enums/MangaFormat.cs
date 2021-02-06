using System.ComponentModel;

namespace API.Entities
{
    public enum MangaFormat
    {
        [Description("Image")]
        Image = 0,
        [Description("Archive")]
        Archive = 1,
        [Description("Unknown")]
        Unknown = 2
    }
}