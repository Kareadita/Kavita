using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using API.Services;

namespace API.Extensions
{
    public static class DirectoryInfoExtensions
    {
        public static void Empty(this DirectoryInfo directory)
        {
            foreach(FileInfo file in directory.EnumerateFiles()) file.Delete();
            foreach(DirectoryInfo subDirectory in directory.EnumerateDirectories()) subDirectory.Delete(true);
        }

        /// <summary>
        /// Flattens all files in subfolders to the passed directory recursively.
        /// 
        /// 
        /// foo<para />
        /// ├── 1.txt<para />
        /// ├── 2.txt<para />
        /// ├── 3.txt<para />
        /// ├── 4.txt<para />
        /// └── bar<para />
        ///     ├── 1.txt<para />
        ///     ├── 2.txt<para />
        ///     └── 5.txt<para />
        /// 
        /// becomes:<para />
        /// foo<para />
        /// ├── 1.txt<para />
        /// ├── 2.txt<para />
        /// ├── 3.txt<para />
        /// ├── 4.txt<para />
        ///     ├── bar_1.txt<para />
        ///     ├── bar_2.txt<para />
        ///     └── bar_5.txt<para />
        /// </summary>
        /// <param name="directory"></param>
        public static void Flatten(this DirectoryInfo directory)
        {
            var index = 0;
            FlattenDirectory(directory, directory, ref index);
        }

        private static void FlattenDirectory(DirectoryInfo root, DirectoryInfo directory, ref int directoryIndex)
        {
            if (!root.FullName.Equals(directory.FullName))
            {
                var fileIndex = 1;

                // TODO: Maybe go back and implement natural sorting instead of alphanumeric.
                foreach (FileInfo file in directory.EnumerateFiles().OrderByAlphaNumeric(file => file.FullName))
                {
                    if (file.Directory == null) continue;
                    var paddedIndex = Parser.Parser.PadZeros(directoryIndex + "");
                    // We need to rename the files so that after flattening, they are in the order we found them
                    var newName = $"{paddedIndex}_{Parser.Parser.PadZeros(fileIndex + "")}.{file.Extension}";
                    var newPath = Path.Join(root.FullName, newName);
                    if (!File.Exists(newPath)) file.MoveTo(newPath);
                    fileIndex++;
                }

                directoryIndex++;
            }
            
            foreach (var subDirectory in directory.EnumerateDirectories())
            {
                FlattenDirectory(root, subDirectory, ref directoryIndex);
            }
        }

        public static IEnumerable<T> OrderByAlphaNumeric<T>(this IEnumerable<T> source, Func<T, string> selector)
        {
            int max = source.SelectMany(i => Regex.Matches(selector(i), @"\d+").Cast<Match>().Select(m => (int?)m.Value.Length)).Max() ?? 0;
            return source.OrderBy(i => Regex.Replace(selector(i), @"\d+", m => m.Value.PadLeft(max, '0')));
        }
    }
}