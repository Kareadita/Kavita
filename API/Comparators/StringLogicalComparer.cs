﻿//(c) Vasian Cepa 2005
// Version 2
// Taken from: https://www.codeproject.com/Articles/11016/Numeric-String-Sort-in-C

using static System.Char;

namespace API.Comparators
{
    public static class StringLogicalComparer
    {
        public static int Compare(string s1, string s2)
		{
			//get rid of special cases
			if((s1 == null) && (s2 == null)) return 0;
			if(s1 == null) return -1;
			if(s2 == null) return 1;

			if (string.IsNullOrEmpty(s1) && string.IsNullOrEmpty(s2)) return 0;
			if (string.IsNullOrEmpty(s1)) return -1;
			if (string.IsNullOrEmpty(s2)) return -1;

			//WE style, special case
			var sp1 = IsLetterOrDigit(s1, 0);
			var sp2 = IsLetterOrDigit(s2, 0);
			if(sp1 && !sp2) return 1;
			if(!sp1 && sp2) return -1;

			int i1 = 0, i2 = 0; //current index
			while(true)
			{
				var c1 = IsDigit(s1, i1);
				var c2 = IsDigit(s2, i2);
				int r; // temp result
				if(!c1 && !c2)
				{
					bool letter1 = IsLetter(s1, i1);
					bool letter2 = IsLetter(s2, i2);
					if((letter1 && letter2) || (!letter1 && !letter2))
					{
						if(letter1 && letter2)
						{
							r = ToLower(s1[i1]).CompareTo(ToLower(s2[i2]));
						}
						else
						{
							r = s1[i1].CompareTo(s2[i2]);
						}
						if(r != 0) return r;
					}
					else if(!letter1 && letter2) return -1;
					else if(letter1 && !letter2) return 1;
				}
				else if(c1 && c2)
				{
					r = CompareNum(s1, ref i1, s2, ref i2);
					if(r != 0) return r;
				}
				else if(c1)
				{
					return -1;
				}
				else if(c2)
				{
					return 1;
				}
				i1++;
				i2++;
				if((i1 >= s1.Length) && (i2 >= s2.Length))
				{
					return 0;
				}
				if(i1 >= s1.Length)
				{
					return -1;
				}
				if(i2 >= s2.Length)
				{
					return -1;
				}
			}
		}

		private static int CompareNum(string s1, ref int i1, string s2, ref int i2)
		{
			int nzStart1 = i1, nzStart2 = i2; // nz = non zero
			int end1 = i1, end2 = i2;

			ScanNumEnd(s1, i1, ref end1, ref nzStart1); 
			ScanNumEnd(s2, i2, ref end2, ref nzStart2);
			var start1 = i1; i1 = end1 - 1;
			var start2 = i2; i2 = end2 - 1;

			var nzLength1 = end1 - nzStart1;
			var nzLength2 = end2 - nzStart2;

			if(nzLength1 < nzLength2) return -1;
			if(nzLength1 > nzLength2) return 1;

			for(int j1 = nzStart1,j2 = nzStart2; j1 <= i1; j1++,j2++)
			{
				var r = s1[j1].CompareTo(s2[j2]);
				if(r != 0) return r;
			}
			// the nz parts are equal
			var length1 = end1 - start1;
			var length2 = end2 - start2;
			if(length1 == length2) return 0;
			if(length1 > length2) return -1;
			return 1;
		}

		//lookahead
		private static void ScanNumEnd(string s, int start, ref int end, ref int nzStart)
		{
			nzStart = start;
			end = start;
			var countZeros = true;
			while(IsDigit(s, end))
			{
				if(countZeros && s[end].Equals('0'))
				{
					nzStart++;
				}
				else countZeros = false;
				end++;
				if(end >= s.Length) break;
			}
		}
    }
}