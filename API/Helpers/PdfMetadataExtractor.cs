using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Xml;
using System.IO;
using Microsoft.Extensions.Logging;
using API.Services;

namespace API.Helpers;
#nullable enable

//
// Parse PDF file and try to extract as much metadata as possible.
// Supports both text based XRef tables and compressed XRef streams (Deflate only).
// Supports both UTF-16 and PDFDocEncoding for strings.
// Lacks support for many PDF configurations that are theoretically possible, but should handle most common cases.
//
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

    private readonly char[] _pdfDocMappingLow = new char[] {
        '\u02D8', '\u02C7', '\u02C6', '\u02D9', '\u02DD', '\u02DB', '\u02DA', '\u02DC',
    };

    private readonly char[] _pdfDocMappingHigh = new char[] {
        '\u2022', '\u2020', '\u2021', '\u2026', '\u2014', '\u2013', '\u0192', '\u2044',
        '\u2039', '\u203A', '\u2212', '\u2030', '\u201E', '\u201C', '\u201D', '\u2018',
        '\u2019', '\u201A', '\u2122', '\uFB01', '\uFB02', '\u0141', '\u0152', '\u0160',
        '\u0178', '\u017D', '\u0131', '\u0142', '\u0153', '\u0161', '\u017E', ' ',
        '\u20AC',
    };

    public void AppendPdfDocByte(byte b)
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
        if (_builder.Length == 0 && !_isUnicode)
        {
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

class PdfLexer(Stream stream)
{
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

    public struct Token
    {
        public TokenType type;
        public object value;

        public Token(TokenType type, object value)
        {
            this.type = type;
            this.value = value;
        }
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
                    throw new Exception("Unexpected byte, got {LastByte()}");
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
            byte b = NextByte();
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
                    throw new Exception("Unexpected character, expected newline, got {b}");
            }
        }
    }

    public long GetXRefStart()
    {
        while (true)
        {
            byte b = NextByte();

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

                    if (token.type == TokenType.Keyword && (string)token.value == "startxref")
                    {
                        token = NextToken();

                        if (token.type == TokenType.Int)
                        {
                            return (long)token.value;
                        }
                        else
                        {
                            throw new Exception("Expected integer after startxref keyword");
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
        WantLookahead(20);

        if (_valid - _pos < 20)
        {
            throw new Exception("End of stream");
        }

        var inUse = true;

        if (obj == 0)
        {
            obj = Convert.ToInt64(System.Text.Encoding.ASCII.GetString(_buffer, _pos, 10));
            generation = Convert.ToInt32(System.Text.Encoding.ASCII.GetString(_buffer, _pos + 11, 5));
            inUse = _buffer[_pos + 17] == 'n';
        }

        _pos += 20;

        return inUse;
    }

    public Stream StreamObject(int length, bool deflate)
    {
        var rawData = new MemoryStream();

        ExpectNewline();

        if (_pos < _valid)
        {
            int buffered = Math.Min(_valid - _pos, length);
            rawData.Write(_buffer, _pos, buffered);
            length -= buffered;
            _pos += buffered;
        }

        while (length > 0)
        {
            int buffered = Math.Min(length, _bufferSize);
            stream.Read(_buffer, 0, buffered);
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

    private const int _bufferSize = 1024;
    private readonly byte[] _buffer = new byte[_bufferSize];
    private int _pos = 0;
    private int _valid = 0;

    private byte NextByte()
    {
        if (_pos >= _valid)
        {
            _pos = 0;
            _valid = stream.Read(_buffer, 0, _bufferSize);

            if (_valid <= 0)
            {
                throw new Exception("End of stream");
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
            throw new Exception($"Unexpected character, expected {expected}");
        }
    }

    private void WantLookahead(int length)
    {
        if (_pos + length > _valid)
        {
            Buffer.BlockCopy(_buffer, _pos, _buffer, 0, _valid - _pos);
            _valid -= _pos;
            _pos = 0;
            _valid += stream.Read(_buffer, _valid, _bufferSize - _valid);
        }
    }

    private void SkipComment()
    {
        while (true)
        {
            byte b = NextByte();

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
        bool hasDot = LastByte() == '.';
        bool followedBySpace = false;

        sb.Append((char)LastByte());

        while (true)
        {
            byte b = NextByte();

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
            // Look ahead to see if it's an object reference
            WantLookahead(32);

            var savedPos = _pos;
            byte b = NextByte();

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

    private int HexDigit(byte b)
    {
        switch ((char)b)
        {
            case >= '0' and <= '9':
                return b - (byte)'0';

            case >= 'a' and <= 'f':
                return b - (byte)'a' + 10;

            case >= 'A' and <= 'F':
                return b - (byte)'A' + 10;

            default:
                throw new Exception("Invalid hex digit, got {b}");
        }
    }

    private Token ScanName()
    {
        StringBuilder sb = new StringBuilder();
        while (true)
        {
            byte b = NextByte();
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
                    byte b1 = NextByte();
                    byte b2 = NextByte();
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
        PdfStringBuilder sb = new();
        int parenLevel = 1;

        while (true)
        {
            byte b = NextByte();

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
                            byte b1 = b;
                            byte b2 = NextByte();
                            byte b3 = NextByte();

                            if (b2 < '0' || b2 > '7' || b3 < '0' || b3 > '7')
                            {
                                throw new Exception("Invalid octal escape, got {b1}{b2}{b3}");
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
        PdfStringBuilder sb = new();

        while (true)
        {
            byte b = NextByte();

            switch ((char)b)
            {
                case (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F'):
                    byte b1 = NextByte();
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
                    throw new Exception("Invalid hex string, got {b}");
            }
        }
    }

    private Token ScanKeyword()
    {
        StringBuilder sb = new();

        sb.Append((char)LastByte());

        while (true)
        {
            byte b = NextByte();
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

class PdfMetadataExtractor : IPdfMetadataExtractor
{
    public PdfMetadataExtractor(ILogger<BookService> logger, string filename)
    {
        Stopwatch timing = Stopwatch.StartNew();
        _logger = logger;
        _stream = File.OpenRead(filename);
        _lexer = new PdfLexer(_stream);

        ReadObjectOffsets();
        long objOffsetTime = timing.ElapsedMilliseconds;

        ReadMetadata(filename);
        long metadataTime = timing.ElapsedMilliseconds - objOffsetTime;

        LogMetadata(filename);
        timing.Stop();

        _logger.LogInformation("PDF {File}, object offsets {ObjOffsetTime} ms, metadata {MetadataTime} ms", filename, objOffsetTime, metadataTime);
    }

    public Dictionary<string, string> GetMetadata()
    {
        return _metadata;
    }

    private readonly ILogger<BookService> _logger;
    private readonly PdfLexer _lexer;
    private readonly FileStream _stream;
    private long[] _objectOffsets = new long[0];
    private readonly Dictionary<string, string> _metadata = new();

    private struct MetadataRef
    {
        public long root;
        public long info;

        public MetadataRef(long root, long info)
        {
            this.root = root;
            this.info = info;
        }
    }

    private readonly Stack<MetadataRef> metadataRef = new();

    private void LogMetadata(string filename)
    {
        _logger.LogDebug("Metadata for {Path}:", filename);

        foreach (var entry in _metadata)
        {
            _logger.LogDebug("   {Key:0,-5} : {Value:1}", entry.Key, entry.Value);
        }
    }

    private void ReadObjectOffsets()
    {
        _stream.Seek(-32, SeekOrigin.End);

        long xrefOffset = _lexer.GetXRefStart();

        ReadXRefAndTrailer(xrefOffset);
    }

    private void ReadXRefAndTrailer(long xrefOffset)
    {
        _stream.Seek(xrefOffset, SeekOrigin.Begin);
        _lexer.ResetBuffer();

        if (!_lexer.TestByte((byte)'x'))
        {
            ReadXRefStream();

            return;
        }

        var token = _lexer.NextToken();

        if (token.type != PdfLexer.TokenType.Keyword || (string)token.value != "xref")
        {
            throw new Exception("Expected xref keyword");
        }

        // Read xref entries
        while (true)
        {
            token = _lexer.NextToken();

            if (token.type == PdfLexer.TokenType.Int)
            {
                long startObj = (long)token.value;
                token = _lexer.NextToken();

                if (token.type != PdfLexer.TokenType.Int)
                {
                    throw new Exception("Expected number of objects in xref subsection");
                }

                long numObj = (long)token.value;

                if (_objectOffsets.Length < startObj + numObj)
                {
                    Array.Resize(ref _objectOffsets, (int)(startObj + numObj));
                }

                _lexer.ExpectNewline();

                int generation = 0;

                for (var obj = startObj; obj < startObj + numObj; ++obj)
                {
                    bool inUse = _lexer.NextXRefEntry(ref _objectOffsets[obj], ref generation);

                    if (!inUse)
                    {
                        _objectOffsets[obj] = 0;
                    }
                }
            }
            else if (token.type == PdfLexer.TokenType.Keyword && (string)token.value == "trailer")
            {
                break;
            }
            else
            {
                throw new Exception("Unexpected token in xref");
            }
        }

        ReadTrailerDictionary();
    }

    private struct XRefSection
    {
        public long first;
        public long count;

        public XRefSection(long first, long count)
        {
            this.first = first;
            this.count = count;
        }
    }

    private void ReadXRefStream()
    {
        var token = _lexer.NextToken();

        if (token.type != PdfLexer.TokenType.ObjectStart)
        {
            throw new Exception("Expected obj keyword");
        }

        token = _lexer.NextToken();

        if (token.type != PdfLexer.TokenType.DictionaryStart)
        {
            throw new Exception("Expected dictionary");
        }

        long length = -1;
        long size = -1;
        bool deflate = false;
        long prev = -1;
        long typeWidth = -1;
        long offsetWidth = -1;
        long generationWidth = -1;
        Queue<XRefSection> sections = new();
        MetadataRef meta = new MetadataRef(-1, -1);

        while (true)
        {
            token = _lexer.NextToken();

            if (token.type == PdfLexer.TokenType.DictionaryEnd)
            {
                break;
            }
            else if (token.type == PdfLexer.TokenType.Name)
            {
                switch ((string)token.value)
                {
                    case "Type":
                        token = _lexer.NextToken();

                        if (token.type != PdfLexer.TokenType.Name || (string)token.value != "XRef")
                        {
                            throw new Exception("Expected /Type to be /XRef");
                        }

                        break;

                    case "Length":
                        token = _lexer.NextToken();

                        if (token.type != PdfLexer.TokenType.Int)
                        {
                            throw new Exception("Expected integer after /Length");
                        }

                        length = (long)token.value;

                        break;

                    case "Size":
                        token = _lexer.NextToken();

                        if (token.type != PdfLexer.TokenType.Int)
                        {
                            throw new Exception("Expected integer after /Size");
                        }

                        size = (long)token.value;

                        break;

                    case "Prev":
                        token = _lexer.NextToken();

                        if (token.type != PdfLexer.TokenType.Int)
                        {
                            throw new Exception("Expected offset after /Prev");
                        }

                        prev = (long)token.value;

                        break;

                    case "Index":
                        token = _lexer.NextToken();

                        if (token.type != PdfLexer.TokenType.ArrayStart)
                        {
                            throw new Exception("Expected array after /Index");
                        }

                        while (true)
                        {
                            token = _lexer.NextToken();

                            if (token.type == PdfLexer.TokenType.ArrayEnd)
                            {
                                break;
                            }
                            else if (token.type != PdfLexer.TokenType.Int)
                            {
                                throw new Exception("Expected integer in /Index array");
                            }

                            long first = (long)token.value;
                            token = _lexer.NextToken();

                            if (token.type != PdfLexer.TokenType.Int)
                            {
                                throw new Exception("Expected integer pair in /Index array");
                            }

                            long count = (long)token.value;
                            sections.Enqueue(new XRefSection(first, count));
                        }

                        break;

                    case "W":
                        token = _lexer.NextToken();

                        if (token.type != PdfLexer.TokenType.ArrayStart)
                        {
                            throw new Exception("Expected array after /W");
                        }

                        long[] widths = new long[3];

                        for (int i = 0; i < 3; ++i)
                        {
                            token = _lexer.NextToken();

                            if (token.type != PdfLexer.TokenType.Int)
                            {
                                throw new Exception("Expected integer in /W array");
                            }

                            widths[i] = (long)token.value;
                        }

                        token = _lexer.NextToken();

                        if (token.type != PdfLexer.TokenType.ArrayEnd)
                        {
                            throw new Exception("Unclosed array after /W");
                        }

                        typeWidth = widths[0];
                        offsetWidth = widths[1];
                        generationWidth = widths[2];

                        break;

                    case "Filter":
                        token = _lexer.NextToken();

                        if (token.type != PdfLexer.TokenType.Name)
                        {
                            throw new Exception("Expected name after /Filter");
                        }

                        if ((string)token.value != "FlateDecode")
                        {
                            throw new Exception("Unsupported filter, only FlateDecode is supported");
                        }

                        deflate = true;

                        break;

                    case "Root":
                        token = _lexer.NextToken();

                        if (token.type != PdfLexer.TokenType.ObjectRef)
                        {
                            throw new Exception("Expected object reference after /Root");
                        }

                        meta.root = (long)token.value;

                        break;

                    case "Info":
                        token = _lexer.NextToken();

                        if (token.type != PdfLexer.TokenType.ObjectRef)
                        {
                            throw new Exception("Expected object reference after /Info");
                        }

                        meta.info = (long)token.value;

                        break;

                    default:
                        SkipValue();

                        break;
                }
            }
            else
            {
                throw new Exception("Unexpected token in xref stream dictionary");
            }
        }

        token = _lexer.NextToken();

        if (token.type != PdfLexer.TokenType.StreamStart)
        {
            throw new Exception("Expected xref stream after dictionary");
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

            for (long i = section.first; i < section.first + section.count; ++i)
            {
                long type = 0;
                long offset = 0;
                long generation = 0;

                if (typeWidth == 0)
                {
                    type = 1;
                }

                for (int j = 0; j < typeWidth; ++j)
                {
                    type = (type << 8) | (UInt16)stream.ReadByte();
                }

                for (int j = 0; j < offsetWidth; ++j)
                {
                    offset = (offset << 8) | (UInt16)stream.ReadByte();
                }

                for (int j = 0; j < generationWidth; ++j)
                {
                    generation = (generation << 8) | (UInt16)stream.ReadByte();
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
        if (metadataRef.Count > 0)
        {
            if (meta.root == metadataRef.Peek().root)
            {
                meta.root = -1;
            }

            if (meta.info == metadataRef.Peek().info)
            {
                meta.info = -1;
            }
        }

        if (meta.root != -1 || meta.info != -1)
        {
            metadataRef.Push(meta);
        }
    }

    private void ReadTrailerDictionary()
    {
        // Read trailer directory
        long prev = -1;
        long xrefStm = -1;

        MetadataRef meta = new(-1, -1);
        var token = _lexer.NextToken();

        if (token.type != PdfLexer.TokenType.DictionaryStart)
        {
            throw new Exception("Expected trailer dictionary");
        }

        while (true)
        {
            token = _lexer.NextToken();

            if (token.type == PdfLexer.TokenType.DictionaryEnd)
            {
                PushMetadataRef(meta);

                if (xrefStm != -1)
                {
                    ReadXRefAndTrailer(xrefStm);
                }

                if (prev != -1)
                {
                    ReadXRefAndTrailer(prev);
                }

                break;
            }
            else if (token.type == PdfLexer.TokenType.Name)
            {
                switch ((string)token.value)
                {
                    case "Root":
                        token = _lexer.NextToken();

                        if (token.type != PdfLexer.TokenType.ObjectRef)
                        {
                            throw new Exception("Expected object reference after /Root");
                        }

                        meta.root = (long)token.value;

                        break;
                    case "Prev":
                        token = _lexer.NextToken();

                        if (token.type != PdfLexer.TokenType.Int)
                        {
                            throw new Exception("Expected offset after /Prev");
                        }

                        prev = (long)token.value;

                        break;
                    case "Info":
                        token = _lexer.NextToken();

                        if (token.type != PdfLexer.TokenType.ObjectRef)
                        {
                            throw new Exception("Expected object reference after /Info");
                        }

                        meta.info = (long)token.value;

                        break;
                    case "XRefStm":
                        // Prefer encoded xref stream over xref table
                        token = _lexer.NextToken();

                        if (token.type != PdfLexer.TokenType.Int)
                        {
                            throw new Exception("Expected offset after /XRefStm");
                        }

                        xrefStm = (long)token.value;

                        break;

                    case "Encrypt":
                        throw new Exception("Encryption not supported");

                    default:
                        SkipValue();
                        break;
                }
            }
        }
    }

    private void ReadMetadata(string filename)
    {
        // We read potential metadata sources in backwards historical order, so
        // we can overwrite to our hearts content
        while (metadataRef.Count > 0)
        {
            var meta = metadataRef.Pop();

            _logger.LogDebug("DocumentCatalog for {Path}: {Root}, Info: {Info}", filename, meta.root, meta.info);

            ReadMetadataFromInfo(meta.info);
            ReadMetadataFromXML(MetadataObjInObjectCatalog(meta.root));
        }
    }

    private void ReadMetadataFromInfo(long infoObj)
    {
        if (infoObj < 1 || infoObj >= _objectOffsets.Length || _objectOffsets[infoObj] == 0)
        {
            return;
        }

        _stream.Seek(_objectOffsets[infoObj], SeekOrigin.Begin);
        _lexer.ResetBuffer();

        var token = _lexer.NextToken();

        if (token.type != PdfLexer.TokenType.ObjectStart)
        {
            throw new Exception("Expected object header");
        }

        token = _lexer.NextToken();

        if (token.type != PdfLexer.TokenType.DictionaryStart)
        {
            throw new Exception("Expected info dictionary");
        }

        Dictionary<String, long> indirectObjects = new();

        while (true)
        {
            token = _lexer.NextToken();

            if (token.type == PdfLexer.TokenType.DictionaryEnd)
            {
                break;
            }
            else if (token.type == PdfLexer.TokenType.Name)
            {
                switch ((string)token.value)
                {
                    case "Title":
                    case "Author":
                    case "Subject":
                    case "Keywords":
                    case "Creator":
                    case "Producer":
                    case "CreationDate":
                    case "ModDate":
                        var value = _lexer.NextToken();

                        if (value.type == PdfLexer.TokenType.ObjectRef) {
                            indirectObjects[(string)token.value] = (long)value.value;
                        }
                        else if (value.type != PdfLexer.TokenType.String)
                        {
                            throw new Exception("Expected string value");
                        }
                        else
                        {
                            _metadata[(string)token.value] = (string)value.value;
                        }

                        break;

                    default:
                        SkipValue();
                        break;
                }
            }
            else
            {
                throw new Exception("Unexpected token in info dictionary");
            }
        }

        // Resolve indirectly referenced values
        foreach(var key in indirectObjects.Keys) {
            _stream.Seek(_objectOffsets[indirectObjects[key]], SeekOrigin.Begin);
            _lexer.ResetBuffer();

            token = _lexer.NextToken();

            if (token.type != PdfLexer.TokenType.ObjectStart) {
                throw new Exception("Expected object here");
            }

            token = _lexer.NextToken();

            if (token.type != PdfLexer.TokenType.String) {
                throw new Exception("Expected string");
            }

            _metadata[key] = (string)token.value;
        }
    }

    private long MetadataObjInObjectCatalog(long rootObj)
    {
        if (rootObj < 1 || rootObj >= _objectOffsets.Length || _objectOffsets[rootObj] == 0)
        {
            return -1;
        }

        _stream.Seek(_objectOffsets[rootObj], SeekOrigin.Begin);
        _lexer.ResetBuffer();

        var token = _lexer.NextToken();

        if (token.type != PdfLexer.TokenType.ObjectStart)
        {
            throw new Exception("Expected object header");
        }

        token = _lexer.NextToken();

        if (token.type != PdfLexer.TokenType.DictionaryStart)
        {
            throw new Exception("Expected root dictionary");
        }

        while (true)
        {
            token = _lexer.NextToken();

            if (token.type == PdfLexer.TokenType.DictionaryEnd)
            {
                break;
            }
            else if (token.type == PdfLexer.TokenType.Name)
            {
                switch ((string)token.value)
                {
                    case "Metadata":
                        token = _lexer.NextToken();

                        if (token.type != PdfLexer.TokenType.ObjectRef)
                        {
                            throw new Exception("Expected object number after /Metadata");
                        }

                        return (long)token.value;

                    default:
                        SkipValue();

                        break;
                }
            }
            else
            {
                throw new Exception("Unexpected token in document catalog");
            }
        }
        return -1;
    }

    private string? GetTextFromXmlNode(XmlDocument doc, XmlNamespaceManager ns, string path)
    {
        return (doc.DocumentElement?.SelectSingleNode(path + "//rdf:li", ns)
            ?? doc.DocumentElement?.SelectSingleNode(path, ns))?.InnerText;
    }

    private string? GetListFromXmlNode(XmlDocument doc, XmlNamespaceManager ns, string path)
    {
        var nodes = doc.DocumentElement?.SelectNodes(path + "//rdf:li", ns);

        if (nodes == null) return null;

        var list = new StringBuilder();

        foreach (XmlNode n in nodes)
        {
            if (list.Length > 0)
            {
                list.Append(",");
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

    private void ReadMetadataFromXML(long meta)
    {
        if (meta < 1 || meta >= _objectOffsets.Length || _objectOffsets[meta] == 0) return;

        _stream.Seek(_objectOffsets[meta], SeekOrigin.Begin);
        _lexer.ResetBuffer();

        var token = _lexer.NextToken();

        if (token.type != PdfLexer.TokenType.ObjectStart)
        {
            throw new Exception("Expected obj keyword");
        }

        token = _lexer.NextToken();

        if (token.type != PdfLexer.TokenType.DictionaryStart)
        {
            throw new Exception("Expected dictionary");
        }

        long length = -1;
        bool deflate = false;

        while (true)
        {
            token = _lexer.NextToken();

            if (token.type == PdfLexer.TokenType.DictionaryEnd)
            {
                break;
            }
            else if (token.type == PdfLexer.TokenType.Name)
            {
                switch ((string)token.value)
                {
                    case "Type":
                        token = _lexer.NextToken();

                        if (token.type != PdfLexer.TokenType.Name || (string)token.value != "Metadata")
                        {
                            throw new Exception("Expected /Type to be /Metadata");
                        }

                        break;

                    case "Subtype":
                        token = _lexer.NextToken();

                        if (token.type != PdfLexer.TokenType.Name || (string)token.value != "XML")
                        {
                            throw new Exception("Expected /Subtype to be /XML");
                        }

                        break;

                    case "Length":
                        token = _lexer.NextToken();

                        if (token.type != PdfLexer.TokenType.Int)
                        {
                            throw new Exception("Expected integer after /Length");
                        }

                        length = (long)token.value;

                        break;

                    case "Filter":
                        token = _lexer.NextToken();

                        if (token.type != PdfLexer.TokenType.Name)
                        {
                            throw new Exception("Expected name after /Filter");
                        }

                        if ((string)token.value != "FlateDecode")
                        {
                            throw new Exception("Unsupported filter, only FlateDecode is supported");
                        }

                        deflate = true;

                        break;

                    default:
                        SkipValue();

                        break;
                }
            }
            else
            {
                throw new Exception("Unexpected token in xref stream dictionary");
            }
        }

        token = _lexer.NextToken();

        if (token.type != PdfLexer.TokenType.StreamStart)
        {
            throw new Exception("Expected xref stream after dictionary");
        }

        var xmlStream = _lexer.StreamObject((int)length, deflate);

        // Skip XMP header
        while (true) {
            var b = xmlStream.ReadByte();

            if (b < 0) {
                throw new Exception("Reached EOF in XMP header");
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

    private void SkipValue(PdfLexer.Token? existingToken = null)
    {
        var token = existingToken ?? _lexer.NextToken();

        switch (token.type)
        {
            case PdfLexer.TokenType.Bool:
            case PdfLexer.TokenType.Int:
            case PdfLexer.TokenType.Double:
            case PdfLexer.TokenType.Name:
            case PdfLexer.TokenType.String:
            case PdfLexer.TokenType.ObjectRef:
                break;

            case PdfLexer.TokenType.ArrayStart:
                SkipArray();

                break;

            case PdfLexer.TokenType.DictionaryStart:
                SkipDictionary();

                break;

            default:
                throw new Exception("Unexpected token in SkipValue");
        }
    }

    private void SkipArray()
    {
        while (true)
        {
            var token = _lexer.NextToken();

            if (token.type == PdfLexer.TokenType.ArrayEnd)
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

            if (token.type == PdfLexer.TokenType.DictionaryEnd)
            {
                break;
            }
            else if (token.type != PdfLexer.TokenType.Name)
            {
                throw new Exception("Expected name in dictionary");
            }

            SkipValue();
        }
    }
}
