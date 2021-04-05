using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using static System.GC;

namespace API.Comparators
{
    public class NaturalSortComparer : IComparer<string>, IDisposable
    {
        private readonly bool _isAscending;
        private Dictionary<string, string[]> _table = new();

        public NaturalSortComparer(bool inAscendingOrder = true)
        {
            _isAscending = inAscendingOrder;
        }

        int IComparer<string>.Compare(string x, string y)
        {
            if (x == y)
                return 0;

            string[] x1, y1;

            if (!_table.TryGetValue(x, out x1))
            {
                x1 = Regex.Split(x.Replace(" ", ""), "([0-9]+)");
                _table.Add(x, x1);
            }

            if (!_table.TryGetValue(y ?? string.Empty, out y1))
            {
                y1 = Regex.Split(y?.Replace(" ", ""), "([0-9]+)");
                _table.Add(y, y1);
            }

            int returnVal;

            for (var i = 0; i < x1.Length && i < y1.Length; i++)
            {
                if (x1[i] == y1[i]) continue;
                returnVal = PartCompare(x1[i], y1[i]);
                return _isAscending ? returnVal : -returnVal;
            }

            if (y1.Length > x1.Length)
            {
                returnVal = 1;
            }
            else if (x1.Length > y1.Length)
            { 
                returnVal = -1; 
            }
            else
            {
                returnVal = 0;
            }

            return _isAscending ? returnVal : -returnVal;
        }

        private static int PartCompare(string left, string right)
        {
            int x, y;
            if (!int.TryParse(left, out x))
                return left.CompareTo(right);

            if (!int.TryParse(right, out y))
                return left.CompareTo(right);

            return x.CompareTo(y);
        }

        public void Dispose()
        {
            SuppressFinalize(this);
            _table.Clear();
            _table = null;
        }
    }
}