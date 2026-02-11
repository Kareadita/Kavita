using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Kavita.Services.Extensions;

public static class StringExtensions
{
    extension(string input)
    {
        public IList<string> SplitBy(char separator)
        {
            if (string.IsNullOrEmpty(input))
            {
                return ImmutableList<string>.Empty;
            }

            return input.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .DistinctBy(Scanner.Parser.Normalize)
                .ToList();
        }
    }
}
