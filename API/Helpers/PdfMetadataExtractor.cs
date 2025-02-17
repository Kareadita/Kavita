/**
 * Contributed by https://github.com/microtherion
 *
 * All references to the "PDF Spec" (section numbers, etc) refer to the
 * PDF 1.7 Specification a.k.a. PDF32000-1:2008
 * https://opensource.adobe.com/dc-acrobat-sdk-docs/pdfstandards/PDF32000_2008.pdf
 */

using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.IO;
using Microsoft.Extensions.Logging;
using API.Services;

namespace API.Helpers;
#nullable enable

/// <summary>
/// Parse PDF file and try to extract as much metadata as possible.
/// Supports both text based XRef tables and compressed XRef streams (Deflate only).
/// Supports both UTF-16 and PDFDocEncoding for strings.
/// Lacks support for many PDF configurations that are theoretically possible, but should handle most common cases.
/// </summary>
public class PdfMetadataExtractorException : Exception
{
    public PdfMetadataExtractorException()
    {
    }

    public PdfMetadataExtractorException(string message)
        : base(message)
    {
    }

    public PdfMetadataExtractorException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

public interface IPdfMetadataExtractor
{
    Dictionary<String, String> GetMetadata();
}

class PdfStringBuilder
{
    private readonly StringBuilder _builder = new();
    private bool _secondByte = false;
    private byte _prevByte = 0;
    private bool _isUnicode = false;

    // PDFDocEncoding defined in PDF Spec D.1

    private readonly char[] _pdfDocMappingLow =
    [
        '\u02D8', '\u02C7', '\u02C6', '\u02D9', '\u02DD', '\u02DB', '\u02DA', '\u02DC'
    ];

    private readonly char[] _pdfDocMappingHigh =
    [
        '\u2022', '\u2020', '\u2021', '\u2026', '\u2014', '\u2013', '\u0192', '\u2044',
        '\u2039', '\u203A', '\u2212', '\u2030', '\u201E', '\u201C', '\u201D', '\u2018',
        '\u2019', '\u201A', '\u2122', '\uFB01', '\uFB02', '\u0141', '\u0152', '\u0160',
        '\u0178', '\u017D', '\u0131', '\u0142', '\u0153', '\u0161', '\u017E', ' ',
        '\u20AC'
    ];

    private void AppendPdfDocByte(byte b)
    {
        if (b >= 0x18 && b < 0x20)
        {
            _builder.Append(_pdfDocMappingLow[b - 0x18]);
        }
        else if (b >= 0x80 && b < 0xA1)
        {
            _builder.Append(_pdfDocMappingHigh[b - 0x80]);
        }
        else
        {
            _builder.Append((char)b);
        }
    }

    public void Append(char c)
    {
        _builder.Append(c);
    }

    public void AppendByte(byte b)
    {
        // PDF Spec 7.9.2.1: Strings are either UTF-16BE or PDFDocEncoded
        if (_builder.Length == 0 && !_isUnicode)
        {
            // Unicode strings are prefixed by a big endian BOM \uFEFF
            if (_secondByte)
            {
                if (b == 0xFF)
                {
                    _isUnicode = true;
                    _secondByte = false;
                }
                else
                {
                    AppendPdfDocByte(_prevByte);
                    AppendPdfDocByte(b);
                }
            }
            else if (!_secondByte && b == 0xFE)
            {
                _secondByte = true;
                _prevByte = b;
            }
            else
            {
                AppendPdfDocByte(b);
            }
        }
        else if (_isUnicode)
        {
            if (_secondByte)
            {
                _builder.Append((char)(((char)_prevByte) << 8 | (char)b));
                _secondByte = false;
            }
            else
            {
                _prevByte = b;
                _secondByte = true;
            }
        }
        else
        {
            AppendPdfDocByte(b);
        }
    }

    override public string ToString()
    {
        if (_builder.Length == 0 && _secondByte)
        {
            AppendPdfDocByte(_prevByte);
        }

        return _builder.ToString();
    }
}

internal class PdfLexer(Stream stream)
{
    private const int BufferSize = 1024;
    private readonly byte[] _buffer = new byte[BufferSize];
    private int _pos = 0;
    private int _valid = 0;

    public enum TokenType
    {
        None,
        Bool,
        Int,
        Double,
        Name,
        String,
        ArrayStart,
        ArrayEnd,
        DictionaryStart,
        DictionaryEnd,
        StreamStart,
        StreamEnd,
        ObjectStart,
        ObjectEnd,
        ObjectRef,
        Keyword,
        Newline,
    }

    public struct Token(TokenType type, object value)
    {
        public TokenType Type = type;
        public object Value = value;
    }

