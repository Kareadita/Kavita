namespace Kavita.API.Services.Helpers;

public interface ICacheHelper
{
    bool ShouldUpdateCoverImage(string coverPath, bool sourceChanged, bool forceUpdate = false, bool isCoverLocked = false);

    bool CoverImageExists(string path);
}
