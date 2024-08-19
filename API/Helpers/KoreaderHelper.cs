using API.DTOs.Koreader;
using API.DTOs.Progress;
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
    /// Hashes the document according to a custom Koreader algorithm
    /// </summary>
    /// <remarks>The hashing algorithm is relatively quick as it only hashes ~10,000 bytes for the biggest of files.</remarks>
    /// <param name="filePath">The path to the file to hash</param>
    public static string HashContents(string filePath)
    {

        if (string.IsNullOrEmpty(filePath))
            return null;

        using (var file = File.OpenRead(filePath))
        {
            int step = 1024;
            int size = 1024;
            MD5 md5 = MD5.Create();
            byte[] buffer = new byte[size];

            for (int i = -1; i < 10; i++)
            {
                file.Position = step << 2 * i;
                int bytesRead = file.Read(buffer, 0, size);
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
            byte[] hashBytes = md5.Hash;
            return BitConverter.ToString(hashBytes).Replace("-", "").ToUpper();
        }
    }

    /// <summary>
    /// Koreader can identitfy documents based on contents or title.
    /// For now we only support by contents.
    /// </summary>
    public static string HashTitle(string filePath)
    {
        using var md5 = MD5.Create();
        var fileName = Path.GetFileName(filePath);
        var fileNameBytes = Encoding.ASCII.GetBytes(fileName);
        byte[] bytes = md5.ComputeHash(fileNameBytes);
        return BitConverter.ToString(bytes).Replace("-", "");
    }

    public static void UpdateProgressDto(string koreaderPosition, ProgressDto progress)
    {
        var path = koreaderPosition.Split('/');
        var docNumber = path[2].Replace("DocFragment[", "").Replace("]", "");
        progress.PageNum = Int32.Parse(docNumber) - 1;
        string lastTag = path[5].ToUpper();
        if (lastTag == "A")
        {
            Console.WriteLine("It's an A tag!");
            progress.BookScrollId = null;
        }
        else
        {
            progress.BookScrollId = $"//html[1]/BODY/APP-ROOT[1]/DIV[1]/DIV[1]/DIV[1]/APP-BOOK-READER[1]/DIV[1]/DIV[2]/DIV[1]/DIV[1]/DIV[1]/{lastTag}";
        }
    }


    public static string GetKoreaderPosition(ProgressDto progressDto)
    {
        string lastTag;
        int koreaderPageNumber = progressDto.PageNum + 1;
        if (string.IsNullOrEmpty(progressDto.BookScrollId))
            lastTag = "a";
        else
            lastTag = progressDto.BookScrollId.Split('/').Last().ToLower();
        return $"/body/DocFragment[{koreaderPageNumber}]/body/div/{lastTag}";
    }
}
