using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

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
}
