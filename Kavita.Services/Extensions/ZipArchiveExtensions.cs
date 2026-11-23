using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace Kavita.Services.Extensions;

public static class ZipArchiveExtensions
{
    private const uint EndOfCentralDirectorySignature = 0x06054b50;
    private const uint Zip64EndOfCentralDirectorySignature = 0x06064b50;
    private const uint Zip64LocatorSignature = 0x07064b50;
    private const uint CentralDirectoryHeaderSignature = 0x02014b50;
    private const ushort UnicodePathExtraFieldId = 0x7075;
    private const int EndOfCentralDirectorySize = 22;
    private const int MaxCommentLength = ushort.MaxValue;

    /// <summary>
    /// Checks if the archive has one or more files. Excludes directory entries.
    /// </summary>
    /// <param name="archive"></param>
    /// <returns></returns>
    public static bool HasFiles(this ZipArchive archive)
    {
        return archive.Entries.Any(x => Path.HasExtension(x.FullName));
    }

    /// <summary>
    /// Reads the Info-ZIP Unicode Path extra field (0x7075) for every entry in the archive's central directory.
    /// <see cref="ZipArchive"/> does not surface extra fields, and it decodes entry names with the current default
    /// encoding unless the UTF-8 flag is set. Archives written by tools that store a legacy code page (CP949, CP932,
    /// GBK, ...) without that flag lose characters on decode, and the replacement characters can collapse distinct
    /// pages onto a single file name.
    /// </summary>
    /// <param name="archivePath">Path to a zip archive on disk</param>
    /// <returns>One name per central directory entry, in central directory order. Null where no valid field exists.</returns>
    public static IReadOnlyList<string?> ReadUnicodePathExtraFields(string archivePath)
    {
        var names = new List<string?>();

        using var stream = File.Open(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        var (entryCount, centralDirectoryOffset) = ReadEndOfCentralDirectory(reader);
        if (entryCount <= 0 || centralDirectoryOffset <= 0 || centralDirectoryOffset >= stream.Length) return names;

        stream.Position = centralDirectoryOffset;
        for (var i = 0; i < entryCount; i++)
        {
            if (stream.Position + 46 > stream.Length) break;
            if (reader.ReadUInt32() != CentralDirectoryHeaderSignature) break;

            reader.ReadUInt16(); // version made by
            reader.ReadUInt16(); // version needed to extract
            reader.ReadUInt16(); // general purpose bit flag
            reader.ReadUInt16(); // compression method
            reader.ReadUInt16(); // last mod file time
            reader.ReadUInt16(); // last mod file date
            reader.ReadUInt32(); // crc-32
            reader.ReadUInt32(); // compressed size
            reader.ReadUInt32(); // uncompressed size
            var nameLength = reader.ReadUInt16();
            var extraLength = reader.ReadUInt16();
            var commentLength = reader.ReadUInt16();
            reader.ReadUInt16(); // disk number start
            reader.ReadUInt16(); // internal file attributes
            reader.ReadUInt32(); // external file attributes
            reader.ReadUInt32(); // relative offset of local header

            stream.Position += nameLength;
            var extra = reader.ReadBytes(extraLength);
            stream.Position += commentLength;

            names.Add(ReadUnicodePathName(extra));
        }

        return names;
    }

    private static (long EntryCount, long CentralDirectoryOffset) ReadEndOfCentralDirectory(BinaryReader reader)
    {
        var stream = reader.BaseStream;
        var searchLength = (int)Math.Min(stream.Length, EndOfCentralDirectorySize + MaxCommentLength);

        var tail = new byte[searchLength];
        stream.Position = stream.Length - searchLength;
        var read = stream.Read(tail, 0, searchLength);
        if (read < EndOfCentralDirectorySize) return (0, 0);

        var eocdIndex = -1;
        for (var i = read - EndOfCentralDirectorySize; i >= 0; i--)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(i)) != EndOfCentralDirectorySignature) continue;

