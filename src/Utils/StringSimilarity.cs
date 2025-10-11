using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace RustAnalyzer.Utils
{
    public static class StringSimilarity
    {
        private const double ExactMatchBonus = 0.3;
        private const double ContainmentBonus = 0.2;
        private const double TokenMatchBonus = 0.2;
        private const double PartialTokenMatchBonus = 0.1;
        private const double MultipleTokensBonus = 0.2;
        private const double LengthPenaltyPerChar = 0.01;
        private const double MinimumSimilarityScore = 0.3;
        private const double JaroWinklerScalingFactor = 0.1;

        private const int DefaultMaxResults = 5;
        private const int MinTokensForBonus = 2;
        private const int MaxPrefixLength = 4;

        private static readonly Regex NormalizeRegex = new("[_\\s]", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
        private static readonly Regex SplitRegex = new("(?=[A-Z])|[_\\s\\d]", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

        public static IEnumerable<MatchResult<T>> FindSimilarWithContext<T>(
            string queryName,
            IEnumerable<(string text, T context)> candidates,
            int maxSuggestions = DefaultMaxResults)
        {
            if (string.IsNullOrEmpty(queryName) || candidates is null)
            {
                return Enumerable.Empty<MatchResult<T>>();
            }

            List<(string Key, T Value)> candidateEntries = candidates
                .Where(static candidate => !string.IsNullOrEmpty(candidate.text))
                .Select(static candidate => (candidate.text, candidate.context))
                .ToList();

            if (candidateEntries.Count == 0)
            {
                return Enumerable.Empty<MatchResult<T>>();
            }

            IEnumerable<MatchResult<T>> results = FindSimilarSymbols(queryName, candidateEntries, maxSuggestions)
                .Select(item => new MatchResult<T>(item.Name, item.Value, item.Score, MatchType.Composite));

            return results
                .OrderByDescending(static r => r.Score)
                .ThenByDescending(static r => r.Text, StringComparer.Ordinal)
                .Distinct(new MatchResultComparer<T>())
                .Take(maxSuggestions);
        }

        public static IEnumerable<string> FindSimilar(
            string queryName,
            IEnumerable<string> candidates,
            int maxSuggestions = DefaultMaxResults)
        {
            if (string.IsNullOrEmpty(queryName) || candidates is null)
            {
                return Enumerable.Empty<string>();
            }

            return FindSimilarWithContext(
                    queryName,
                    candidates.Select(static candidate => (text: candidate, context: candidate)),
                    maxSuggestions)
                .Select(static result => result.Text);
        }

        public static List<(string name, double score)> FindPossibleExports(
            string requestedName,
            INamespaceSymbol moduleSymbol,
            int maxResults = DefaultMaxResults)
        {
            if (moduleSymbol is null)
            {
                return [];
            }

            try
            {
                return moduleSymbol
                    .GetMembers()
                    .Select(member => (member.Name, ComputeCompositeScore(requestedName, member.Name)))
                    .Where(item => item.Item2 > MinimumSimilarityScore)
                    .OrderByDescending(static item => item.Item2)
                    .Take(maxResults)
                    .Select(static item => (item.Name, item.Item2))
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StringSimilarity.FindPossibleExports] Error retrieving exports: {ex}");
                return [];
            }
        }

        public static List<(string Name, double Score)> FindSimilarLocalSymbols(
            string queryName,
            string[] candidateNames,
            int maxResults = DefaultMaxResults)
        {
            if (candidateNames is null || candidateNames.Length == 0)
            {
                return [];
            }

            return candidateNames
                .Where(static candidate => !string.IsNullOrEmpty(candidate))
                .Select(candidate => (Name: candidate, Score: ComputeCompositeScore(queryName, candidate)))
                .Where(item => item.Score >= MinimumSimilarityScore)
                .OrderByDescending(static item => item.Score)
                .Take(maxResults)
                .ToList();
        }

        public static List<(string Name, TValue Value, double Score)> FindSimilarSymbols<TValue>(
            string queryName,
            IEnumerable<(string Key, TValue Value)> candidateEntries,
            int maxResults = DefaultMaxResults)
        {
            if (string.IsNullOrEmpty(queryName) || candidateEntries is null)
            {
                return [];
            }

            return candidateEntries
                .Select(entry => (Name: entry.Key, entry.Value, Score: ComputeCompositeScore(queryName, entry.Key)))
                .Where(item =>
                    !string.IsNullOrEmpty(item.Name) &&
                    item.Score >= MinimumSimilarityScore &&
                    !string.Equals(item.Name, queryName, StringComparison.Ordinal))
                .OrderByDescending(static item => item.Score)
                .ThenBy(static item => item.Name, StringComparer.Ordinal)
                .Take(maxResults)
                .ToList();
        }

        public static List<string> GetFormattedMembersList(ITypeSymbol objectType, string requestedName)
        {
            if (objectType is null)
            {
                return [];
            }

            return objectType
                .GetMembers()
                .Select(member => MemberDisplayFormatter.FormatMember(member, requestedName))
                .Where(static result => result.score >= MinimumSimilarityScore)
                .OrderByDescending(static result => result.score)
                .Take(DefaultMaxResults)
                .Select(static result => result.displayName)
                .ToList();
        }

        internal static double ComputeCompositeScore(string unknown, string candidate)
        {
            if (string.IsNullOrEmpty(unknown) || string.IsNullOrEmpty(candidate))
            {
                return 0.0;
            }

            string normalizedQuery = Normalize(unknown);
            string normalizedCandidate = Normalize(candidate);

            double baseSimilarity = JaroWinkler(normalizedQuery, normalizedCandidate);

            double exactBonus = string.Equals(normalizedQuery, normalizedCandidate, StringComparison.Ordinal)
                ? ExactMatchBonus
                : 0.0;

            bool contains = normalizedCandidate.Contains(normalizedQuery) ||
                            normalizedQuery.Contains(normalizedCandidate);
            double containmentBonus = contains ? ContainmentBonus : 0.0;

            HashSet<string> tokensQuery = new(SplitIdentifier(unknown), StringComparer.Ordinal);
            HashSet<string> tokensCandidate = new(SplitIdentifier(candidate), StringComparer.Ordinal);

            double tokenBonus = 0.0;
            int tokensMatched = 0;

            foreach (string tokenQuery in tokensQuery)
            {
                foreach (string tokenCandidate in tokensCandidate)
                {
                    if (string.Equals(tokenQuery, tokenCandidate, StringComparison.Ordinal))
                    {
                        tokenBonus += TokenMatchBonus;
                        tokensMatched++;
                    }
                    else if (tokenQuery.StartsWith(tokenCandidate, StringComparison.Ordinal) ||
                             tokenCandidate.StartsWith(tokenQuery, StringComparison.Ordinal))
                    {
                        tokenBonus += PartialTokenMatchBonus;
                        tokensMatched++;
                    }
                }
            }

            if (tokensMatched >= MinTokensForBonus)
            {
                tokenBonus += MultipleTokensBonus;
            }

            double lengthPenalty = Math.Max(0, candidate.Length - unknown.Length) * LengthPenaltyPerChar;

            return baseSimilarity + exactBonus + containmentBonus + tokenBonus - lengthPenalty;
        }

        internal static double Jaro(string s1, string s2)
        {
            if (s1 is null || s2 is null)
            {
                return 0.0;
            }

            if (string.Equals(s1, s2, StringComparison.Ordinal))
            {
                return 1.0;
            }

            int len1 = s1.Length;
            int len2 = s2.Length;

            if (len1 == 0 || len2 == 0)
            {
                return 0.0;
            }

            int matchDistance = (int)Math.Floor(Math.Max(len1, len2) / 2.0) - 1;
            (int matches, bool[] s1Matches, bool[] s2Matches) = JaroSimilarityHelper.FindMatches(s1, s2, matchDistance);

            if (matches == 0)
            {
                return 0.0;
            }

            int transpositions = JaroSimilarityHelper.CountTranspositions(s1, s2, s1Matches, s2Matches);
            return JaroSimilarityHelper.CalculateJaroScore(matches, transpositions, len1, len2);
        }

        internal static double JaroWinkler(string s1, string s2)
        {
            if (s1 is null || s2 is null)
            {
                return 0.0;
            }

            double jaroScore = Jaro(s1, s2);

            int prefix = 0;
            int maxLength = Math.Min(MaxPrefixLength, Math.Min(s1.Length, s2.Length));

            for (int i = 0; i < maxLength; i++)
            {
                if (s1[i] == s2[i])
                {
                    prefix++;
                }
                else
                {
                    break;
                }
            }

            return jaroScore + (prefix * JaroWinklerScalingFactor * (1 - jaroScore));
        }

        internal static string Normalize(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return string.Empty;
            }

            try
            {
                return NormalizeRegex.Replace(str.ToLowerInvariant(), string.Empty);
            }
            catch (RegexMatchTimeoutException ex)
            {
                Console.WriteLine($"[StringSimilarity.Normalize] Regex timeout: {ex}");
                return str.ToLowerInvariant().Replace("_", string.Empty).Replace(" ", string.Empty);
            }
        }

        internal static string[] SplitIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                return [];
            }

            try
            {
                return SplitRegex
                    .Split(identifier)
                    .Select(static s => s.ToLowerInvariant())
                    .Where(static s => !string.IsNullOrWhiteSpace(s))
                    .ToArray();
            }
            catch (RegexMatchTimeoutException ex)
            {
                Console.WriteLine($"[StringSimilarity.SplitIdentifier] Regex timeout: {ex}");
                return [identifier.ToLowerInvariant()];
            }
        }

        public sealed class MatchResult<T>
        {
            public MatchResult(string text, T context, double score, MatchType type)
            {
                Text = text;
                Context = context;
                Score = score;
                Type = type;
            }

            public string Text { get; }

            public T Context { get; }

            public double Score { get; }

            public MatchType Type { get; }

            public override string ToString() => Text;
        }

        public enum MatchType
        {
            Levenshtein = 1,
            PartialMatch = 2,
            WordMatch = 3,
            Composite = 4,
        }

        private sealed class MatchResultComparer<T> : IEqualityComparer<MatchResult<T>>
        {
            public bool Equals(MatchResult<T>? x, MatchResult<T>? y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (x is null || y is null)
                {
                    return false;
                }

                return string.Equals(x.Text, y.Text, StringComparison.Ordinal);
            }

            public int GetHashCode(MatchResult<T> obj)
            {
                if (obj?.Text is null)
                {
                    return 0;
                }

                return StringComparer.Ordinal.GetHashCode(obj.Text);
            }
        }
    }
}
