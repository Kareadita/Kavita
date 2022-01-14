using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using static System.GC;
using static System.String;

namespace API.Comparators
{
    /// <summary>
    /// Attempts to emulate Windows explorer sorting
    /// </summary>
    /// <remarks>This is not thread-safe</remarks>
    public sealed class NaturalSortComparer : IComparer<string>, IDisposable
    {
        private readonly bool _isAscending;
        private Dictionary<string, string[]> _table = new();

        private bool _disposed;

        private static readonly Regex Regex = new Regex(@"\d+", RegexOptions.Compiled);


        public NaturalSortComparer(bool inAscendingOrder = true)
        {
            _isAscending = inAscendingOrder;
        }

        int IComparer<string>.Compare(string? x, string? y)
        {
            if (x == y) return 0;

            if (x != null && y == null) return -1;
            if (x == null) return 1;


            if (!_table.TryGetValue(x ?? Empty, out var x1))
            {
                x1 = Regex.Split(x ?? Empty, "([0-9]+)");
                _table.Add(x ?? Empty, x1);
            }

            if (!_table.TryGetValue(y ?? Empty, out var y1))
            {
                y1 = Regex.Split(y ?? Empty, "([0-9]+)");
                _table.Add(y ?? Empty, y1);
            }

            int returnVal;

            for (var i = 0; i < x1.Length && i < y1.Length; i++)
            {
                if (x1[i] == y1[i]) continue;
                if (x1[i] == Empty || y1[i] == Empty) continue;
                returnVal = PartCompare(x1[i], y1[i]);
                return _isAscending ? returnVal : -returnVal;
            }

            if (y1.Length > x1.Length)
            {
                returnVal = -1;
            }
            else if (x1.Length > y1.Length)
            {
                returnVal = 1;
            }
            else
            {
                // If same length, can we do a first character sort then result to 0?
                returnVal = 0;
            }


            return _isAscending ? returnVal : -returnVal;
        }

        private static int PartCompare(string left, string right)
        {
            if (!int.TryParse(left, out var x))
                return Compare(left, right, StringComparison.Ordinal);

            if (!int.TryParse(right, out var y))
                return Compare(left, right, StringComparison.Ordinal);

            return x.CompareTo(y);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // called via myClass.Dispose().
                    _table.Clear();
                    _table = null;
                }
                // Release unmanaged resources.
                // Set large fields to null.
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            SuppressFinalize(this);
        }

        ~NaturalSortComparer() // the finalizer
        {
            Dispose(false);
        }
    }
}