    public Token NextToken(bool reportNewlines = false)
    {
        while (true)
        {
            switch ((char)NextByte())
            {
                case '\n' when reportNewlines:
                    return new Token(TokenType.Newline, true);

                case '\r' when reportNewlines:
                    if (NextByte() != '\n')
                    {
                        PutBack();
                    }
                    return new Token(TokenType.Newline, true);

                case ' ':
                case '\x00':
                case '\t':
                case '\n':
                case '\f':
                case '\r':
                    continue; // Skip whitespace

                case '%':
                    SkipComment();
                    continue;

                case '+':
                case '-':
                case '.':
                case >= '0' and <= '9':
                    return ScanNumber();

                case '/':
                    return ScanName();

                case '(':
                    return ScanString();

                case '[':
                    return new Token(TokenType.ArrayStart, true);

                case ']':
                    return new Token(TokenType.ArrayEnd, true);

                case '<':
                    if (NextByte() == '<')
                    {
                        return new Token(TokenType.DictionaryStart, true);
                    }
                    else
                    {
                        PutBack();
                        return ScanHexString();
                    }
                case '>':
                    ExpectByte((byte)'>');

                    return new Token(TokenType.DictionaryEnd, true);

                case >= 'a' and <= 'z':
                case >= 'A' and <= 'Z':
                    return ScanKeyword();

                default:
                    throw new PdfMetadataExtractorException("Unexpected byte, got {LastByte()}");
            }
        }
    }

    public void ResetBuffer()
    {
        _pos = 0;
        _valid = 0;
    }

    public bool TestByte(byte expected)
    {
        var result = NextByte() == expected;

        PutBack();

        return result;
    }

    public void ExpectNewline()
    {
        while (true)
        {
            var b = NextByte();
            switch ((char)b)
            {
                case ' ':
                case '\t':
                case '\f':
                    continue; // Skip whitespace

                case '\n':
                    return;

                case '\r':
                    if (NextByte() != '\n')
                    {
                        PutBack();
                    }

                    return;

                default:
                    throw new PdfMetadataExtractorException("Unexpected character, expected newline, got {b}");
            }
        }
    }

    public long GetXRefStart()
    {
        // Look for the startxref element as per PDF Spec 7.5.5
        while (true)
        {
            var b = NextByte();

            switch ((char)b)
            {
                case '\r':
                    b = NextByte();

                    if (b != '\n')
                    {
                        PutBack();
                    }

                    goto case '\n';

                case '\n':
                    // Handle consecutive newlines
                    while (true)
                    {
                        b = NextByte();

                        if (b == '\r')
                        {
                            goto case '\r';
                        }
                        else if (b == '\n')
                        {
                            goto case '\n';
                        }
                        else if (b == ' ' || b == '\t' || b == '\f')
                        {
                            continue;
                        }
                        else
                        {
                            PutBack();

                            break;
                        }
                    }

                    var token = NextToken(true);

                    if (token.Type == TokenType.Keyword && (string)token.Value == "startxref")
                    {
                        token = NextToken();

                        if (token.Type == TokenType.Int)
                        {
                            return (long)token.Value;
                        }
                        else
                        {
                            throw new PdfMetadataExtractorException("Expected integer after startxref keyword");
                        }
                    }

                    continue;

                default:
                    continue;
            }
        }
    }

    public bool NextXRefEntry(ref long obj, ref int generation)
    {
        // Cross-reference table entry as per PDF Spec 7.5.4

        WantLookahead(20);

        if (_valid - _pos < 20)
        {
            throw new PdfMetadataExtractorException("End of stream");
        }

        var inUse = true;

        if (obj == 0)
        {
            obj = Convert.ToInt64(Encoding.ASCII.GetString(_buffer, _pos, 10));
            generation = Convert.ToInt32(Encoding.ASCII.GetString(_buffer, _pos + 11, 5));
            inUse = _buffer[_pos + 17] == 'n';
        }

        _pos += 20;

        return inUse;
    }

    public Stream StreamObject(int length, bool deflate)
    {
        // Read a stream object as per PDF Spec 7.3.8
        // At the moment, we only accept uncompressed streams or the FlateDecode (PDF Spec 7.4.1) filter
        // with no parameters. These cover the vast majority of streams we're interested in.

        var rawData = new MemoryStream();

        ExpectNewline();

        if (_pos < _valid)
        {
            var buffered = Math.Min(_valid - _pos, length);
            rawData.Write(_buffer, _pos, buffered);
            length -= buffered;
            _pos += buffered;
        }

        while (length > 0)
        {
            var buffered = Math.Min(length, BufferSize);
            stream.ReadExactly(_buffer, 0, buffered);
            rawData.Write(_buffer, 0, buffered);
            _pos = 0;
            _valid = 0;
            length -= buffered;
        }

        rawData.Seek(0, SeekOrigin.Begin);

        if (deflate)
        {
            return new ZLibStream(rawData, CompressionMode.Decompress, false);
        }
        else
        {
            return rawData;
        }
    }

    private byte NextByte()
    {
        if (_pos >= _valid)
        {
            _pos = 0;
            _valid = stream.Read(_buffer, 0, BufferSize);

            if (_valid <= 0)
            {
                throw new PdfMetadataExtractorException("End of stream");
            }
        }

        return _buffer[_pos++];
    }

