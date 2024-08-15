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
    public bool IsSupported(string filename) => filename.EndsWith(".jxl", StringComparison.InvariantCultureIgnoreCase);
    public string Extension => ".jxl";
    public string Convert(string filename)
    {
        string exe = "djxl";
        if (OsInfo.IsWindows)
            exe = "djxl.exe";
        string destination = Path.ChangeExtension(filename, "jpg");
        OsInfo.RunAndCapture(exe, "\"" + filename + "\" \"" + destination + "\"", Timeout.Infinite);
        File.Delete(filename);
        return destination;
    }
}
