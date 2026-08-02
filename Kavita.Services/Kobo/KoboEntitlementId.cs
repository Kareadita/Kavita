using System;
using System.Security.Cryptography;
using System.Text;

namespace Kavita.Services.Kobo;

/// <summary>
/// Deterministic UUID v5 entitlement ids for Kobo chapters.
/// Namespace is fixed; name is <c>chapter:{Chapter.Id}</c>.
/// </summary>
public static class KoboEntitlementId
{
    /// <summary>
    /// Fixed Kavita namespace for Kobo entitlement UUID v5 generation.
    /// </summary>
    public static readonly Guid Namespace = new("9c8e2f4a-6b1d-4e3c-a7f5-2d8b1c0e9a34");

    public static Guid FromChapterId(int chapterId) =>
        CreateVersion5(Namespace, $"chapter:{chapterId}");

    public static string FromChapterIdString(int chapterId) =>
        FromChapterId(chapterId).ToString();

    /// <summary>
    /// RFC 4122 UUID version 5 (SHA-1 name-based).
    /// </summary>
    public static Guid CreateVersion5(Guid namespaceId, string name)
    {
        var namespaceBytes = namespaceId.ToByteArray();
        SwapByteOrder(namespaceBytes);
        var nameBytes = Encoding.UTF8.GetBytes(name);

        var data = new byte[namespaceBytes.Length + nameBytes.Length];
        Buffer.BlockCopy(namespaceBytes, 0, data, 0, namespaceBytes.Length);
        Buffer.BlockCopy(nameBytes, 0, data, namespaceBytes.Length, nameBytes.Length);

        var hash = SHA1.HashData(data);
        var newGuid = new byte[16];
        Array.Copy(hash, 0, newGuid, 0, 16);

        newGuid[6] = (byte)((newGuid[6] & 0x0F) | 0x50); // version 5
        newGuid[8] = (byte)((newGuid[8] & 0x3F) | 0x80); // RFC 4122 variant

        SwapByteOrder(newGuid);
        return new Guid(newGuid);
    }

    private static void SwapByteOrder(byte[] guid)
    {
        (guid[0], guid[3]) = (guid[3], guid[0]);
        (guid[1], guid[2]) = (guid[2], guid[1]);
        (guid[4], guid[5]) = (guid[5], guid[4]);
        (guid[6], guid[7]) = (guid[7], guid[6]);
    }
}
