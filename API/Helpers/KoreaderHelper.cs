using API.DTOs.Koreader;
using API.DTOs.Progress;
using API.Services;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace API.Helpers;

public static class KoreaderHelper
{
    /// <summary>
    /// Hashes the document according to a custom Koreader hashing algorithm.
    /// Look at the util.partialMD5 method in the attached link.
    /// </summary>
    /// <remarks>The hashing algorithm is relatively quick as it only hashes ~10,000 bytes for the biggest of files.</remarks>
    /// <see href="https://github.com/koreader/koreader/blob/master/frontend/util.lua#L1040"/>
    /// <param name="filePath">The path to the file to hash</param>
    public static string HashContents(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return null;
        }

        using var file = File.OpenRead(filePath);

        var step = 1024;
        var size = 1024;
        MD5 md5 = MD5.Create();
        byte[] buffer = new byte[size];

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
        md5.TransformFinalBlock(new byte[0], 0, 0);

        return BitConverter.ToString(md5.Hash).Replace("-", string.Empty).ToUpper();

    }

    /// <summary>
    /// Koreader can identitfy documents based on contents or title.
    /// For now we only support by contents.
    /// </summary>
    public static string HashTitle(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var fileNameBytes = Encoding.ASCII.GetBytes(fileName);
        var bytes = MD5.HashData(fileNameBytes);

        return BitConverter.ToString(bytes).Replace("-", string.Empty);
    }

    public static void UpdateProgressDto(string koreaderPosition, ProgressDto progress)
    {
        var path = koreaderPosition.Split('/');
        if (path.Length < 6)
        {
            return;
        }
        var docNumber = path[2].Replace("DocFragment[", string.Empty).Replace("]", string.Empty);
        progress.PageNum = Int32.Parse(docNumber) - 1;
        var lastTag = path[5].ToUpper();
        if (lastTag == "A")
        {
            progress.BookScrollId = null;
        }
        else
        {
            // The format that Kavita accpets as a progress string. It tells Kavita where Koreader last left off.
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
            lastTag = progressDto.BookScrollId.Split('/').Last().ToLower();
        }
        // The format that Koreader accepts as a progress string. It tells Koreader where Kavita last left off.
        return $"/body/DocFragment[{koreaderPageNumber}]/body/div/{lastTag}";
    }
}
