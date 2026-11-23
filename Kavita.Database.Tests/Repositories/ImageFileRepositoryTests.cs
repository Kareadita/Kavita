using System.Linq;
using System.Threading.Tasks;
using Kavita.Models.Builders;
using Kavita.Models.Entities;
using Kavita.Models.Images;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace Kavita.Database.Tests.Repositories;

public class ImageFileRepositoryTests(ITestOutputHelper outputHelper) : AbstractDbTest(outputHelper)
{
    [Fact]
    public async Task GetAllHoldersAsync_FindsHolders_WhenIdsExceedSqliteParameterLimit()
    {
        var (unitOfWork, context, _) = await CreateDatabase();

        var personCover = new ImageFile { Kind = ImageKind.PersonCover, FileName = "person.webp" };
        var libraryIcon = new ImageFile { Kind = ImageKind.LibraryIcon, FileName = "library.webp" };
        context.ImageFile.AddRange(personCover, libraryIcon);
        await context.SaveChangesAsync();

        var person = new PersonBuilder("Holder").Build();
        person.CoverImageFileId = personCover.Id;
        context.Person.Add(person);
        var library = await context.Library.FirstAsync();
        library.CoverImageFileId = libraryIcon.Id;
        await context.SaveChangesAsync();

        var ids = Enumerable.Range(10_000, 40_000).Append(personCover.Id).Append(libraryIcon.Id).ToList();

        var holders = await unitOfWork.ImageFileRepository.GetAllHoldersAsync(ids);

        Assert.Equal(2, holders.Count);
        Assert.Contains(new ImageFileHolder(personCover.Id, "Person", person.Id), holders);
        Assert.Contains(new ImageFileHolder(libraryIcon.Id, "Library", library.Id), holders);
    }
}