    private byte LastByte()
    {
        return _buffer[_pos - 1];
    }

    private void PutBack()
    {
        --_pos;
    }

    private void ExpectByte(byte expected)
    {
        if (NextByte() != expected)
        {
            throw new PdfMetadataExtractorException($"Unexpected character, expected {expected}");
        }
    }

    private void WantLookahead(int length)
    {
        if (_pos + length > _valid)
        {
            Buffer.BlockCopy(_buffer, _pos, _buffer, 0, _valid - _pos);
            _valid -= _pos;
            _pos = 0;
            _valid += stream.Read(_buffer, _valid, BufferSize - _valid);
        }
    }

    private void SkipComment()
    {
        while (true)
        {
            var b = NextByte();

            if (b == '\n')
            {
                break;
            }
            else if (b == '\r')
            {
                if (NextByte() != '\n')
                {
                    PutBack();
                }

                break;
            }
        }
    }

    private Token ScanNumber()
    {
        StringBuilder sb = new();
        var hasDot = LastByte() == '.';
        var followedBySpace = false;

        sb.Append((char)LastByte());

        while (true)
        {
            var b = NextByte();

            if (b == '.' || b >= '0' && b <= '9')
            {
                sb.Append((char)b);

                if (b == '.')
                {
                    hasDot = true;
                }
            }
            else
            {
                followedBySpace = (b == ' ' || b == '\t');
                PutBack();

                break;
            }
        }

        if (hasDot)
        {
            return new Token(TokenType.Double, double.Parse(sb.ToString()));
        }

        if (followedBySpace)
        {
            // Look ahead to see if it's an object reference (PDF Spec 7.3.10)
            WantLookahead(32);

            var savedPos = _pos;
            var b = NextByte();

            while (b == ' ' || b == '\t')
            {
                b = NextByte();
            }

            // Generation number (ignored)
            while (b >= '0' && b <= '9')
            {
                b = NextByte();
            }

            while (b == ' ' || b == '\t')
            {
                b = NextByte();
            }

            if (b == 'R')
            {
                return new Token(TokenType.ObjectRef, long.Parse(sb.ToString()));
            }
            else if (b == 'o' && NextByte() == 'b' && NextByte() == 'j')
            {
                return new Token(TokenType.ObjectStart, long.Parse(sb.ToString()));
            }
            else
            {
                _pos = savedPos;
            }
        }

        return new Token(TokenType.Int, long.Parse(sb.ToString()));
    }

    private static int HexDigit(byte b)
    {
        return (char) b switch
        {
            >= '0' and <= '9' => b - (byte) '0',
            >= 'a' and <= 'f' => b - (byte) 'a' + 10,
            >= 'A' and <= 'F' => b - (byte) 'A' + 10,
            _ => throw new PdfMetadataExtractorException("Invalid hex digit, got {b}")
        };
    }

    private Token ScanName()
    {
        // PDF Spec 7.3.5

        var sb = new StringBuilder();
        while (true)
        {
            var b = NextByte();
            switch ((char)b)
            {
                case '(':
                case ')':
                case '[':
                case ']':
                case '{':
                case '}':
                case '<':
                case '>':
                case '/':
                case '%':
                    PutBack();

                    goto case ' ';

                case ' ':
                case '\t':
                case '\n':
                case '\f':
                case '\r':
                    return new Token(TokenType.Name, sb.ToString());

                case '#':
                    var b1 = NextByte();
                    var b2 = NextByte();
                    b = (byte)((HexDigit(b1) << 4) | HexDigit(b2));

                    goto default;

                default:
                    sb.Append((char)b);
                    break;
            }
        }
    }

    private Token ScanString()
    {
        // PDF Spec 7.3.4.2

        PdfStringBuilder sb = new();
        var parenLevel = 1;

        while (true)
        {
            var b = NextByte();

            switch ((char)b)
            {
                case '(':
                    parenLevel++;

                    goto default;

                case ')':
                    if (--parenLevel == 0)
                    {
                        return new Token(TokenType.String, sb.ToString());
                    }

                    goto default;

                case '\\':
                    b = NextByte();

                    switch ((char)b)
                    {
                        case 'b':
                            sb.Append('\b');

                            break;

                        case 'f':
                            sb.Append('\f');

                            break;

                        case 'n':
                            sb.Append('\n');

                            break;

                        case 'r':
                            sb.Append('\r');

                            break;

                        case 't':
                            sb.Append('\t');

                            break;

                        case >= '0' and <= '7':
                            var b1 = b;
                            var b2 = NextByte();
                            var b3 = NextByte();

                            if (b2 < '0' || b2 > '7' || b3 < '0' || b3 > '7')
                            {
                                throw new PdfMetadataExtractorException("Invalid octal escape, got {b1}{b2}{b3}");
                            }

                            sb.AppendByte((byte)((b1 - '0') << 6 | (b2 - '0') << 3 | (b3 - '0')));

                            break;
                    }
                    break;

                default:
                    sb.AppendByte(b);
                    break;
            }
        }
    }

