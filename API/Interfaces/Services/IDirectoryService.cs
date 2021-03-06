﻿using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using API.DTOs;

namespace API.Interfaces.Services
{
    public interface IDirectoryService
    {
        /// <summary>
        /// Lists out top-level folders for a given directory. Filters out System and Hidden folders.
        /// </summary>
        /// <param name="rootPath">Absolute path of directory to scan.</param>
        /// <returns>List of folder names</returns>
        IEnumerable<string> ListDirectory(string rootPath);
        /// <summary>
        /// Gets files in a directory. If searchPatternExpression is passed, will match the regex against for filtering.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="searchPatternExpression"></param>
        /// <returns></returns>
        string[] GetFilesWithExtension(string path, string searchPatternExpression = "");
        /// <summary>
        /// Returns true if the path exists and is a directory. If path does not exist, this will create it. Returns false in all fail cases.
        /// </summary>
        /// <param name="directoryPath"></param>
        /// <returns></returns>
        bool ExistOrCreate(string directoryPath);

        Task<byte[]> ReadFileAsync(string path);

        /// <summary>
        /// Deletes all files within the directory, then the directory itself.
        /// </summary>
        /// <param name="directoryPath"></param>
        void ClearAndDeleteDirectory(string directoryPath);
        /// <summary>
        /// Deletes all files within the directory.
        /// </summary>
        /// <param name="directoryPath"></param>
        /// <returns></returns>
        void ClearDirectory(string directoryPath);

        bool CopyFilesToDirectory(IEnumerable<string> filePaths, string directoryPath);

        IEnumerable<string> GetFiles(string path, string searchPatternExpression = "",
            SearchOption searchOption = SearchOption.TopDirectoryOnly);
    }
}