using System;
using System.Collections.Generic;
using System.Linq;
using API.Entities.Enums;
using API.Extensions;
using Xunit;

namespace API.Tests.Extensions;

public class EncodeFormatExtensionsTests
{
    [Fact]
    public void GetExtension_ShouldReturnCorrectExtensionForAllValues()
    {
        // Arrange
        var expectedExtensions = new Dictionary<EncodeFormat, string>
        {
            { EncodeFormat.PNG, ".png" },
            { EncodeFormat.WEBP, ".webp" },
            { EncodeFormat.AVIF, ".avif" }
        };

        // Act & Assert
        foreach (var format in Enum.GetValues(typeof(EncodeFormat)).Cast<EncodeFormat>())
        {
            var extension = format.GetExtension();
            Assert.Equal(expectedExtensions[format], extension);
        }
    }

}