    private Token ScanHexString()
    {
        // PDF Spec 7.3.4.3

        PdfStringBuilder sb = new();

        while (true)
        {
            var b = NextByte();

            switch ((char)b)
            {
                case (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F'):
                    var b1 = NextByte();
                    if (b1 == '>')
                    {
                        PutBack();
                        b1 = (byte)'0';
                    }
                    sb.AppendByte((byte)(HexDigit(b) << 4 | HexDigit(b1)));

                    break;

                case '>':
                    return new Token(TokenType.String, sb.ToString());

                default:
                    throw new PdfMetadataExtractorException("Invalid hex string, got {b}");
            }
        }
    }

    private Token ScanKeyword()
    {
        StringBuilder sb = new();

        sb.Append((char)LastByte());

        while (true)
        {
            var b = NextByte();
            if ((b >= 'a' && b <= 'z') || (b >= 'A' && b <= 'Z'))
            {
                sb.Append((char)b);
            }
            else
            {
                PutBack();

                break;
            }
        }

        switch (sb.ToString())
        {
            case "true":
                return new Token(TokenType.Bool, true);

            case "false":
                return new Token(TokenType.Bool, false);

            case "stream":
                return new Token(TokenType.StreamStart, true);

            case "endstream":
                return new Token(TokenType.StreamEnd, true);

            case "endobj":
                return new Token(TokenType.ObjectEnd, true);

            default:
                return new Token(TokenType.Keyword, sb.ToString());
        }
    }
}

internal class PdfMetadataExtractor : IPdfMetadataExtractor
{
    private readonly ILogger<BookService> _logger;
    private readonly PdfLexer _lexer;
    private readonly FileStream _stream;
    private long[] _objectOffsets = new long[0];
    private readonly Dictionary<string, string> _metadata = [];
    private readonly Stack<MetadataRef> _metadataRef = new();

    private struct MetadataRef(long root, long info)
    {
        public long Root = root;
        public long Info = info;
    }

    private struct XRefSection(long first, long count)
    {
        public readonly long First = first;
        public readonly long Count = count;
    }

    public PdfMetadataExtractor(ILogger<BookService> logger, string filename)
    {
        _logger = logger;
        _stream = File.OpenRead(filename);
        _lexer = new PdfLexer(_stream);

        ReadObjectOffsets();
        ReadMetadata(filename);
    }

    public Dictionary<string, string> GetMetadata()
    {
        return _metadata;
    }

    private void LogMetadata(string filename)
    {
       _logger.LogTrace("Metadata for {Path}:", filename);

        foreach (var entry in _metadata)
        {
            _logger.LogTrace("   {Key:0,-5} : {Value:1}", entry.Key, entry.Value);
        }
    }

    private void ReadObjectOffsets()
    {
        // Look for file trailer (PDF Spec 7.5.5)
        // Spec says trailer must be strictly at end of file.
        // Adobe software accepts trailer within last 1K of EOF,
        // but in practice, virtually all PDFs have trailer at end.

        _stream.Seek(-32, SeekOrigin.End);

        var xrefOffset = _lexer.GetXRefStart();

        ReadXRefAndTrailer(xrefOffset);
    }

    private void ReadXRefAndTrailer(long xrefOffset)
    {
        _stream.Seek(xrefOffset, SeekOrigin.Begin);
        _lexer.ResetBuffer();

        if (!_lexer.TestByte((byte)'x'))
        {
            // Cross-reference stream (PDF Spec 7.5.8)

            ReadXRefStream();

            return;
        }

        // Cross-reference table (PDF Spec 7.5.4)

        var token = _lexer.NextToken();

        if (token.Type != PdfLexer.TokenType.Keyword || (string)token.Value != "xref")
        {
            throw new PdfMetadataExtractorException("Expected xref keyword");
        }

        while (true)
        {
            token = _lexer.NextToken();

            if (token.Type == PdfLexer.TokenType.Int)
            {
                var startObj = (long)token.Value;
                token = _lexer.NextToken();

                if (token.Type != PdfLexer.TokenType.Int)
                {
                    throw new PdfMetadataExtractorException("Expected number of objects in xref subsection");
                }

                var numObj = (long)token.Value;

                if (_objectOffsets.Length < startObj + numObj)
                {
                    Array.Resize(ref _objectOffsets, (int)(startObj + numObj));
                }

                _lexer.ExpectNewline();

                var generation = 0;

                for (var obj = startObj; obj < startObj + numObj; ++obj)
                {
                    var inUse = _lexer.NextXRefEntry(ref _objectOffsets[obj], ref generation);

                    if (!inUse)
                    {
                        _objectOffsets[obj] = 0;
                    }
                }
            }
            else if (token.Type == PdfLexer.TokenType.Keyword && (string)token.Value == "trailer")
            {
                break;
            }
            else
            {
                throw new PdfMetadataExtractorException("Unexpected token in xref");
            }
        }

        ReadTrailerDictionary();
    }

