using System.Collections.Generic;

namespace API.Comparators
{
    public class ChapterSortComparer : IComparer<float>
    {
        // public int Compare(int x, int y)
        // {
        //     if (x == 0 && y == 0) return 0;
        //     // if x is 0, it comes second
        //     if (x == 0) return 1;
        //     // if y is 0, it comes second
        //     if (y == 0) return -1;
        //
        //     return x.CompareTo(y);
        // }
        
        public int Compare(float x, float y)
        {
            if (x == 0.0 && y == 0.0) return 0;
            // if x is 0, it comes second
            if (x == 0.0) return 1;
            // if y is 0, it comes second
            if (y == 0.0) return -1;

            return x.CompareTo(y);
        }
    }
}