using API.DTOs.Progress;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using API.Services;
using API.Services.Tasks.Scanner.Parser;

namespace API.Helpers;

/// <summary>
/// All things related to Koreader
/// </summary>
/// <remarks>Original developer: https://github.com/MFDeAngelo</remarks>
public static partial class KoreaderHelper
{
    [GeneratedRegex(@"DocFragment\[(\d+)\]")]
    private static partial Regex DocFragmentRegex();

    [GeneratedRegex(@"^\d+$")]
    private static partial Regex JustNumber();

    /// <summary>
    /// Hashes the document according to a custom Koreader hashing algorithm.
    /// Look at the util.partialMD5 method in the attached link.
    /// Note: Only applies to epub files
    /// </summary>
    /// <remarks>The hashing algorithm is relatively quick as it only hashes ~10,000 bytes for the biggest of files.</remarks>
    /// <see href="https://github.com/koreader/koreader/blob/master/frontend/util.lua#L1040"/>
    /// <param name="filePath">The path to the file to hash</param>
    public static string HashContents(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        using var file = File.OpenRead(filePath);

        const int step = 1024;
        const int size = 1024;
        var md5 = MD5.Create();
        var buffer = new byte[size];

        for (var i = -1; i < 10; i++)
        {
            file.Position = step << 2 * i;
            var bytesRead = file.Read(buffer, 0, size);
            if (bytesRead > 0)
            {
                md5.TransformBlock(buffer, 0, bytesRead, buffer, 0);
            }
            else
            {
                break;
            }
        }

        file.Close();
        md5.TransformFinalBlock([], 0, 0);

        return md5.Hash == null ? null : Convert.ToHexString(md5.Hash).ToUpper();
    }

    /// <summary>
    /// Koreader can identify documents based on contents or title.
    /// For now, we only support by contents.
    /// </summary>
    public static string HashTitle(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var fileNameBytes = Encoding.ASCII.GetBytes(fileName);
        var bytes = MD5.HashData(fileNameBytes);

        return Convert.ToHexString(bytes);
    }

    public static void UpdateProgressDto(ProgressDto progress, string koreaderPosition)
    {
        // #_doc_fragment26
        if (koreaderPosition.StartsWith("#_doc_fragment"))
        {
            var docNumber = koreaderPosition.Replace("#_doc_fragment", string.Empty);
            progress.PageNum = int.Parse(docNumber) - 1;
            return;
        }

        // Check if koreaderPosition is just a number, this indicates an Archive
        if (JustNumber().IsMatch(koreaderPosition))
        {
            progress.PageNum = int.Parse(koreaderPosition) - 1;
            return;
        }

        var path = koreaderPosition.Split('/');
        if (path.Length < 6)
        {
            // Handle cases like: /body/DocFragment[10].0
            if (path.Length == 3)
            {
                progress.PageNum = GetPageNumber(path);
            }
            return;
        }

        progress.PageNum = GetPageNumber(path);

        var lastPart = koreaderPosition.Split("/body/")[^1];
        var lastTag = path[5].ToUpper();

        // If lastPart ends in a .Decimal, remove it as it's not a valid xpath
        lastPart = lastPart.Split("/text()")[0];

        if (lastTag == "A")
        {
            progress.BookScrollId = null;
        }
        else
        {
            // The format that Kavita accepts as a progress string. It tells Kavita where Koreader last left off.
            progress.BookScrollId = $"//body/{lastPart}";
        }
    }

    private static int GetPageNumber(string[] path, int offset = 2)
    {
        if (offset >= path.Length) return 0;

        var match = DocFragmentRegex().Match(path[offset]);
        if (!match.Success) return 0;

        return int.Parse(match.Groups[1].Value) - 1;
    }

    /// <summary>
    /// The format that Koreader accepts as a progress string. It tells Koreader where Kavita last left off.
    /// </summary>
    /// <remarks>
    /// Koreader stores the format as:
    /// /body/DocFragment[fragment_index]/body/[xpath_to_element]
    /// fragment_index is the page number for the xhtml files
    /// </remarks>
    /// <param name="progressDto"></param>
    /// <returns></returns>
    public static string GetKoreaderPosition(ProgressDto progressDto)
    {
        var targetPath = !string.IsNullOrEmpty(progressDto.BookScrollId)
            ? progressDto.BookScrollId.Replace("//body/", string.Empty, StringComparison.InvariantCultureIgnoreCase)
            : "p[1]"; // Default to first paragraph if unknown

        // Add 1 back to match KOReader's 1-based indexing
        return $"/body/DocFragment[{progressDto.PageNum + 1}]/body/{targetPath}";
    }
}
