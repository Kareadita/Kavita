namespace API.Extensions
{
    public static class DirectoryInfoExtensions
    {
        public static void Empty(this System.IO.DirectoryInfo directory)
        {
            foreach(System.IO.FileInfo file in directory.EnumerateFiles()) file.Delete();
            foreach(System.IO.DirectoryInfo subDirectory in directory.EnumerateDirectories()) subDirectory.Delete(true);
        }
    }
}