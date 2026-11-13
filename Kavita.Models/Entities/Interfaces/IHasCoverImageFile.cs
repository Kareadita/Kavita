namespace Kavita.Models.Entities.Interfaces;
#nullable enable

public interface IHasCoverImageFile
{
    public int Id { get; set; }
    public int? CoverImageFileId { get; set; }
    public ImageFile? CoverImageFile { get; set; }
}
