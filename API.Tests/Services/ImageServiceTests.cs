using System.Drawing;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using API.Entities.Enums;
using API.Services;
using API.Services.ImageServices.ImageMagick;
using EasyCaching.Core;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Services;

public class ImageServiceTests
{
    private readonly string _testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ImageService/Covers");
    private readonly string _testDirectoryColorScapes = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ImageService/ColorScapes");
    private const string OutputPattern = "_output";
    private const string BaselinePattern = "_baseline";

    /// <summary>
    /// Run this once to get the baseline generation
    /// </summary>
    [Fact]
    public void GenerateBaseline()
    {
        GenerateFiles(BaselinePattern);
    }

    /// <summary>
    /// Change the Scaling/Crop code then run this continuously
    /// </summary>
    [Fact]
    public void TestScaling()
    {
        GenerateFiles(OutputPattern);
        GenerateHtmlFile();
    }

    private void GenerateFiles(string outputExtension)
    {
        // Step 1: Delete any images that have _output in the name
        var outputFiles = Directory.GetFiles(_testDirectory, "*_output.*");
        foreach (var file in outputFiles)
        {
            File.Delete(file);
        }

        // Step 2: Scan the _testDirectory for images
        var imageFiles = Directory.GetFiles(_testDirectory, "*.*")
            .Where(file => !file.EndsWith("html"))
            .Where(file => !file.Contains(OutputPattern) && !file.Contains(BaselinePattern))
            .ToList();

        // Step 3: Process each image
        ImageMagickImageFactory factory = new ImageMagickImageFactory();
        foreach (var imagePath in imageFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(imagePath);
            var dims = CoverImageSize.Default.GetDimensions();
            var thumbnail = factory.Create(imagePath);
            thumbnail = ImageService.Thumbnail(thumbnail, dims.Width, dims.Height);
            var outputFileName = fileName + outputExtension + ".png";
            thumbnail.Save(Path.Join(_testDirectory, outputFileName), EncodeFormat.PNG,100);
        }
    }

    private void GenerateHtmlFile()
    {
        var imageFiles = Directory.GetFiles(_testDirectory, "*.*")
            .Where(file => !file.EndsWith("html"))
            .Where(file => !file.Contains(OutputPattern) && !file.Contains(BaselinePattern))
            .ToList();
        ImageMagickImageFactory factory = new ImageMagickImageFactory();

        var htmlBuilder = new StringBuilder();
        htmlBuilder.AppendLine("<!DOCTYPE html>");
        htmlBuilder.AppendLine("<html lang=\"en\">");
        htmlBuilder.AppendLine("<head>");
        htmlBuilder.AppendLine("<meta charset=\"UTF-8\">");
        htmlBuilder.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        htmlBuilder.AppendLine("<title>Image Comparison</title>");
        htmlBuilder.AppendLine("<style>");
        htmlBuilder.AppendLine("body { font-family: Arial, sans-serif; }");
        htmlBuilder.AppendLine(".container { display: flex; flex-wrap: wrap; }");
        htmlBuilder.AppendLine(".image-row { display: flex; align-items: center; margin-bottom: 20px; width: 100% }");
        htmlBuilder.AppendLine(".image-row img { margin-right: 10px; max-width: 200px; height: auto; }");
        htmlBuilder.AppendLine("</style>");
        htmlBuilder.AppendLine("</head>");
        htmlBuilder.AppendLine("<body>");
        htmlBuilder.AppendLine("<div class=\"container\">");

        foreach (var imagePath in imageFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(imagePath);
            var baselinePath = Path.Combine(_testDirectory, fileName + "_baseline.png");
            var outputPath = Path.Combine(_testDirectory, fileName + "_output.png");
            var dims = CoverImageSize.Default.GetDimensions();

            using var sourceImage = factory.Create(imagePath);
            htmlBuilder.AppendLine("<div class=\"image-row\">");
            htmlBuilder.AppendLine($"<p>{fileName} ({((double) sourceImage.Width / sourceImage.Height).ToString("F2")}) - {ImageService.WillScaleWell(sourceImage, dims.Width, dims.Height)}</p>");
            htmlBuilder.AppendLine($"<img src=\"./{Path.GetFileName(imagePath)}\" alt=\"{fileName}\">");
            if (File.Exists(baselinePath))
            {
                htmlBuilder.AppendLine($"<img src=\"./{Path.GetFileName(baselinePath)}\" alt=\"{fileName} baseline\">");
            }
            if (File.Exists(outputPath))
            {
                htmlBuilder.AppendLine($"<img src=\"./{Path.GetFileName(outputPath)}\" alt=\"{fileName} output\">");
            }
            htmlBuilder.AppendLine("</div>");
        }

        htmlBuilder.AppendLine("</div>");
        htmlBuilder.AppendLine("</body>");
        htmlBuilder.AppendLine("</html>");

        File.WriteAllText(Path.Combine(_testDirectory, "index.html"), htmlBuilder.ToString());
    }


