using System;

namespace RustAnalyzer.Utils
{
    internal static class JaroSimilarityHelper
    {
        private const double JaroComponents = 3.0;
        private const int TranspositionDivisor = 2;

        internal static double CalculateJaroScore(int matches, int transpositions, int len1, int len2)
        {
            return matches <= 0 || len1 <= 0 || len2 <= 0
                ? 0.0
                : (((double)matches / len1) +
                   ((double)matches / len2) +
                   (((double)matches - transpositions) / matches)) / JaroComponents;
        }

        internal static int CountTranspositions(string s1, string s2, bool[] s1Matches, bool[] s2Matches)
        {
            if (s1 is null || s2 is null || s1Matches is null || s2Matches is null)
            {
                return 0;
            }

            if (s1Matches.Length != s1.Length || s2Matches.Length != s2.Length)
            {
                return 0;
            }

            int transpositions = 0;
            int k = 0;

            for (int i = s1.Length - 1; i >= 0; i--)
            {
                if (!s1Matches[i])
                {
                    continue;
                }

                while (k < s2Matches.Length && !s2Matches[k])
                {
                    k++;
                }

                if (k >= s2Matches.Length)
                {
                    break;
                }

                if (s1[i] != s2[k])
                {
                    transpositions++;
                }

                k++;
            }

            return Math.DivRem(transpositions, TranspositionDivisor, out _);
        }

        internal static (int matches, bool[] s1Matches, bool[] s2Matches) FindMatches(
            string s1,
            string s2,
            int matchDistance)
        {
            if (s1 is null || s2 is null)
            {
                return (0, [], []);
            }

            int len1 = s1.Length;
            int len2 = s2.Length;

            if (len1 == 0 || len2 == 0)
            {
                return (0, new bool[len1], new bool[len2]);
            }

            bool[] s1Matches = new bool[len1];
            bool[] s2Matches = new bool[len2];
            int matches = 0;

            for (int i = len1 - 1; i >= 0; i--)
            {
                int start = Math.Max(0, i - matchDistance);
                int end = Math.Min(i + matchDistance + 1, len2);

                for (int j = end - 1; j >= start; j--)
                {
                    if (s2Matches[j] || s1[i] != s2[j])
                    {
                        continue;
                    }

                    s1Matches[i] = true;
                    s2Matches[j] = true;
                    matches++;
                    break;
                }
            }

            return (matches, s1Matches, s2Matches);
        }
    }
}
