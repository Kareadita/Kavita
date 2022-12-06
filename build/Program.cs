using System;
using System.Collections.Generic;
using System.IO;
using GlobExpressions;
using static Bullseye.Targets;
using static SimpleExec.Command;

const string Clean = "clean";
const string Build = "build";
const string Test = "test";
const string Angular = "angular";
const string CSharp = "csharp";
//const string Format = "format";
//const string FixFormat = "fix-format";

/*static void Publish(string project, string path)
{
    Run("dotnet", $"publish {project} -c Release -f net6.0 -o {path} --no-restore --no-build --verbosity=normal");
}*/

Target(
    Clean,
    ForEach("**/node_modules", "./publish", "**/bin", "**/obj"),
    dir =>
    {
        IEnumerable<string> GetDirectories(string d)
        {
            return Glob.Directories(".", d);
        }

        void RemoveDirectory(string d)
        {
            if (Directory.Exists(d))
            {
                Console.WriteLine(d);
                Directory.Delete(d, true);
            }
        }

        foreach (var d in GetDirectories(dir))
        {
            RemoveDirectory(d);
        }
    }
);
/*Target(
    Format,
    () =>
    {
        Run("dotnet", "tool restore", "./csharp");
        Run("dotnet", "csharpier --check .", "./csharp");
    }
);
Target(
    FixFormat,
    () =>
    {
        Run("dotnet", "tool restore", "./csharp");
        Run("dotnet", "csharpier .", "./csharp");
    }
);*/
Target(Build, () => Run("dotnet", "build Kavita.sln -c Release", "."));
Target(
    Test,
    DependsOn(Build),
    () =>
        Run(
            "dotnet",
            "test . -c Release --no-restore --no-build --verbosity=normal",
            "."
        )
);
Target(
    "default",
        DependsOn(CSharp, Angular),
    () =>
    {
    }
);

Target(CSharp, DependsOn(Build));
Target(
    Angular,
    () =>
    {
        var path = "UI/Web";
        Run("npm", "ci", path);

        Run("npm", "run prod", path);
    }
);

await RunTargetsAndExitAsync(args);
