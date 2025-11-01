using API.DTOs.Progress;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using API.Services;
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
        // #_doc_fragment26
        string docNumber;
        if (koreaderPosition.StartsWith("#_doc_fragment"))
        {
            docNumber = koreaderPosition.Replace("#_doc_fragment", string.Empty);
            progress.PageNum = int.Parse(docNumber) - 1;
            return;
        }

        var path = koreaderPosition.Split('/');
        if (path.Length < 6)
        {
            return;
        }

        docNumber = path[2].Replace("DocFragment[", string.Empty).Replace("]", string.Empty);
        progress.PageNum = int.Parse(docNumber) - 1;

        var lastPart = koreaderPosition.Split("/body/")[^1];
        var lastTag = path[5].ToUpper();

        // TODO: Enhance this code: /body/DocFragment[27]/body/section/p[3]/text().229 -> p[3] but we probably can get more

        if (lastTag == "A")
        {
            progress.BookScrollId = null;
        }
        else
        {
            // The format that Kavita accepts as a progress string. It tells Kavita where Koreader last left off.
            progress.BookScrollId = $"//html[1]/{BookService.BookReaderBodyScope[2..].ToLowerInvariant()}/{lastPart}";
        }
    }


    public static string GetKoreaderPosition(ProgressDto progressDto)
    {
        string nonBodyTag;
        var koreaderPageNumber = progressDto.PageNum + 1;

        if (string.IsNullOrEmpty(progressDto.BookScrollId))
        {
            nonBodyTag = "a";
        }
        else
        {
            // What we Store: //html[1]/BODY/APP-ROOT[1]/DIV[1]/DIV[1]/DIV[1]/APP-BOOK-READER[1]/DIV[1]/DIV[2]/DIV[1]/DIV[1]/DIV[1]/section/p[62]/text().0
            // What we Need to send back: section/p[62]/text().0
            nonBodyTag = progressDto.BookScrollId.Replace("//html[1]/", "//", StringComparison.InvariantCultureIgnoreCase).Replace(BookService.BookReaderBodyScope + "/", string.Empty, StringComparison.InvariantCultureIgnoreCase);
        }

        // The format that Koreader accepts as a progress string. It tells Koreader where Kavita last left off.
        return $"/body/DocFragment[{koreaderPageNumber}]/body/{nonBodyTag}";
    }
}
