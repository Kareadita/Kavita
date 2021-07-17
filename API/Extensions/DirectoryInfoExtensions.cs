using System.IO;
using System.Linq;
using API.Comparators;

namespace API.Extensions
{
    public static class DirectoryInfoExtensions
    {
        private static readonly NaturalSortComparer Comparer = new NaturalSortComparer();
        public static void Empty(this DirectoryInfo directory)
        {
          // NOTE: We have this in DirectoryService.Empty(), do we need this here?
          foreach(FileInfo file in directory.EnumerateFiles()) file.Delete();
          foreach(DirectoryInfo subDirectory in directory.EnumerateDirectories()) subDirectory.Delete(true);
        }

        public static void RemoveNonImages(this DirectoryInfo directory)
        {
          foreach (var file in directory.EnumerateFiles())
          {
            if (!Parser.Parser.IsImage(file.FullName))
            {
              file.Delete();
            }
          }
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

                foreach (var file in directory.EnumerateFiles().OrderBy(file => file.FullName, Comparer))
                {
                    if (file.Directory == null) continue;
                    var paddedIndex = Parser.Parser.PadZeros(directoryIndex + "");
                    // We need to rename the files so that after flattening, they are in the order we found them
                    var newName = $"{paddedIndex}_{Parser.Parser.PadZeros(fileIndex + "")}{file.Extension}";
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
    }
}
