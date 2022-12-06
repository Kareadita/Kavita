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
const string Package = "package";

const string OutputFolder = "_output";
var Runtimes = new[]
{
    "win-x64",
    "win-x86",
    "linux-x64",
    "linux-arm",
    "linux-arm64",
    "linux-musl-x64",
    "osx-x64"
};

static void PackageRuntime(string framework, string runtime)
{
    var outputFolder = $"{OutputFolder}/{runtime}/Kavita";

    Console.WriteLine($"Creating {runtime} Package for {framework}");

    Run(
        "dotnet",
        $"publish -c Release --self-contained --runtime {runtime} -o \"{outputFolder}\" --framework {framework}",
        "."
    );

    Console.WriteLine("Recopying wwwroot due to bug");
    Run("cp", $"-R ./API/wwwroot/ {outputFolder}/wwwroot");

    Console.WriteLine("Copying Install information");
    Run("cp", $" INSTALL.txt {outputFolder}/README.txt");

    Console.WriteLine("Copying LICENSE");
    Run("cp", $" LICENSE {outputFolder}/LICENSE.txt");

    Console.WriteLine("Renaming API -> Kavita");
    if (runtime == "win-x64" || runtime == "win-x86")
    {
        Run("cp", $"{outputFolder}/API.exe {outputFolder}/Kavita.exe");
    }
    else
    {
        Run("mv", $"{outputFolder}/API {outputFolder}/Kavita");
    }

    Console.WriteLine("Copying appsettings.json");
    Run("cp", $" API/config/appsettings.json {outputFolder}/config/appsettings.json");

    Console.WriteLine("Creating tar");
    var workingDir = $"{OutputFolder}/{runtime}/";
    Run("tar", $" -czvf kavita-{runtime}.tar.gz Kavita", workingDir);
}

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
Target(
    Build,
    () =>
    {
        Run("rm", $"-rf {OutputFolder}");
        Run("dotnet", "build Kavita.sln -c Release", ".");
    }
);
Target(
    Test,
    DependsOn(Build),
    () => Run("dotnet", "test . -c Release --no-restore --no-build --verbosity=normal", ".")
);
Target("default", DependsOn(CSharp, Angular), () => { });

Target(Package, DependsOn(Build, Angular), Runtimes, r => PackageRuntime("net6.0", r));

Target(CSharp, DependsOn(Build));
Target(
    Angular,
    () =>
    {
        var path = "UI/Web";
        Console.WriteLine("Removing old wwwroot");
        Run("rm", "-rf API/wwwroot/*");
        Console.WriteLine("Installing web dependencies");
        Run("npm", "ci", path);
        Console.WriteLine("Building UI");
        Run("npm", "run prod", path);
        Console.WriteLine("Copying back to Kavita wwwroot");
        Run("mkdir", "-p ../../API/wwwroot", path);
        Run("cp", "-R dist/ ../../API/wwwroot", path);
    }
);

foreach (var runtime in Runtimes)
{
    Target(runtime, DependsOn(Build, Angular), () => PackageRuntime("net6.0", runtime));
}

await RunTargetsAndExitAsync(args);
