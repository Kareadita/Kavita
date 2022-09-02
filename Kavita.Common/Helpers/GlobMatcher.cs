using System.Collections.Generic;
using System.Linq;
using DotNet.Globbing;

namespace Kavita.Common.Helpers;

/**
 * Matches against strings using Glob syntax
 */
public class GlobMatcher
{
    private readonly IList<Glob> _includes = new List<Glob>();
    private readonly IList<Glob> _excludes = new List<Glob>();

    public void AddInclude(string pattern)
    {
        _includes.Add(Glob.Parse(pattern));
    }

    public void AddExclude(string pattern)
    {
        _excludes.Add(Glob.Parse(pattern));
    }

    public bool ExcludeMatches(string file)
    {
        // NOTE: Glob.IsMatch() returns the opposite of what you'd expect
        return _excludes.Any(p => p.IsMatch(file));
    }


    /// <summary>
    ///
    /// </summary>
    /// <param name="file"></param>
    /// <param name="mustMatchIncludes"></param>
    /// <returns>True if any</returns>
    public bool IsMatch(string file, bool mustMatchIncludes = false)
    {
        // NOTE: Glob.IsMatch() returns the opposite of what you'd expect
        if (_excludes.Any(p => p.IsMatch(file))) return true;
        if (mustMatchIncludes)
        {
            return _includes.Any(p => p.IsMatch(file));
        }

        return false;
    }

    public void Merge(GlobMatcher matcher)
    {
        if (matcher == null) return;
        foreach (var glob in matcher._excludes)
        {
            _excludes.Add(glob);
        }

        foreach (var glob in matcher._includes)
        {
            _includes.Add(glob);
        }

    }
}
