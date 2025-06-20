using API.DTOs.Progress;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using API.Services.Tasks.Scanner.Parser;

namespace API.Helpers;

/// <summary>
/// All things related to Koreader
/// </summary>
/// <remarks>Original developer: https://github.com/MFDeAngelo</remarks>
public static class KoreaderHelper
{
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
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath) || !Parser.IsEpub(filePath))
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
        var path = koreaderPosition.Split('/');
        if (path.Length < 6)
        {
            return;
        }

        var docNumber = path[2].Replace("DocFragment[", string.Empty).Replace("]", string.Empty);
        progress.PageNum = int.Parse(docNumber) - 1;
        var lastTag = path[5].ToUpper();

        if (lastTag == "A")
        {
            progress.BookScrollId = null;
        }
        else
        {
            // The format that Kavita accepts as a progress string. It tells Kavita where Koreader last left off.
            progress.BookScrollId = $"//html[1]/BODY/APP-ROOT[1]/DIV[1]/DIV[1]/DIV[1]/APP-BOOK-READER[1]/DIV[1]/DIV[2]/DIV[1]/DIV[1]/DIV[1]/{lastTag}";
        }
    }


    public static string GetKoreaderPosition(ProgressDto progressDto)
    {
        string lastTag;
        var koreaderPageNumber = progressDto.PageNum + 1;

        if (string.IsNullOrEmpty(progressDto.BookScrollId))
        {
            lastTag = "a";
        }
        else
        {
            var tokens = progressDto.BookScrollId.Split('/');
            lastTag = tokens[^1].ToLower();
        }

        // The format that Koreader accepts as a progress string. It tells Koreader where Kavita last left off.
        return $"/body/DocFragment[{koreaderPageNumber}]/body/div/{lastTag}";
    }
}