    private void ReadXRefStream()
    {
        // Cross-reference stream (PDF Spec 7.5.8)

        var token = _lexer.NextToken();

        if (token.Type != PdfLexer.TokenType.ObjectStart)
        {
            throw new PdfMetadataExtractorException("Expected obj keyword");
        }

        long length = -1;
        long size = -1;
        var deflate = false;
        long prev = -1;
        long typeWidth = -1;
        long offsetWidth = -1;
        long generationWidth = -1;
        Queue<XRefSection> sections = new();
        var meta = new MetadataRef(-1, -1);

        // Cross-reference stream dictionary (PDF Spec 7.5.8.2)

        ParseDictionary(delegate(string key, PdfLexer.Token value) {
            switch (key)
            {
                case "Type":
                    if (value.Type != PdfLexer.TokenType.Name || (string)value.Value != "XRef")
                    {
                        throw new PdfMetadataExtractorException("Expected /Type to be /XRef");
                    }

                    return true;

                case "Length":
                    if (value.Type != PdfLexer.TokenType.Int)
                    {
                        throw new PdfMetadataExtractorException("Expected integer after /Length");
                    }

                    length = (long)value.Value;

                    return true;

                case "Size":
                    if (value.Type != PdfLexer.TokenType.Int)
                    {
                        throw new PdfMetadataExtractorException("Expected integer after /Size");
                    }

                    size = (long)value.Value;

                    return true;

                case "Prev":
                    if (value.Type != PdfLexer.TokenType.Int)
                    {
                        throw new PdfMetadataExtractorException("Expected offset after /Prev");
                    }

                    prev = (long)value.Value;

                    return true;

                case "Index":
                    if (value.Type != PdfLexer.TokenType.ArrayStart)
                    {
                        throw new PdfMetadataExtractorException("Expected array after /Index");
                    }

                    while (true)
                    {
                        token = _lexer.NextToken();

                        if (token.Type == PdfLexer.TokenType.ArrayEnd)
                        {
                            break;
                        }
                        else if (token.Type != PdfLexer.TokenType.Int)
                        {
                            throw new PdfMetadataExtractorException("Expected integer in /Index array");
                        }

                        var first = (long)token.Value;
                        token = _lexer.NextToken();

                        if (token.Type != PdfLexer.TokenType.Int)
                        {
                            throw new PdfMetadataExtractorException("Expected integer pair in /Index array");
                        }

                        var count = (long)token.Value;
                        sections.Enqueue(new XRefSection(first, count));
                    }

                    return true;

                case "W":
                    if (value.Type != PdfLexer.TokenType.ArrayStart)
                    {
                        throw new PdfMetadataExtractorException("Expected array after /W");
                    }

                    var widths = new long[3];

                    for (var i = 0; i < 3; ++i)
                    {
                        token = _lexer.NextToken();

                        if (token.Type != PdfLexer.TokenType.Int)
                        {
                            throw new PdfMetadataExtractorException("Expected integer in /W array");
                        }

                        widths[i] = (long)token.Value;
                    }

                    token = _lexer.NextToken();

                    if (token.Type != PdfLexer.TokenType.ArrayEnd)
                    {
                        throw new PdfMetadataExtractorException("Unclosed array after /W");
                    }

                    typeWidth = widths[0];
                    offsetWidth = widths[1];
                    generationWidth = widths[2];

                    return true;

                case "Filter":
                    if (value.Type != PdfLexer.TokenType.Name)
                    {
                        throw new PdfMetadataExtractorException("Expected name after /Filter");
                    }

                    if ((string)value.Value != "FlateDecode")
                    {
                        throw new PdfMetadataExtractorException("Unsupported filter, only FlateDecode is supported");
                    }

                    deflate = true;

                    return true;

                case "Root":
                    if (value.Type != PdfLexer.TokenType.ObjectRef)
                    {
                        throw new PdfMetadataExtractorException("Expected object reference after /Root");
                    }

                    meta.Root = (long)value.Value;

                    return true;

                case "Info":
                    if (value.Type != PdfLexer.TokenType.ObjectRef)
                    {
                        throw new PdfMetadataExtractorException("Expected object reference after /Info");
                    }

                    meta.Info = (long)value.Value;

                    return true;

                default:
                    return false;
            }
        });

        token = _lexer.NextToken();

        if (token.Type != PdfLexer.TokenType.StreamStart)
        {
            throw new PdfMetadataExtractorException("Expected xref stream after dictionary");
        }

        var stream = _lexer.StreamObject((int)length, deflate);

        if (sections.Count == 0)
        {
            sections.Enqueue(new XRefSection(0, size));
        }

        while (sections.Count > 0)
        {
            var section = sections.Dequeue();

            if (_objectOffsets.Length < size)
            {
                Array.Resize(ref _objectOffsets, (int)size);
            }

            for (var i = section.First; i < section.First + section.Count; ++i)
            {
                long type = 0;
                long offset = 0;
                long generation = 0;

                if (typeWidth == 0)
                {
                    type = 1;
                }

                for (var j = 0; j < typeWidth; ++j)
                {
                    type = (type << 8) | (ushort)stream.ReadByte();
                }

                for (var j = 0; j < offsetWidth; ++j)
                {
                    offset = (offset << 8) | (ushort)stream.ReadByte();
                }

                for (var j = 0; j < generationWidth; ++j)
                {
                    generation = (generation << 8) | (ushort)stream.ReadByte();
                }

                if (type == 1 && _objectOffsets[i] == 0)
                {
                    _objectOffsets[i] = offset;
                }
            }
        }

        if (prev > -1)
        {
            ReadXRefAndTrailer(prev);
        }

        PushMetadataRef(meta);
    }