    [Fact]
    public void TestColorScapes()
    {
        // Step 1: Delete any images that have _output in the name
        var outputFiles = Directory.GetFiles(_testDirectoryColorScapes, "*_output.*");
        foreach (var file in outputFiles)
        {
            File.Delete(file);
        }

        // Step 2: Scan the _testDirectory for images
        var imageFiles = Directory.GetFiles(_testDirectoryColorScapes, "*.*")
            .Where(file => !file.EndsWith("html"))
            .Where(file => !file.Contains(OutputPattern) && !file.Contains(BaselinePattern))
            .ToList();

        var factory = LoggerFactory.Create(builder => { builder.AddConsole(); });
        var logger = factory.CreateLogger<ImageService>();

        ImageService service = new ImageService(logger, null, null, new ImageMagickImageFactory());

        // Step 3: Process each image
        foreach (var imagePath in imageFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(imagePath);
            var colors = service.CalculateColorScape(imagePath);

            // Generate primary color image
            GenerateColorImage(colors.Primary, Path.Combine(_testDirectoryColorScapes, $"{fileName}_primary_output.png"));

            // Generate secondary color image
            GenerateColorImage(colors.Secondary, Path.Combine(_testDirectoryColorScapes, $"{fileName}_secondary_output.png"));
        }

        // Step 4: Generate HTML file
        GenerateHtmlFileForColorScape();

    }

    private static void GenerateColorImage(string hexColor, string outputPath)
    {
        ImageMagickImageFactory factory = new ImageMagickImageFactory();
        var color = ImageService.HexToRgb(hexColor);
        using var colorImage = factory.Create(200,100,color.R, color.G, color.B);
        colorImage.Save(outputPath, EncodeFormat.PNG, 100);
    }

    private void GenerateHtmlFileForColorScape()
    {
        var imageFiles = Directory.GetFiles(_testDirectoryColorScapes, "*.*")
            .Where(file => !file.EndsWith("html"))
            .Where(file => !file.Contains(OutputPattern) && !file.Contains(BaselinePattern))
            .ToList();

        var htmlBuilder = new StringBuilder();
        htmlBuilder.AppendLine("<!DOCTYPE html>");
        htmlBuilder.AppendLine("<html lang=\"en\">");
        htmlBuilder.AppendLine("<head>");
        htmlBuilder.AppendLine("<meta charset=\"UTF-8\">");
        htmlBuilder.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        htmlBuilder.AppendLine("<title>Color Scape Comparison</title>");
        htmlBuilder.AppendLine("<style>");
        htmlBuilder.AppendLine("body { font-family: Arial, sans-serif; }");
        htmlBuilder.AppendLine(".container { display: flex; flex-wrap: wrap; }");
        htmlBuilder.AppendLine(".image-row { display: flex; align-items: center; margin-bottom: 20px; width: 100% }");
        htmlBuilder.AppendLine(".image-row img { margin-right: 10px; max-width: 200px; height: auto; }");
        htmlBuilder.AppendLine(".color-square { width: 100px; height: 100px; margin-right: 10px; }");
        htmlBuilder.AppendLine("</style>");
        htmlBuilder.AppendLine("</head>");
        htmlBuilder.AppendLine("<body>");
        htmlBuilder.AppendLine("<div class=\"container\">");

        foreach (var imagePath in imageFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(imagePath);
            var primaryPath = Path.Combine(_testDirectoryColorScapes, $"{fileName}_primary_output.png");
            var secondaryPath = Path.Combine(_testDirectoryColorScapes, $"{fileName}_secondary_output.png");

            htmlBuilder.AppendLine("<div class=\"image-row\">");
            htmlBuilder.AppendLine($"<p>{fileName}</p>");
            htmlBuilder.AppendLine($"<img src=\"./{Path.GetFileName(imagePath)}\" alt=\"{fileName}\">");
            if (File.Exists(primaryPath))
            {
                htmlBuilder.AppendLine($"<img class=\"color-square\" src=\"./{Path.GetFileName(primaryPath)}\" alt=\"{fileName} primary color\">");
            }
            if (File.Exists(secondaryPath))
            {
                htmlBuilder.AppendLine($"<img class=\"color-square\" src=\"./{Path.GetFileName(secondaryPath)}\" alt=\"{fileName} secondary color\">");
            }
            htmlBuilder.AppendLine("</div>");
        }

        htmlBuilder.AppendLine("</div>");
        htmlBuilder.AppendLine("</body>");
        htmlBuilder.AppendLine("</html>");

        File.WriteAllText(Path.Combine(_testDirectoryColorScapes, "colorscape_index.html"), htmlBuilder.ToString());
    }
}
