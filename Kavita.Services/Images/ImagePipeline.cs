using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Kavita.API.Services;
using Kavita.Common.Extensions;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Extensions;
using Kavita.Models.Images;
using Kavita.Services.Images.ImageSteps;
using Microsoft.Extensions.Logging;
using NetVips;
using Image = NetVips.Image;

namespace Kavita.Services.Images;

public class ImagePipeline(ILogger<ImagePipeline> logger) : IImagePipeline
{
    // Windows needs these when moving files that might have a handle on them
    private static readonly int[] MoveRetryDelaysMs = [50, 100, 200];

    /// <inheritdoc />
    public ImageWriteResult Write(ImageSource source, ImageKind kind, ImagePolicy policy, string directory, string fileName)
    {
        var steps = GetNewImageSteps(ImageProfile.For(kind), policy);
        var format = policy.Format.ForKind(kind);

        return StageAndMove(directory, fileName, tmpPath => Run(source, steps, img => EncodeToFile(img, tmpPath, format)));
    }

    /// <inheritdoc />
    public ImageWriteResult Copy(string sourcePath, ImageKind kind, string directory, string fileName)
    {
        int width, height;
        using (var header = Image.NewFromFile(sourcePath, access: Enums.Access.Sequential))
        {
            width = header.Width;
            height = header.Height;
        }

        EnsureInsideFolder(directory, fileName);
        Directory.CreateDirectory(directory);
        var tmpPath = Path.Join(directory, $"{Path.GetFileNameWithoutExtension(fileName)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.Copy(sourcePath, tmpPath);
            MoveIntoPlace(tmpPath, Path.Join(directory, fileName));

            return new ImageWriteResult(fileName, width, height, width, height);
        }
        finally
        {
            DeleteQuietly(tmpPath);
        }
    }

    /// <inheritdoc />
    public ImageWriteResult Reencode(string sourcePath, ImageKind kind, ImagePolicy policy, string directory, string fileName)
    {
        // Read into memory due to WebP opened from disk keeping its handle on Windows, so it could not be replaced or deleted
        return Reencode(File.ReadAllBytes(sourcePath), kind, policy, directory, fileName);
    }

    /// <inheritdoc />
    public ImageWriteResult Reencode(byte[] sourceBytes, ImageKind kind, ImagePolicy policy, string directory, string fileName)
    {
        var steps = GetReencodeSteps(ImageProfile.For(kind), policy);
        var format = policy.Format.ForKind(kind);

        return StageAndMove(directory, fileName,
            tmpPath => Run(new ImageSource.FromBytes(sourceBytes), steps, img => EncodeToFile(img, tmpPath, format)));
    }

    /// <inheritdoc />
    public ImageRenderResult Render(ImageSource source, ImageRender render)
    {
        byte[] bytes = [];
        var staged = Run(source, render.Steps, img => bytes = EncodeToBuffer(img, render.Format, render.Quality));
        return new ImageRenderResult(bytes, staged.Width, staged.Height);
    }

    /// <inheritdoc />
    public ImageHeader? Inspect(ImageSource source)
    {
        try
        {
            using var header = OpenHeader(source);
            return new ImageHeader(GetExtension(header), header.Width, header.Height);
        }
        catch (VipsException)
        {
            return null;
        }
    }

    private static string? GetExtension(Image image)
    {
        // libvips names the loader by source: pngload from a file, pngload_buffer from memory, pngload_source from a stream
        return (string) image.Get("vips-loader") switch
        {
            "pngload" or "pngload_buffer" or "pngload_source" => ".png",
            "jpegload" or "jpegload_buffer" or "jpegload_source" => ".jpg",
            "webpload" or "webpload_buffer" or "webpload_source" => ".webp",
            "gifload" or "gifload_buffer" or "gifload_source" => ".gif",
            // heifload also opens HEIC, which browsers can't show
            "heifload" or "heifload_buffer" or "heifload_source" when image.Contains("heif-compression") && (string) image.Get("heif-compression") == "av1" => ".avif",
            _ => null,
        };
    }

    private ImageWriteResult StageAndMove(string directory, string fileName, Func<string, StagedImage> stage)
    {
        EnsureInsideFolder(directory, fileName);
        Directory.CreateDirectory(directory);
        var tmpPath = Path.Join(directory, $"{Path.GetFileNameWithoutExtension(fileName)}.{Guid.NewGuid():N}.tmp");

        try
        {
            var staged = stage(tmpPath);
            MoveIntoPlace(tmpPath, Path.Join(directory, fileName));

            return new ImageWriteResult(fileName, staged.Width, staged.Height, staged.SourceWidth, staged.SourceHeight);
        }
        finally
        {
            DeleteQuietly(tmpPath);
        }
    }

