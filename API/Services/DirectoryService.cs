using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using API.DTOs;
using API.Interfaces;
using Microsoft.Extensions.Logging;

namespace API.Services
{
    public class DirectoryService : IDirectoryService
    {
       private readonly ILogger<DirectoryService> _logger;

       public DirectoryService(ILogger<DirectoryService> logger)
       {
          _logger = logger;
       }

       /// <summary>
        /// Lists out top-level folders for a given directory. Filters out System and Hidden folders.
        /// </summary>
        /// <param name="rootPath">Absolute path </param>
        /// <returns>List of folder names</returns>
        public IEnumerable<string> ListDirectory(string rootPath)
        {
           if (!Directory.Exists(rootPath)) return ImmutableList<string>.Empty;
            
            var di = new DirectoryInfo(rootPath);
            var dirs = di.GetDirectories()
                .Where(dir => !(dir.Attributes.HasFlag(FileAttributes.Hidden) || dir.Attributes.HasFlag(FileAttributes.System)))
                .Select(d => d.Name).ToImmutableList();
            
            
            return dirs;
        }

        public void ScanLibrary(LibraryDto library)
        {
           foreach (var folderPath in library.Folders)
           {
              try {
                 TraverseTreeParallelForEach(folderPath, (f) =>
                 {
                    // Exceptions are no-ops.
                    try {
                       // Do nothing with the data except read it.
                       //byte[] data = File.ReadAllBytes(f);
                       ProcessManga(f);
                    }
                    catch (FileNotFoundException) {}
                    catch (IOException) {}
                    catch (UnauthorizedAccessException) {}
                    catch (SecurityException) {}
                    // Display the filename.
                    Console.WriteLine(f);
                 });
              }
              catch (ArgumentException) {
                 _logger.LogError($"The directory '{folderPath}' does not exist");
              }
           }
        }

        private static void ProcessManga(string filename)
        {
            Console.WriteLine($"Found {filename}");
        }
        
        public static void TraverseTreeParallelForEach(string root, Action<string> action)
         {
            //Count of files traversed and timer for diagnostic output
            int fileCount = 0;
            var sw = Stopwatch.StartNew();

            // Determine whether to parallelize file processing on each folder based on processor count.
            int procCount = Environment.ProcessorCount;

            // Data structure to hold names of subfolders to be examined for files.
            Stack<string> dirs = new Stack<string>();

            if (!Directory.Exists(root)) {
                   throw new ArgumentException();
            }
            dirs.Push(root);

            while (dirs.Count > 0) {
               string currentDir = dirs.Pop();
               string[] subDirs = {};
               string[] files = {};

               try {
                  subDirs = Directory.GetDirectories(currentDir);
               }
               // Thrown if we do not have discovery permission on the directory.
               catch (UnauthorizedAccessException e) {
                  Console.WriteLine(e.Message);
                  continue;
               }
               // Thrown if another process has deleted the directory after we retrieved its name.
               catch (DirectoryNotFoundException e) {
                  Console.WriteLine(e.Message);
                  continue;
               }

               try {
                  files = Directory.GetFiles(currentDir);
               }
               catch (UnauthorizedAccessException e) {
                  Console.WriteLine(e.Message);
                  continue;
               }
               catch (DirectoryNotFoundException e) {
                  Console.WriteLine(e.Message);
                  continue;
               }
               catch (IOException e) {
                  Console.WriteLine(e.Message);
                  continue;
               }

               // Execute in parallel if there are enough files in the directory.
               // Otherwise, execute sequentially.Files are opened and processed
               // synchronously but this could be modified to perform async I/O.
               try {
                  if (files.Length < procCount) {
                     foreach (var file in files) {
                        action(file);
                        fileCount++;
                     }
                  }
                  else {
                     Parallel.ForEach(files, () => 0, (file, loopState, localCount) =>
                                                  { action(file);
                                                    return ++localCount;
                                                  },
                                      (c) => {
                                                Interlocked.Add(ref fileCount, c);
                                      });
                  }
               }
               catch (AggregateException ae) {
                  ae.Handle((ex) => {
                               if (ex is UnauthorizedAccessException) {
                                  // Here we just output a message and go on.
                                  Console.WriteLine(ex.Message);
                                  return true;
                               }
                               // Handle other exceptions here if necessary...

                               return false;
                  });
               }

               // Push the subdirectories onto the stack for traversal.
               // This could also be done before handing the files.
               foreach (string str in subDirs)
                  dirs.Push(str);
            }

            // For diagnostic purposes.
            Console.WriteLine("Processed {0} files in {1} milliseconds", fileCount, sw.ElapsedMilliseconds);
         }
    }
}