using System;
using System.IO;
using System.Threading;
using Kavita.API.Services;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Extensions;
using Kavita.Models.Images;
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
        var profile = ImageProfile.For(kind);
        Directory.CreateDirectory(directory);
        var tmpPath = Path.Join(directory, $"{Path.GetFileNameWithoutExtension(fileName)}.{Guid.NewGuid():N}.tmp");

        try
        {
            // Writes the staged file then Moves it into place
            var staged = Stage(source, profile, policy, policy.Format.ForKind(kind), tmpPath);
            MoveIntoPlace(tmpPath, Path.Join(directory, fileName));

            return new ImageWriteResult(fileName, staged.Width, staged.Height, staged.SourceWidth, staged.SourceHeight);
        }
        finally
        {
            DeleteQuietly(tmpPath);
        }
    }

    private static StagedImage Stage(ImageSource source, ImageProfile profile, ImagePolicy policy, EncodeFormat format, string tmpPath)
    {
        switch (source)
        {
            case ImageSource.FromFile file:
            {
                using var header = Image.NewFromFile(file.Path, access: Enums.Access.Sequential);
                var target = GetThumbnailTarget(header, profile, policy);
                using var thumbnail = target is { } t
                    ? Image.Thumbnail(file.Path, t.Width, height: t.Height, size: t.Size, crop: t.Crop)
                    : null;

                return Save(thumbnail ?? header, header, tmpPath, format);
            }
            case ImageSource.FromBytes bytes:
            {
                using var header = Image.NewFromBuffer(bytes.Data);
                var target = GetThumbnailTarget(header, profile, policy);
                using var thumbnail = target is { } t
                    ? Image.ThumbnailBuffer(bytes.Data, t.Width, height: t.Height, size: t.Size, crop: t.Crop)
                    : null;

                return Save(thumbnail ?? header, header, tmpPath, format);
            }
            case ImageSource.FromStream fromStream:
                return StageStream(fromStream.Stream, profile, policy, format, tmpPath);
            default:
                throw new ArgumentOutOfRangeException(nameof(source), source.GetType().Name, null);
        }
    }

    private static StagedImage StageStream(Stream stream, ImageProfile profile, ImagePolicy policy, EncodeFormat format, string tmpPath)
    {
        if (stream.CanSeek) stream.Position = 0;
        using var source = Image.NewFromStream(stream);

        var target = GetThumbnailTarget(source, profile, policy);
        if (target is not { } t)
        {
            return Save(source, source, tmpPath, format);
        }

        using var thumbnail = source.ThumbnailImage(t.Width, t.Height, size: t.Size, crop: t.Crop);
        try
        {
            return Save(thumbnail, source, tmpPath, format);
        }
        catch (VipsException)
        {
            // Saving a PDF cover decoded from a stream can fail: https://github.com/kleisauke/net-vips/issues/234
            if (stream.CanSeek) stream.Position = 0;
            using var retry = Image.ThumbnailStream(stream, t.Width, height: t.Height, size: t.Size, crop: t.Crop);

            return Save(retry, source, tmpPath, format);
        }
    }

    private static ThumbnailTarget? GetThumbnailTarget(Image source, ImageProfile profile, ImagePolicy policy)
    {
        switch (profile.Size)
        {
            case ImageSizeRule.CoverSize:
                var (width, height) = policy.CoverSize.GetDimensions();
                return new ThumbnailTarget(width, height, GetSizeForDimensions(source, width, height),
                    GetCropForDimensions(source, width, height));
            case ImageSizeRule.FitWithin:
                return new ThumbnailTarget(profile.Width, profile.Height, Enums.Size.Down, null);
            case ImageSizeRule.Original:
                return null;
            default:
                throw new ArgumentOutOfRangeException(nameof(profile), profile.Size, null);
        }
    }

    private static StagedImage Save(Image output, Image source, string path, EncodeFormat format)
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

        return new StagedImage(output.Width, output.Height, source.Width, source.Height);
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
    public static Enums.Size GetSizeForDimensions(Image image, int targetWidth, int targetHeight)
    {
        try
        {
            if (WillScaleWell(image, targetWidth, targetHeight) || IsLikelyWideImage(image.Width, image.Height))
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
    /// <param name="image">Source image, only its dimensions are read</param>
    /// <param name="targetWidth">Width of the cover being produced</param>
    /// <param name="targetHeight">Height of the cover being produced</param>
    /// <returns>No crop when the image will scale well or is wide, otherwise <see cref="Enums.Interesting.Attention"/></returns>
    public static Enums.Interesting? GetCropForDimensions(Image image, int targetWidth, int targetHeight)
    {
        try
        {
            if (WillScaleWell(image, targetWidth, targetHeight) || IsLikelyWideImage(image.Width, image.Height))
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
    /// <param name="sourceImage">Source image, only its dimensions are read</param>
    /// <param name="targetWidth">Width of the cover being produced</param>
    /// <param name="targetHeight">Height of the cover being produced</param>
    /// <param name="tolerance">How far the two aspect ratios may differ</param>
    /// <returns>True when the aspect ratios are within <paramref name="tolerance"/> and neither side grows more than 2x</returns>
    public static bool WillScaleWell(Image sourceImage, int targetWidth, int targetHeight, double tolerance = 0.1)
    {
        // Calculate the aspect ratios
        var sourceAspectRatio = (double) sourceImage.Width / sourceImage.Height;
        var targetAspectRatio = (double) targetWidth / targetHeight;

        // Compare aspect ratios
        if (Math.Abs(sourceAspectRatio - targetAspectRatio) > tolerance)
        {
            return false; // Aspect ratios differ significantly
        }

        // Calculate scaling factors
        var widthScaleFactor = (double) targetWidth / sourceImage.Width;
        var heightScaleFactor = (double) targetHeight / sourceImage.Height;

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

    private readonly record struct ThumbnailTarget(int Width, int Height, Enums.Size Size, Enums.Interesting? Crop);

    private readonly record struct StagedImage(int Width, int Height, int SourceWidth, int SourceHeight);
}
