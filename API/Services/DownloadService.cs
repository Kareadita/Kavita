﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.StaticFiles;

namespace API.Services;

public interface IDownloadService
{
    Tuple<string, string, string> GetFirstFileDownload(IEnumerable<MangaFile> files);
    string GetContentTypeFromFile(string filepath);
    Task<bool> HasDownloadPermission(AppUser user);
}
public class DownloadService : IDownloadService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly FileExtensionContentTypeProvider _fileTypeProvider = new FileExtensionContentTypeProvider();

    public DownloadService(UserManager<AppUser> userManager)
    {
        _userManager = userManager;
    }

    /// <summary>
    /// Downloads the first file in the file enumerable for download
    /// </summary>
    /// <param name="files"></param>
    /// <returns></returns>
    public Tuple<string, string, string> GetFirstFileDownload(IEnumerable<MangaFile> files)
    {
        var firstFile = files.Select(c => c.FilePath).First();
        return Tuple.Create(firstFile, GetContentTypeFromFile(firstFile), Path.GetFileName(firstFile));
    }

    public string GetContentTypeFromFile(string filepath)
    {
        // Figures out what the content type should be based on the file name.
        if (!_fileTypeProvider.TryGetContentType(filepath, out var contentType))
        {
            contentType = Path.GetExtension(filepath).ToLowerInvariant() switch
            {
                ".cbz" => "application/x-cbz",
                ".cbr" => "application/x-cbr",
                ".cb7" => "application/x-cb7",
                ".cbt" => "application/x-cbt",
                ".epub" => "application/epub+zip",
                ".7z" => "application/x-7z-compressed",
                ".7zip" => "application/x-7z-compressed",
                ".rar" => "application/vnd.rar",
                ".zip" => "application/zip",
                ".tar.gz" => "application/gzip",
                ".pdf" => "application/pdf",
                _ => contentType
            };
        }

        return contentType;
    }

    public async Task<bool> HasDownloadPermission(AppUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        return roles.Contains(PolicyConstants.DownloadRole) || roles.Contains(PolicyConstants.AdminRole);
    }
}
