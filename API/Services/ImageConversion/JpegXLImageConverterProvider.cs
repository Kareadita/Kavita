using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Kavita.Common.EnvironmentInfo;

namespace API.Services.ImageConversion;

public interface IImageConverterProvider
{
    string Extension { get; }
    bool IsSupported(string filename);
    string Convert(string filename);
    (int Width, int Height)? GetDimensions(string fileName, int pageNumber);
}

public class JpegXLImageConverterProvider : IImageConverterProvider
{
    private bool? _appFound = null;

    internal bool AppFound
    {
        get
        {
            if (_appFound == null)
            {
                try
                {
                    _appFound = OsInfo.RunAndCapture(exeFile, "--version", Timeout.Infinite).Contains("JPEG XL");
                }
                catch (Exception e)
                {
                    //Eat it
                    _appFound = false;
                }
            }
            return _appFound.Value;
        }
    }
    private string exeFile => OsInfo.IsWindows ? "djxl.exe" : "djxl";

    private string infoFile => OsInfo.IsWindows ? "jxlinfo.exe" : "jxlinfo";

    public bool IsSupported(string filename)
    {
        if (AppFound)
            return filename.EndsWith(".jxl", StringComparison.InvariantCultureIgnoreCase);
        return false;
    }

    private static Regex dimensions = new Regex(@",\s?(\d+)x(\d+),", RegexOptions.Compiled);
    public (int Width, int Height)? GetDimensions(string fileName, int pageNumber)
    {
        if (!AppFound)
            return null;
        string output = OsInfo.RunAndCapture(infoFile, "\"" + fileName + "\"", Timeout.Infinite);
        Match m= dimensions.Match(output);
        if (!m.Success)
            return null;
        return (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value));
    }

    public string Extension => ".jxl";
    public string Convert(string filename)
    {
        if (!AppFound)
            return filename;
        string destination = Path.ChangeExtension(filename, "jpg");
        OsInfo.RunAndCapture(exeFile, "\"" + filename + "\" \"" + destination + "\"", Timeout.Infinite);
        File.Delete(filename);
        return destination;
    }
}
