using System;
using System.Collections.Generic;
using System.IO;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Parser;

namespace Kavita.Services.Scanner;

public static class ParseIssues
{
    public static ParseIssue FromException(string filePath, string comment, Exception ex)
    {
        return new ParseIssue(ReasonFor(filePath, ex), comment, Describe(ex));
    }

    /// <param name="info">What the parser returned with its failure, if anything</param>
    public static ParseIssue FromFailedParse(ParserInfo? info)
    {
        return info == null
            ? new ParseIssue(MediaErrorReason.ParseFailed, "Unable to parse any meaningful information out of file", string.Empty)
            : new ParseIssue(MediaErrorReason.NoSeriesName, "Failed to parse a valid series name for a file", $"{info.Filename} has no series name");
    }

    public static MediaErrorReason ReasonFor(string filePath, Exception ex)
    {
        return ex switch
        {
            UnauthorizedAccessException => MediaErrorReason.IoError,
            // A truncated archive or epub, not a disk problem
            EndOfStreamException => ReasonForFormat(filePath),
            IOException => MediaErrorReason.IoError,
            _ => ReasonForFormat(filePath),
        };
    }

    private static MediaErrorReason ReasonForFormat(string filePath)
    {
        if (Parser.IsEpub(filePath)) return MediaErrorReason.CorruptEpub;
        if (Parser.IsArchive(filePath)) return MediaErrorReason.UnreadableArchive;
        return MediaErrorReason.Unknown;
    }

    /// <summary>
    /// The message of the exception and each inner exception, as the outer one alone is often generic
    /// </summary>
    public static string Describe(Exception ex)
    {
        var messages = new List<string>();
        for (var current = ex; current != null; current = current.InnerException)
        {
            messages.Add(current.Message);
        }

        return string.Join(" -> ", messages);
    }
}
