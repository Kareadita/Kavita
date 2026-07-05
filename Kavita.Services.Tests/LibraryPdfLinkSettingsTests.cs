using System.Linq;
using System.Threading.Tasks;
using Kavita.API.Repositories;
using Kavita.Database.Tests;
using Kavita.Models.Builders;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Services.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Kavita.Services.Tests;

public class LibraryPdfLinkSettingsTests(ITestOutputHelper testOutputHelper) : AbstractDbTest(testOutputHelper)
{
    [Fact]
    public async Task UpdateLibrary_WithPdfLinkTogglesOff_ReturnsCorrectValuesOnGet()
    {
        var (unitOfWork, _, _) = await CreateDatabase();

        var library = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(1,
            LibraryIncludes.Folders | LibraryIncludes.FileTypes | LibraryIncludes.ExcludePatterns);
        Assert.NotNull(library);

        var updateDto = LibrarySettingsTestHelper.ToUpdateDto(library, enablePdfExternalLinks: false, enablePdfInternalLinks: false);
        library.Name = updateDto.Name.Trim();
        library.Folders = updateDto.Folders.Select(path => new FolderPath { Path = path }).Distinct().ToList();
        LibrarySettingsTestHelper.ApplyUpdateLibrarySettings(updateDto, library);
        unitOfWork.LibraryRepository.Update(library);
        await unitOfWork.CommitAsync();

        var libraryDto = await unitOfWork.LibraryRepository.GetLibraryDtoByIdAsync(library.Id);
        var liteLibraryDto = await unitOfWork.LibraryRepository.GetLiteLibraryDtoByIdAsync(library.Id);

        Assert.NotNull(libraryDto);
        Assert.NotNull(liteLibraryDto);
        Assert.False(libraryDto.EnablePdfExternalLinks);
        Assert.False(libraryDto.EnablePdfInternalLinks);
        Assert.False(liteLibraryDto.EnablePdfExternalLinks);
        Assert.False(liteLibraryDto.EnablePdfInternalLinks);
    }

    [Fact]
    public async Task CopySettingsFromLibrary_PropagatesPdfLinkTogglesToTargetLibraries()
    {
        var (unitOfWork, _, _) = await CreateDatabase();

        var sourceLibrary = new LibraryBuilder("Source Library", LibraryType.Book)
            .WithFolderPath(new FolderPathBuilder("/data/source").Build())
            .Build();
        sourceLibrary.EnablePdfExternalLinks = false;
        sourceLibrary.EnablePdfInternalLinks = false;

        var targetLibrary = new LibraryBuilder("Target Library", LibraryType.Manga)
            .WithFolderPath(new FolderPathBuilder("/data/target").Build())
            .Build();

        unitOfWork.LibraryRepository.Add(sourceLibrary);
        unitOfWork.LibraryRepository.Add(targetLibrary);
        await unitOfWork.CommitAsync();

        sourceLibrary = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(sourceLibrary.Id,
            LibraryIncludes.Folders | LibraryIncludes.FileTypes | LibraryIncludes.ExcludePatterns);
        targetLibrary = await unitOfWork.LibraryRepository.GetLibraryForIdAsync(targetLibrary.Id,
            LibraryIncludes.Folders | LibraryIncludes.FileTypes | LibraryIncludes.ExcludePatterns);
        Assert.NotNull(sourceLibrary);
        Assert.NotNull(targetLibrary);

        Assert.True(targetLibrary.EnablePdfExternalLinks);
        Assert.True(targetLibrary.EnablePdfInternalLinks);

        LibrarySettingsTestHelper.CopySettingsFromLibrary(sourceLibrary, targetLibrary);
        unitOfWork.LibraryRepository.Update(targetLibrary);
        await unitOfWork.CommitAsync();

        var targetDto = await unitOfWork.LibraryRepository.GetLibraryDtoByIdAsync(targetLibrary.Id);
        Assert.NotNull(targetDto);
        Assert.False(targetDto.EnablePdfExternalLinks);
        Assert.False(targetDto.EnablePdfInternalLinks);
    }
}