    private void PushMetadataRef(MetadataRef meta)
    {
        if (_metadataRef.Count > 0)
        {
            if (meta.Root == _metadataRef.Peek().Root)
            {
                meta.Root = -1;
            }

            if (meta.Info == _metadataRef.Peek().Info)
            {
                meta.Info = -1;
            }
        }

        if (meta.Root != -1 || meta.Info != -1)
        {
            _metadataRef.Push(meta);
        }
    }

    private void ReadTrailerDictionary()
    {
        // Read trailer directory (PDF Spec 7.5.5)

        long prev = -1;
        long xrefStm = -1;

        MetadataRef meta = new(-1, -1);

        ParseDictionary(delegate(string key, PdfLexer.Token value)
        {
            switch (key)
            {
                case "Root":
                    if (value.Type != PdfLexer.TokenType.ObjectRef)
                    {
                        throw new PdfMetadataExtractorException("Expected object reference after /Root");
                    }

                    meta.Root = (long)value.Value;

                    return true;
                case "Prev":
                    if (value.Type != PdfLexer.TokenType.Int)
                    {
                        throw new PdfMetadataExtractorException("Expected offset after /Prev");
                    }

                    prev = (long)value.Value;

                    return true;
                case "Info":
                    if (value.Type != PdfLexer.TokenType.ObjectRef)
                    {
                        throw new PdfMetadataExtractorException("Expected object reference after /Info");
                    }

                    meta.Info = (long)value.Value;

                    return true;
                case "XRefStm":
                    // Prefer encoded xref stream over xref table
                    if (value.Type != PdfLexer.TokenType.Int)
                    {
                        throw new PdfMetadataExtractorException("Expected offset after /XRefStm");
                    }

                    xrefStm = (long)value.Value;

                    return true;

                case "Encrypt":
                    throw new PdfMetadataExtractorException("Encryption not supported");

                default:
                    return false;
            }
        });

        PushMetadataRef(meta);

        if (xrefStm != -1)
        {
            ReadXRefAndTrailer(xrefStm);
        }

        if (prev != -1)
        {
            ReadXRefAndTrailer(prev);
        }
    }

    private void ReadMetadata(string filename)
    {
        // We read potential metadata sources in backwards historical order, so
        // we can overwrite to our heart's content

        while (_metadataRef.Count > 0)
        {
            var meta = _metadataRef.Pop();

            //_logger.LogTrace("DocumentCatalog for {Path}: {Root}, Info: {Info}", filename, meta.root, meta.info);

            ReadMetadataFromInfo(meta.Info);
            ReadMetadataFromXml(MetadataObjInObjectCatalog(meta.Root));
        }
    }

    private void ReadMetadataFromInfo(long infoObj)
    {
        // Document information dictionary (PDF Spec 14.3.3)
        // We treat this as less authoritative than the Metadata stream.

        if (infoObj < 1 || infoObj >= _objectOffsets.Length || _objectOffsets[infoObj] == 0)
        {
            return;
        }

        _stream.Seek(_objectOffsets[infoObj], SeekOrigin.Begin);
        _lexer.ResetBuffer();

        var token = _lexer.NextToken();

        if (token.Type != PdfLexer.TokenType.ObjectStart)
        {
            throw new PdfMetadataExtractorException("Expected object header");
        }

        Dictionary<string, long> indirectObjects = [];

        ParseDictionary(delegate(string key, PdfLexer.Token value)
        {
            switch (key)
            {
                case "Title":
                case "Author":
                case "Subject":
                case "Keywords":
                case "Creator":
                case "Producer":
                case "CreationDate":
                case "ModDate":
                    if (value.Type == PdfLexer.TokenType.ObjectRef) {
                        indirectObjects[key] = (long)value.Value;
                    }
                    else if (value.Type != PdfLexer.TokenType.String)
                    {
                        throw new PdfMetadataExtractorException("Expected string value");
                    }
                    else
                    {
                        _metadata[key] = (string)value.Value;
                    }

                    return true;

                default:
                    return false;
            }
        });

        // Resolve indirectly referenced values
        foreach(var key in indirectObjects.Keys) {
            _stream.Seek(_objectOffsets[indirectObjects[key]], SeekOrigin.Begin);
            _lexer.ResetBuffer();

            token = _lexer.NextToken();

            if (token.Type != PdfLexer.TokenType.ObjectStart) {
                throw new PdfMetadataExtractorException("Expected object here");
            }

            token = _lexer.NextToken();

            if (token.Type != PdfLexer.TokenType.String) {
                throw new PdfMetadataExtractorException("Expected string");
            }

            _metadata[key] = (string) token.Value;
        }
    }

