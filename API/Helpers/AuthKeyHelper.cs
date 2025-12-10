using System;
using System.Security.Cryptography;

namespace API.Helpers;

public static class AuthKeyHelper
{
    private const string AllowedCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    public const int MinKeyLength = 8;
    public const int MaxKeyLength = 32;
    public const string OpdsKeyName = "opds";
    public const string ImageOnlyKeyName = "image-only";



    public static string GenerateKey(int keyLength = 8)
    {
        if (keyLength is < MinKeyLength or > MaxKeyLength)
        {
            throw new ArgumentOutOfRangeException(nameof(keyLength),
                $"Key length must be between {MinKeyLength} and {MaxKeyLength}");
        }

        return RandomNumberGenerator.GetString(AllowedCharacters, keyLength);
    }
}