    internal static IReadOnlyList<IImageStep> GetNewImageSteps(ImageProfile profile, ImagePolicy policy)
    {
        switch (profile.Size)
        {
            case ImageSizeRule.CoverSize:
                var (width, height) = policy.CoverSize.GetDimensions();
                return [new CoverThumbnailStep(width, height)];
            case ImageSizeRule.FitWithin:
                return [new FitWithinStep(profile.Width, profile.Height)];
            case ImageSizeRule.Original:
                return []; // No IResizeStep to run
            default:
                throw new ArgumentOutOfRangeException(nameof(profile), profile.Size, null);
        }
    }

    internal static IReadOnlyList<IImageStep> GetReencodeSteps(ImageProfile profile, ImagePolicy policy)
    {
        var box = profile.GetBox(policy);
        if (box == null) return [];
        return [new FitWithinStep(box.Value.Width, box.Value.Height)];
    }

    /// <summary>
    /// Decodes <paramref name="source"/>, applies <paramref name="steps"/> in order and hands the final image to <paramref name="encode"/>.
    /// A resize step at index 0 is done while decoding
    /// </summary>
    internal static StagedImage Run(ImageSource source, IReadOnlyList<IImageStep> steps, Action<Image> encode)
    {
        var images = new List<Image>();
        try
        {
            var header = OpenHeader(source);
            images.Add(header);

            ThumbnailTarget? target = steps.Count > 0 && steps[0] is IResizeStep resize
                ? resize.TargetFor(header.Width, header.Height)
                : null;
            var remaining = target == null ? steps : steps.Skip(1).ToList();

            var loaded = target is { } t ? LoadWithTarget(source, header, t) : header;
            if (!ReferenceEquals(loaded, header))
            {
                images.Add(loaded);
            }

            var image = ApplySteps(loaded, remaining, images);
            try
            {
                encode(image);
            }
            catch (VipsException) when (target is { } retryTarget && source is ImageSource.FromStream { Stream.CanSeek: true } fromStream)
            {
                // Saving a PDF cover decoded from a stream can fail: https://github.com/kleisauke/net-vips/issues/234
                fromStream.Stream.Position = 0;
                var retry = Image.ThumbnailStream(fromStream.Stream, retryTarget.Width, height: retryTarget.Height,
                    size: retryTarget.Size, crop: retryTarget.Crop);
                images.Add(retry);

                image = ApplySteps(retry, remaining, images);
                encode(image);
            }

            return new StagedImage(image.Width, image.Height, header.Width, header.Height);
        }
        finally
        {
            foreach (var image in images)
            {
                image.Dispose();
            }
        }
    }

    private static Image ApplySteps(Image image, IReadOnlyList<IImageStep> steps, List<Image> images)
    {
        foreach (var step in steps)
        {
            image = step.Apply(image);
            images.Add(image);
        }

        return image;
    }

    private static Image OpenHeader(ImageSource source)
    {
        switch (source)
        {
            case ImageSource.FromBytes bytes:
                return Image.NewFromBuffer(bytes.Data);
            case ImageSource.FromFile file:
                return Image.NewFromFile(file.Path, access: Enums.Access.Sequential);
            case ImageSource.FromStream fromStream:
                if (fromStream.Stream.CanSeek)
                {
                    fromStream.Stream.Position = 0;
                }

                return Image.NewFromStream(fromStream.Stream);
            default:
                throw new ArgumentOutOfRangeException(nameof(source), source.GetType().Name, null);
        }
    }

    private static Image LoadWithTarget(ImageSource source, Image header, ThumbnailTarget target)
    {
        switch (source)
        {
            case ImageSource.FromBytes fromBytes:
                return Image.ThumbnailBuffer(fromBytes.Data, target.Width, height: target.Height, size: target.Size,
                    crop: target.Crop);
            case ImageSource.FromFile fromFile:
                return Image.Thumbnail(fromFile.Path, target.Width, height: target.Height, size: target.Size,
                    crop: target.Crop);
            case ImageSource.FromStream:
                // A stream can't be read once for the header and again to shrink while decoding, so the decoded image is resized
                return header.ThumbnailImage(target.Width, height: target.Height, size: target.Size, crop: target.Crop);
            default:
                throw new ArgumentOutOfRangeException(nameof(source));
        }
    }

    private static void EncodeToFile(Image output, string path, EncodeFormat format)
    {
        switch (format)
        {
            case EncodeFormat.PNG:
                output.Pngsave(path);
                break;
            case EncodeFormat.WEBP:
                output.Webpsave(path);
                break;
            case EncodeFormat.AVIF:
                // Heifsave defaults to HEVC; WriteToFile only picks AV1 from an .avif suffix, which a .tmp lacks
                output.Heifsave(path, compression: Enums.ForeignHeifCompression.Av1);
                break;
            case EncodeFormat.JPEG:
                // Jpegsave flattens transparency onto black, so a cover with a see-through background would turn black
                output.Jpegsave(path, background: [255, 255, 255]);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }
    }

