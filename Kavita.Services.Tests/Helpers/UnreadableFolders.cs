using System.IO.Abstractions;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Kavita.Services.Scanner;

namespace Kavita.Services.Tests.Helpers;

/// <summary>
/// A file system where the given folders behave like permission denied ones: they exist, but every other
/// directory call on them, and every listing through <see cref="IDirectoryInfo"/>, throws <see cref="UnauthorizedAccessException"/>
/// </summary>
public static class UnreadableFolders
{
    private static readonly HashSet<string> DirectoryInfoListings =
    [
        nameof(IDirectoryInfo.EnumerateDirectories), nameof(IDirectoryInfo.EnumerateFiles), nameof(IDirectoryInfo.EnumerateFileSystemInfos),
        nameof(IDirectoryInfo.GetDirectories), nameof(IDirectoryInfo.GetFiles), nameof(IDirectoryInfo.GetFileSystemInfos),
    ];

    public static IFileSystem Wrap(IFileSystem inner, params string[] folders)
    {
        var unreadable = folders.Select(Normalize).ToHashSet();
        var directory = Forwarder<IDirectory>.Create(inner.Directory, (method, args) =>
            method.Name != nameof(IDirectory.Exists) && args is [string path, ..] && unreadable.Contains(Normalize(path))
                ? path
                : null);

        var directoryInfo = Forwarder<IDirectoryInfoFactory>.Create(inner.DirectoryInfo, (_, _) => null,
            (method, result) => method.Name == nameof(IDirectoryInfoFactory.New) && result is IDirectoryInfo info &&
                                unreadable.Contains(Normalize(info.FullName))
                ? Forwarder<IDirectoryInfo>.Create(info, (m, _) => DirectoryInfoListings.Contains(m.Name) ? info.FullName : null)
                : result);

        return Forwarder<IFileSystem>.Create(inner, (_, _) => null, (method, result) => method.Name switch
        {
            $"get_{nameof(IFileSystem.Directory)}" => directory,
            $"get_{nameof(IFileSystem.DirectoryInfo)}" => directoryInfo,
            _ => result,
        });
    }

    private static string Normalize(string path) => Parser.NormalizePath(path).TrimEnd('/');

    public class Forwarder<T> : DispatchProxy where T : class
    {
        private T _inner = null!;
        private Func<MethodInfo, object?[]?, string?> _deniedPath = null!;
        private Func<MethodInfo, object?, object?> _mapResult = null!;

        /// <param name="deniedPath">The path to report as denied when the call should throw, else null</param>
        public static T Create(T inner, Func<MethodInfo, object?[]?, string?> deniedPath,
            Func<MethodInfo, object?, object?>? mapResult = null)
        {
            var proxy = Create<T, Forwarder<T>>();
            var forwarder = (Forwarder<T>) (object) proxy;
            forwarder._inner = inner;
            forwarder._deniedPath = deniedPath;
            forwarder._mapResult = mapResult ?? ((_, result) => result);
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var denied = _deniedPath(targetMethod!, args);
            if (denied != null)
            {
                throw new UnauthorizedAccessException($"Access to the path '{denied}' is denied.");
            }

            try
            {
                return _mapResult(targetMethod!, targetMethod!.Invoke(_inner, args));
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }
    }
}
