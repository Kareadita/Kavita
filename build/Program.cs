using System;
using static Bullseye.Targets;
using static SimpleExec.Command;

const string Build = "build";
const string Test = "test";
const string Angular = "angular";
const string CSharp = "csharp";
const string PackageClean = "package-clean";
const string PackageAll = "package-all";
const string PackageMonoRepo = "package-monorepo";
const string Tools = "tools";
const string Swagger = "swagger";

const string OutputFolder = "_output";
const string TargetFramework = "net6.0";
var AllRuntimes = new[]
{
    "win-x64",
    "win-x86",
    "linux-x64",
    "linux-arm",
    "linux-arm64",
    "linux-musl-x64",
    "osx-x64"
};
var MonoRepoRuntimes = new[]
{
    "linux-x64",
    "linux-arm",
    "linux-arm64"
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

    Console.WriteLine("Creating tar");
    var workingDir = $"{OutputFolder}/{runtime}/";
    Run("tar", $" -czvf kavita-{runtime}.tar.gz Kavita", workingDir);
}

Target(
    Build,
    () =>
    {
        Run("dotnet", "build Kavita.sln -c Release", ".");
    }
);
Target(
    Test,
    DependsOn(Build),
    () => Run("dotnet", "test . -c Release --no-restore --no-build --verbosity=normal", ".")
);
Target("default", DependsOn(CSharp, Angular), () => { });

Target(PackageClean, () =>
{
    Run("rm", $"-rf {OutputFolder}");
});
Target(PackageAll, DependsOn(Build, Angular, PackageClean), AllRuntimes, r => PackageRuntime(TargetFramework, r));
Target(PackageMonoRepo, DependsOn(Build, Angular, PackageClean), MonoRepoRuntimes, r => PackageRuntime(TargetFramework, r));

Target(Tools, () =>
{
    Run("dotnet", "tool restore", ".");
});

Target(Swagger, DependsOn(Build, Tools), () =>
{
    Run("dotnet", $"swagger tofile --output openapi.json API/bin/Release/{TargetFramework}/API.dll v1", ".");
});

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

await RunTargetsAndExitAsync(args);