            // The record must end exactly at the end of the file. A signature appearing inside the archive comment
            // would otherwise be picked as the EOCD and every field read after it would be garbage.
            var commentLength = BinaryPrimitives.ReadUInt16LittleEndian(tail.AsSpan(i + 20));
            if (i + EndOfCentralDirectorySize + commentLength != read) continue;

            eocdIndex = i;
            break;
        }

        if (eocdIndex < 0) return (0, 0);

        var eocdOffset = stream.Length - searchLength + eocdIndex;
        var entryCount = (long)BinaryPrimitives.ReadUInt16LittleEndian(tail.AsSpan(eocdIndex + 10));
        var centralDirectoryOffset = (long)BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(eocdIndex + 16));

        // Zip64: the 32 bit fields are saturated and the real values live in the Zip64 end of central directory record
        if (entryCount != ushort.MaxValue && centralDirectoryOffset != uint.MaxValue) return (entryCount, centralDirectoryOffset);

        var locatorOffset = eocdOffset - 20;
        if (locatorOffset < 0) return (entryCount, centralDirectoryOffset);

        stream.Position = locatorOffset;
        if (reader.ReadUInt32() != Zip64LocatorSignature) return (entryCount, centralDirectoryOffset);

        reader.ReadUInt32(); // number of the disk with the start of the Zip64 EOCD
        var zip64EocdOffset = (long)reader.ReadUInt64();
        if (zip64EocdOffset < 0 || zip64EocdOffset + 56 > stream.Length) return (entryCount, centralDirectoryOffset);

        stream.Position = zip64EocdOffset;
        if (reader.ReadUInt32() != Zip64EndOfCentralDirectorySignature) return (entryCount, centralDirectoryOffset);

        reader.ReadUInt64(); // size of the Zip64 EOCD record
        reader.ReadUInt16(); // version made by
        reader.ReadUInt16(); // version needed to extract
        reader.ReadUInt32(); // number of this disk
        reader.ReadUInt32(); // number of the disk with the start of the central directory
        reader.ReadUInt64(); // total entries on this disk
        entryCount = (long)reader.ReadUInt64();
        reader.ReadUInt64(); // size of the central directory
        centralDirectoryOffset = (long)reader.ReadUInt64();

        return (entryCount, centralDirectoryOffset);
    }

    /// <summary>
    /// Replaces characters the host OS cannot store in a file name. A no-op on Linux, where only '/' and NUL are
    /// invalid, but on Windows the name recorded in the extra field can contain ':' or '?' and would otherwise abort
    /// the whole extraction.
    /// </summary>
    /// <param name="entryName">Relative entry name, already normalised to '/' separators</param>
    /// <param name="invalidChars">Characters to replace. Defaults to the current platform's set; tests pass Windows' set.</param>
    public static string SanitizeEntryName(string entryName, char[]? invalidChars = null)
    {
        invalidChars ??= Path.GetInvalidFileNameChars();
        if (entryName.Length == 0 || entryName.IndexOfAny(invalidChars) < 0) return entryName;

        var segments = entryName.Split('/');
        for (var i = 0; i < segments.Length; i++)
        {
            if (segments[i].Length == 0) continue;

            foreach (var invalid in invalidChars)
            {
                segments[i] = segments[i].Replace(invalid, '_');
            }
        }

        return string.Join('/', segments);
    }

    private static string? ReadUnicodePathName(byte[] extra)
    {
        var offset = 0;
        while (offset + 4 <= extra.Length)
        {
            var id = BinaryPrimitives.ReadUInt16LittleEndian(extra.AsSpan(offset));
            var size = BinaryPrimitives.ReadUInt16LittleEndian(extra.AsSpan(offset + 2));
            if (offset + 4 + size > extra.Length) return null;

            if (id == UnicodePathExtraFieldId)
            {
                var payload = extra.AsSpan(offset + 4, size);
                // 1 byte version, 4 byte crc-32 of the original name, then the UTF-8 name
                if (payload.Length < 6 || payload[0] != 1) return null;

                try
                {
                    return new UTF8Encoding(false, true).GetString(payload[5..]);
                }
                catch (DecoderFallbackException)
                {
                    return null;
                }
            }

            offset += 4 + size;
        }

        return null;
    }
}
