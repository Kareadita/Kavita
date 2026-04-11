namespace Kavita.Models.Entities.Interfaces;

public interface ITag
{
    int Id { get; set; }
    string Title { get; set; }
    string NormalizedTitle { get; set; }
}
