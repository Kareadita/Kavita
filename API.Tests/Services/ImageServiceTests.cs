using System.Drawing;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using API.Entities.Enums;
using API.Services;
using EasyCaching.Core;
using Microsoft.Extensions.Logging;
using NetVips;
using NSubstitute;
using Xunit;
using Image = NetVips.Image;

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
        foreach (var imagePath in imageFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(imagePath);
            var dims = CoverImageSize.Default.GetDimensions();
            using var sourceImage = Image.NewFromFile(imagePath, false, Enums.Access.SequentialUnbuffered);

            var size = ImageService.GetSizeForDimensions(sourceImage, dims.Width, dims.Height);
            var crop = ImageService.GetCropForDimensions(sourceImage, dims.Width, dims.Height);

            using var thumbnail = Image.Thumbnail(imagePath, dims.Width, dims.Height,
                size: size,
                crop: crop);

            var outputFileName = fileName + outputExtension + ".png";
            thumbnail.WriteToFile(Path.Join(_testDirectory, outputFileName));
        }
    }

    private void GenerateHtmlFile()
    {
        var imageFiles = Directory.GetFiles(_testDirectory, "*.*")
            .Where(file => !file.EndsWith("html"))
            .Where(file => !file.Contains(OutputPattern) && !file.Contains(BaselinePattern))
            .ToList();

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

            using var sourceImage = Image.NewFromFile(imagePath, false, Enums.Access.SequentialUnbuffered);
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

        // Step 3: Process each image
        foreach (var imagePath in imageFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(imagePath);
            var colors = ImageService.CalculateColorScape(imagePath);

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
        var color = ImageService.HexToRgb(hexColor);
        using var colorImage = Image.Black(200, 100);
        using var output = colorImage + new[] { color.R / 255.0, color.G / 255.0, color.B / 255.0 };
        output.WriteToFile(outputPath);
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