    private long MetadataObjInObjectCatalog(long rootObj)
    {
        // Look for /Metadata entry in document catalog (PDF Spec 7.7.2)

        if (rootObj < 1 || rootObj >= _objectOffsets.Length || _objectOffsets[rootObj] == 0)
        {
            return -1;
        }

        _stream.Seek(_objectOffsets[rootObj], SeekOrigin.Begin);
        _lexer.ResetBuffer();

        var token = _lexer.NextToken();

        if (token.Type != PdfLexer.TokenType.ObjectStart)
        {
            throw new PdfMetadataExtractorException("Expected object header");
        }

        long meta = -1;

        ParseDictionary(delegate(string key, PdfLexer.Token value)
        {
            switch (key) {
                case "Metadata":
                    if (value.Type != PdfLexer.TokenType.ObjectRef)
                    {
                        throw new PdfMetadataExtractorException("Expected object number after /Metadata");
                    }

                    meta = (long)value.Value;

                    return true;

                default:
                    return false;
            }
        });

        return meta;
    }

    // Obtain metadata from XMP stream object
    // See XMP specification: https://developer.adobe.com/xmp/docs/XMPSpecifications/
    // and Dublin Core: https://www.dublincore.org/specifications/dublin-core/

    private static string? GetTextFromXmlNode(XmlDocument doc, XmlNamespaceManager ns, string path)
    {
        return (doc.DocumentElement?.SelectSingleNode(path + "//rdf:li", ns)
            ?? doc.DocumentElement?.SelectSingleNode(path, ns))?.InnerText;
    }

    private static string? GetListFromXmlNode(XmlDocument doc, XmlNamespaceManager ns, string path)
    {
        var nodes = doc.DocumentElement?.SelectNodes(path + "//rdf:li", ns);

        if (nodes == null) return null;

        var list = new StringBuilder();

        foreach (XmlNode n in nodes)
        {
            if (list.Length > 0)
            {
                list.Append(',');
            }

            list.Append(n.InnerText);
        }

        return list.Length > 0 ? list.ToString() : null;
    }

    private void SetMetadata(string key, string? value)
    {
        if (value == null) return;

        _metadata[key] = value;
    }

