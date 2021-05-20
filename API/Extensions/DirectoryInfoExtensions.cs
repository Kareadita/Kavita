using System.IO;

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
            FlattenDirectory(directory, directory, 0);
        }

        private static void FlattenDirectory(DirectoryInfo root, DirectoryInfo directory, int directoryIndex)
        {
            if (!root.FullName.Equals(directory.FullName))
            {
                foreach (var file in directory.EnumerateFiles())
                {
                    if (file.Directory == null) continue;
                    var paddedIndex = Parser.Parser.PadZeros(directoryIndex + "");
                    var newName = $"{paddedIndex}_{file.Name}";
                    var newPath = Path.Join(root.FullName, newName);
                    if (!File.Exists(newPath)) file.MoveTo(newPath);
                    
                }
            }

            var i = directoryIndex + 1;
            foreach (var subDirectory in directory.EnumerateDirectories())
            {
                FlattenDirectory(root, subDirectory, i);
                i += 1;
            }
        }
    }
}