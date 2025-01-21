using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RustAnalyzer.Utils
{
    public static class StringDistance
    {
        public static int GetLevenshteinDistance(string s, string t)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.IsNullOrEmpty(t) ? 0 : t.Length;
            }

            if (string.IsNullOrEmpty(t))
            {
                return s.Length;
            }

            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            for (int i = 0; i <= n; i++)
            {
                d[i, 0] = i;
            }

            for (int j = 0; j <= m; j++)
            {
                d[0, j] = j;
            }

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (s[i - 1] == t[j - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            return d[n, m];
        }

        public static IEnumerable<string> FindSimilarPrefabs(string input, IEnumerable<string> prefabs, int maxSuggestions = 3)
        {
            return prefabs
                .Select(p => new { Prefab = p, Distance = GetLevenshteinDistance(input, p) })
                .OrderBy(x => x.Distance)
                .Take(maxSuggestions)
                .Select(x => x.Prefab);
        }

        public static IEnumerable<string> FindSimilarShortNames(string input, IEnumerable<string> prefabs, int maxSuggestions = 3)
        {
            return prefabs
                .Select(p => System.IO.Path.GetFileNameWithoutExtension(p))
                .Distinct()
                .Select(p => new { ShortName = p, Distance = GetLevenshteinDistance(input, p) })
                .OrderBy(x => x.Distance)
                .Take(maxSuggestions)
                .Select(x => x.ShortName);
        }

        public static IEnumerable<(string key, string value)> FindKeyValues(string input, IEnumerable<(string key, string value)> prefabs, int maxSuggestions = 3)
        {
            return prefabs
                .Select(p => new { p.key, ShortName = p.value, Distance = GetLevenshteinDistance(input, p.value) })
                .OrderBy(x => x.Distance)
                .Take(maxSuggestions)
                .Select(x => (x.key, x.ShortName));
        }
    }
}