    private void ReadMetadataFromXml(long meta)
    {
        if (meta < 1 || meta >= _objectOffsets.Length || _objectOffsets[meta] == 0) return;

        _stream.Seek(_objectOffsets[meta], SeekOrigin.Begin);
        _lexer.ResetBuffer();

        var token = _lexer.NextToken();

        if (token.Type != PdfLexer.TokenType.ObjectStart)
        {
            throw new PdfMetadataExtractorException("Expected object header");
        }

        long length = -1;
        var deflate = false;

        // Metadata stream dictionary (PDF Spec 14.3.2)

        ParseDictionary(delegate(string key, PdfLexer.Token value)
        {
            switch (key) {
                case "Type":
                    if (value.Type != PdfLexer.TokenType.Name || (string)value.Value != "Metadata")
                    {
                        throw new PdfMetadataExtractorException("Expected /Type to be /Metadata");
                    }

                    return true;

                case "Subtype":
                    if (value.Type != PdfLexer.TokenType.Name || (string)value.Value != "XML")
                    {
                        throw new PdfMetadataExtractorException("Expected /Subtype to be /XML");
                    }

                    return true;

                case "Length":
                    if (value.Type != PdfLexer.TokenType.Int)
                    {
                        throw new PdfMetadataExtractorException("Expected integer after /Length");
                    }

                    length = (long)value.Value;

                    return true;

                case "Filter":
                    if (value.Type != PdfLexer.TokenType.Name)
                    {
                        throw new PdfMetadataExtractorException("Expected name after /Filter");
                    }

                    if ((string)value.Value != "FlateDecode")
                    {
                        throw new PdfMetadataExtractorException("Unsupported filter, only FlateDecode is supported");
                    }

                    deflate = true;

                    return true;

                default:
                    return false;
            }
        });

        token = _lexer.NextToken();

        if (token.Type != PdfLexer.TokenType.StreamStart)
        {
            throw new PdfMetadataExtractorException("Expected xref stream after dictionary");
        }

        var xmlStream = _lexer.StreamObject((int)length, deflate);

        // Skip XMP header
        while (true) {
            var b = xmlStream.ReadByte();

            if (b < 0) {
                throw new PdfMetadataExtractorException("Reached EOF in XMP header");
            }

            if (b == '?') {
                while (b == '?') {
                    b = xmlStream.ReadByte();
                }

                if (b == '>') {
                    break;
                }
            }
        }

        var metaDoc = new XmlDocument();
        metaDoc.Load(xmlStream);

        var ns = new XmlNamespaceManager(metaDoc.NameTable);
        ns.AddNamespace("rdf", "http://www.w3.org/1999/02/22-rdf-syntax-ns#");
        ns.AddNamespace("dc", "http://purl.org/dc/elements/1.1/");
        ns.AddNamespace("calibreSI", "http://calibre-ebook.com/xmp-namespace-series-index");
        ns.AddNamespace("calibre", "http://calibre-ebook.com/xmp-namespace");
        ns.AddNamespace("pdfx", "http://ns.adobe.com/pdfx/1.3/");
        ns.AddNamespace("prism", "http://prismstandard.org/namespaces/basic/2.0/");
        ns.AddNamespace("xmp", "http://ns.adobe.com/xap/1.0/");

        SetMetadata("CreationDate",
            GetTextFromXmlNode(metaDoc, ns, "//dc:date")
         ?? GetTextFromXmlNode(metaDoc, ns, "//xmp:CreateDate"));
        SetMetadata("Summary", GetTextFromXmlNode(metaDoc, ns, "//dc:description"));
        SetMetadata("Publisher", GetTextFromXmlNode(metaDoc, ns, "//dc:publisher"));
        SetMetadata("Author", GetListFromXmlNode(metaDoc, ns, "//dc:creator"));
        SetMetadata("Title", GetTextFromXmlNode(metaDoc, ns, "//dc:title"));
        SetMetadata("Subject", GetListFromXmlNode(metaDoc, ns, "//dc:subject"));
        SetMetadata("Language", GetTextFromXmlNode(metaDoc, ns, "//dc:language"));
        SetMetadata("ISBN", GetTextFromXmlNode(metaDoc, ns, "//pdfx:isbn") ?? GetTextFromXmlNode(metaDoc, ns, "//prism:isbn"));
        SetMetadata("UserRating", GetTextFromXmlNode(metaDoc, ns, "//calibre:rating"));
        SetMetadata("TitleSort", GetTextFromXmlNode(metaDoc, ns, "//calibre:title_sort"));
        SetMetadata("Series", GetTextFromXmlNode(metaDoc, ns, "//calibre:series/rdf:value"));
        SetMetadata("Volume", GetTextFromXmlNode(metaDoc, ns, "//calibreSI:series_index"));
    }

    private delegate bool DictionaryHandler(string key, PdfLexer.Token value);

    private void ParseDictionary(DictionaryHandler handler)
    {
        var token = _lexer.NextToken();

        if (token.Type != PdfLexer.TokenType.DictionaryStart)
        {
            throw new PdfMetadataExtractorException("Expected dictionary");
        }

        while (true)
        {
            token = _lexer.NextToken();

            if (token.Type == PdfLexer.TokenType.DictionaryEnd)
            {
                return;
            }

            if (token.Type == PdfLexer.TokenType.Name)
            {
                var value = _lexer.NextToken();

                if (!handler((string)token.Value, value)) {
                    SkipValue(value);
                }
            }
            else
            {
                throw new PdfMetadataExtractorException("Improper token in dictionary");
            }
        }
    }

    private void SkipValue(PdfLexer.Token? existingToken = null)
    {
        var token = existingToken ?? _lexer.NextToken();

        switch (token.Type)
        {
            case PdfLexer.TokenType.Bool:
            case PdfLexer.TokenType.Int:
            case PdfLexer.TokenType.Double:
            case PdfLexer.TokenType.Name:
            case PdfLexer.TokenType.String:
            case PdfLexer.TokenType.ObjectRef:
                break;
            case PdfLexer.TokenType.ArrayStart:
            {
                SkipArray();
                break;
            }
            case PdfLexer.TokenType.DictionaryStart:
            {
                SkipDictionary();
                break;
            }
            default:
                throw new PdfMetadataExtractorException("Unexpected token in SkipValue");
        }
    }

    private void SkipArray()
    {
        while (true)
        {
            var token = _lexer.NextToken();

            if (token.Type == PdfLexer.TokenType.ArrayEnd)
            {
                break;
            }

            SkipValue(token);
        }
    }

    private void SkipDictionary()
    {
        while (true)
        {
            var token = _lexer.NextToken();

            if (token.Type == PdfLexer.TokenType.DictionaryEnd)
            {
                break;
            }
            if (token.Type != PdfLexer.TokenType.Name)
            {
                throw new PdfMetadataExtractorException("Expected name in dictionary");
            }

            SkipValue();
        }
    }
}