    private static byte[] EncodeToBuffer(Image output, EncodeFormat format, int? quality)
    {
        return format switch
        {
            EncodeFormat.PNG => output.PngsaveBuffer(),
            EncodeFormat.WEBP => output.WebpsaveBuffer(),
            // Same as EncodeToFile: HeifsaveBuffer defaults to HEVC
            EncodeFormat.AVIF => output.HeifsaveBuffer(compression: Enums.ForeignHeifCompression.Av1),
            // Same as EncodeToFile: JpegsaveBuffer flattens transparency onto black
            EncodeFormat.JPEG => output.JpegsaveBuffer(q: quality, background: [255, 255, 255]),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
        };
    }

    private void MoveIntoPlace(string tmpPath, string targetPath)
    {
        foreach (var delay in MoveRetryDelaysMs)
        {
            if (TryMove(tmpPath, targetPath) == null) return;
            Thread.Sleep(delay);
        }

        var error = TryMove(tmpPath, targetPath);
        if (error == null) return;

        // Windows refuses a rename over a file another handle has open, but allows an overwriting copy
        logger.LogWarning(error, "Could not replace {File} while it was open elsewhere, copying over it instead", targetPath);
        File.Copy(tmpPath, targetPath, overwrite: true);
    }

    private static Exception? TryMove(string tmpPath, string targetPath)
    {
        try
        {
            File.Move(tmpPath, targetPath, overwrite: true);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ex;
        }
    }

    private static void EnsureInsideFolder(string directory, string fileName)
    {
        var target = Path.GetFullPath(Path.Join(directory, fileName));
        if (!target.IsInsideFolder(Path.GetFullPath(directory)))
        {
            throw new ArgumentException($"An image name cannot point outside {directory}: {fileName}", nameof(fileName));
        }
    }

    private void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not delete staged image {File}", path);
        }
    }

    /// <summary>
    /// Tries to determine if there is a better mode for resizing
    /// </summary>
    /// <param name="image">Source image, only its dimensions are read</param>
    /// <param name="targetWidth">Width of the cover being produced</param>
    /// <param name="targetHeight">Height of the cover being produced</param>
    /// <returns><see cref="Enums.Size.Force"/> when the image will scale well or is wide, otherwise <see cref="Enums.Size.Both"/></returns>
    public static Enums.Size GetSizeForDimensions(int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
    {
        try
        {
            if (WillScaleWell(sourceWidth, sourceHeight, targetWidth, targetHeight) || IsLikelyWideImage(sourceWidth, sourceHeight))
            {
                return Enums.Size.Force;
            }
        }
        catch (Exception)
        {
            /* Swallow */
        }

        return Enums.Size.Both;
    }

    /// <summary>
    /// Picks the crop to pair with <see cref="GetSizeForDimensions"/>
    /// </summary>
    /// <param name="targetWidth">Width of the cover being produced</param>
    /// <param name="targetHeight">Height of the cover being produced</param>
    /// <returns>No crop when the image will scale well or is wide, otherwise <see cref="Enums.Interesting.Attention"/></returns>
    public static Enums.Interesting? GetCropForDimensions(int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
    {
        try
        {
            if (WillScaleWell(sourceWidth, sourceHeight, targetWidth, targetHeight) || IsLikelyWideImage(sourceWidth, sourceHeight))
            {
                return null;
            }
        } catch (Exception)
        {
            /* Swallow */
            return null;
        }

        return Enums.Interesting.Attention;
    }

    /// <summary>
    /// Checks if the image can be resized to the target without cropping or a large upscale
    /// </summary>
    /// <param name="targetWidth">Width of the cover being produced</param>
    /// <param name="targetHeight">Height of the cover being produced</param>
    /// <param name="tolerance">How far the two aspect ratios may differ</param>
    /// <returns>True when the aspect ratios are within <paramref name="tolerance"/> and neither side grows more than 2x</returns>
    public static bool WillScaleWell(int sourceWidth, int sourceHeight, int targetWidth, int targetHeight, double tolerance = 0.1)
    {
        // Calculate the aspect ratios
        var sourceAspectRatio = (double) sourceWidth / sourceHeight;
        var targetAspectRatio = (double) targetWidth / targetHeight;

        // Compare aspect ratios
        if (Math.Abs(sourceAspectRatio - targetAspectRatio) > tolerance)
        {
            return false; // Aspect ratios differ significantly
        }

        // Calculate scaling factors
        var widthScaleFactor = (double) targetWidth / sourceWidth;
        var heightScaleFactor = (double) targetHeight / sourceHeight;

        // Check resolution quality (example thresholds)
        if (widthScaleFactor > 2.0 || heightScaleFactor > 2.0)
        {
            return false; // Scaling factor too large
        }

        return true; // Image will scale well
    }

    private static bool IsLikelyWideImage(int width, int height)
    {
        var aspectRatio = (double) width / height;
        return aspectRatio > 1.25;
    }

    internal readonly record struct StagedImage(int Width, int Height, int SourceWidth, int SourceHeight);
}
