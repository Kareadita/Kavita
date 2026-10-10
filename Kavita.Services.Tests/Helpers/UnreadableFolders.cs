using System.IO.Abstractions;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Kavita.Services.Scanner;

namespace Kavita.Services.Tests.Helpers;

/// <summary>
/// A file system where the given folders behave like permission denied ones: they exist, but every other
/// directory call on them throws <see cref="UnauthorizedAccessException"/>
/// </summary>
public static class UnreadableFolders
{
    public static IFileSystem Wrap(IFileSystem inner, params string[] folders)
    {
        var unreadable = folders.Select(f => Parser.NormalizePath(f).TrimEnd('/')).ToHashSet();
        var directory = Forwarder<IDirectory>.Create(inner.Directory, (method, args) =>
            method.Name != nameof(IDirectory.Exists) &&
            args is [string path, ..] &&
            unreadable.Contains(Parser.NormalizePath(path).TrimEnd('/')));

        return Forwarder<IFileSystem>.Create(inner, (_, _) => false,
            (method, result) => method.Name == $"get_{nameof(IFileSystem.Directory)}" ? directory : result);
    }

    public class Forwarder<T> : DispatchProxy where T : class
    {
        private T _inner = null!;
        private Func<MethodInfo, object?[]?, bool> _shouldThrow = null!;
        private Func<MethodInfo, object?, object?> _mapResult = null!;

        public static T Create(T inner, Func<MethodInfo, object?[]?, bool> shouldThrow,
            Func<MethodInfo, object?, object?>? mapResult = null)
        {
            var proxy = Create<T, Forwarder<T>>();
            var forwarder = (Forwarder<T>) (object) proxy;
            forwarder._inner = inner;
            forwarder._shouldThrow = shouldThrow;
            forwarder._mapResult = mapResult ?? ((_, result) => result);
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (_shouldThrow(targetMethod!, args))
            {
                throw new UnauthorizedAccessException($"Access to the path '{args![0]}' is denied.");
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
