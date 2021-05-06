using System.IO;

namespace API.Tests.Helpers
{
    /// <summary>
    /// Given a -testcase.txt file, will generate a folder with fake archive or book files. These files are just renamed txt files.
    /// <remarks>This currently is broken - you cannot create files from a unit test it seems</remarks>
    /// </summary>
    public static class TestCaseGenerator
    {
        public static string GenerateFiles(string directory, string fileToExpand)
        {
            //var files = Directory.GetFiles(directory, fileToExpand);
            var file = new FileInfo(fileToExpand);
            if (!file.Exists && file.Name.EndsWith("-testcase.txt")) return string.Empty;

            var baseDirectory = TestCaseGenerator.CreateTestBase(fileToExpand, directory);
            var filesToCreate = File.ReadLines(file.FullName);
            foreach (var fileToCreate in filesToCreate)
            {
                // var folders = DirectoryService.GetFoldersTillRoot(directory, fileToCreate);
                // foreach (var VARIABLE in COLLECTION)
                // {
                //     
                // }
                File.Create(fileToCreate);
            }




            return baseDirectory;
        }

        /// <summary>
        /// Creates and returns a new base directory for data creation for a given testcase
        /// </summary>
        /// <param name="file"></param>
        /// <param name="rootDirectory"></param>
        /// <returns></returns>
        private static string CreateTestBase(string file, string rootDirectory)
        {
            var baseDir = file.Split("-testcase.txt")[0];
            var newDirectory = Path.Join(rootDirectory, baseDir);
            if (!Directory.Exists(newDirectory))
            {
                new DirectoryInfo(newDirectory).Create();
            }

            return newDirectory;
        }
    }
}