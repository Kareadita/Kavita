using System;
using System.Collections.Generic;

namespace API.DTOs.Theme;


public class DownloadableSiteThemeDto
{
    /// <summary>
    /// Theme Name
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// Url to download css file
    /// </summary>
    public string CssUrl { get; set; }
    public string CssFile { get; set; }
    /// <summary>
    /// Url to preview image
    /// </summary>
    public IList<string> PreviewUrls { get; set; }
    /// <summary>
    /// If Already downloaded
    /// </summary>
    public bool AlreadyDownloaded { get; set; }
    /// <summary>
    /// Sha of the file
    /// </summary>
    public string Sha { get; set; }
    /// <summary>
    /// Path of the Folder the files reside in
    /// </summary>
    public string Path { get; set; }
    /// <summary>
    /// Author of the theme
    /// </summary>
    /// <remarks>Derived from Readme</remarks>
    public string Author { get; set; }
    /// <summary>
    /// Last version tested against
    /// </summary>
    /// <remarks>Derived from Readme</remarks>
    public string LastCompatibleVersion { get; set; }
    /// <summary>
    /// If version compatible with version
    /// </summary>
    public bool IsCompatible { get; set; }
    /// <summary>
    /// Small blurb about the Theme
    /// </summary>
    public string Description { get; set; }
}
