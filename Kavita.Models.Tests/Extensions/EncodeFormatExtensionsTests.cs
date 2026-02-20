using Kavita.Models.Entities.Enums;
using Kavita.Models.Extensions;

namespace Kavita.Models.Tests.Extensions;

public class EncodeFormatExtensionsTests
{
    [Fact]
    public void GetExtension_ShouldReturnCorrectExtensionForAllValues()
    {

        var expectedExtensions = new Dictionary<EncodeFormat, string>
        {
            { EncodeFormat.PNG, ".png" },
            { EncodeFormat.WEBP, ".webp" },
            { EncodeFormat.AVIF, ".avif" }
        };

        // Act & Assert
        foreach (var format in Enum.GetValues<EncodeFormat>())
        {
            var extension = format.GetExtension();
            Assert.Equal(expectedExtensions[format], extension);
        }
    }

}
