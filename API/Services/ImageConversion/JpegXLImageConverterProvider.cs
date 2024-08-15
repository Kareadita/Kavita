using System;
using System.IO;
using System.Threading;
using Kavita.Common.EnvironmentInfo;

namespace API.Services.ImageConversion;

public interface IImageConverterProvider
{
    string Extension { get; }
    bool IsSupported(string filename);
    string Convert(string filename);
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

    public bool IsSupported(string filename)
    {
        if (AppFound)
            return filename.EndsWith(".jxl", StringComparison.InvariantCultureIgnoreCase);
        return false;
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
