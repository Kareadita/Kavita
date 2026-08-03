using System;
using System.IO;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Kavita.API.Services;
using Kavita.Services.Kobo;
using Kavita.Services.Scanner;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Kavita.Services.Tests.Kobo;

public class KoboArchiveEpubConverterTests
{
    private readonly string _cbzPath = Path.GetFullPath(Path.Join(Directory.GetCurrentDirectory(),
        "../../../Test Data/ArchiveService/CoverImages/v10.cbz"));

    [Fact]
    public async Task ConvertAsync_MatchesFrozenStructuralContract()
    {
        var tempRoot = Path.Join(Path.GetTempPath(), "kavita-kobo-convert-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var outputPath = Path.Combine(tempRoot, "out.epub");
        try
        {
            var directoryService = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new FileSystem());
            directoryService.ExistOrCreate(directoryService.TempDirectory);
            var archiveService = new ArchiveService(Substitute.For<ILogger<ArchiveService>>(), directoryService,
                Substitute.For<IImageService>(), Substitute.For<IMediaErrorService>());
            var converter = new KoboArchiveEpubConverter(Substitute.For<ILogger<KoboArchiveEpubConverter>>(),
                archiveService, directoryService);

            await converter.ConvertAsync(_cbzPath, outputPath, "Contract Comic");

            Assert.True(File.Exists(outputPath));
            using var zip = ZipFile.OpenRead(outputPath);
            Assert.Equal("mimetype", zip.Entries[0].FullName);

            var opfEntry = zip.GetEntry("OEBPS/content.opf");
            Assert.NotNull(opfEntry);
            await using var opfStream = opfEntry.Open();
            var opf = XDocument.Load(opfStream);
            XNamespace opfNs = "http://www.idpf.org/2007/opf";

            var spineIdrefs = opf.Root!.Element(opfNs + "spine")!.Elements(opfNs + "itemref")
                .Select(e => (string?)e.Attribute("idref"))
                .ToList();
            Assert.Equal(["page_0001", "page_0002", "page_0003"], spineIdrefs);
            Assert.DoesNotContain("nav", spineIdrefs);

            var manifest = opf.Root.Element(opfNs + "manifest")!;
            Assert.Contains(manifest.Elements(opfNs + "item"),
                i => (string?)i.Attribute("id") == "nav" && (string?)i.Attribute("properties") == "nav");
            Assert.DoesNotContain(opf.Descendants(),
                e => (string?)e.Attribute("name") == "cover" || (string?)e.Attribute("property") == "cover");

            for (var i = 1; i <= 3; i++)
            {
                var pageName = $"page_{i:D4}";
                Assert.NotNull(zip.GetEntry($"OEBPS/Text/{pageName}.xhtml"));
                Assert.Contains(manifest.Elements(opfNs + "item"),
                    item => (string?)item.Attribute("id") == pageName);
                Assert.Contains(manifest.Elements(opfNs + "item"),
                    item => (string?)item.Attribute("id") == $"img_{i:D4}");

                var pageEntry = zip.GetEntry($"OEBPS/Text/{pageName}.xhtml")!;
                await using var pageStream = pageEntry.Open();
                using var reader = new StreamReader(pageStream);
                var html = await reader.ReadToEndAsync();
                Assert.Contains("<img ", html);
                Assert.DoesNotContain("koboSpan", html, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("kobo.1.1", html);
                Assert.Equal(1, CountOccurrences(html, "<img "));
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        for (var i = 0; (i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0; i += needle.Length)
        {
            count++;
        }

        return count;
    }
}
