using System;
using System.Collections.Generic;
using System.Linq;

namespace RustAnalyzer.Utils
{
    public static class StringSimilarity
    {
        /// <summary>
        /// Finds similar strings using multiple strategies:
        /// 1. Exact word matches (e.g. "OnDispenser" matches "OnDispenserBonus")
        /// 2. Word part matches (e.g. "metal.1" matches "metal", "metal1")
        /// 3. Levenshtein distance for typos
        /// </summary>
        public static IEnumerable<MatchResult<T>> FindSimilarWithContext<T>(
            string input,
            IEnumerable<(string text, T context)> candidates,
            int maxSuggestions = 3)
        {
            if (string.IsNullOrEmpty(input) || !candidates.Any())
                return Enumerable.Empty<MatchResult<T>>();

            input = input.ToLowerInvariant();
            var results = new List<MatchResult<T>>();

            // 1. Поиск точных совпадений слов
            foreach (var (text, context) in candidates)
            {
                var score = CalculateWordMatchScore(input, text.ToLowerInvariant());
                if (score > 0)
                {
                    results.Add(new MatchResult<T>(text, context, score, MatchType.WordMatch));
                }
            }

            // 2. Поиск совпадений частей слов
            if (results.Count < maxSuggestions)
            {
                foreach (var (text, context) in candidates)
                {
                    var score = CalculatePartialMatchScore(input, text.ToLowerInvariant());
                    if (score > 0)
                    {
                        results.Add(new MatchResult<T>(text, context, score, MatchType.PartialMatch));
                    }
                }
            }

            // 3. Поиск по расстоянию Левенштейна для опечаток
            if (results.Count < maxSuggestions)
            {
                foreach (var (text, context) in candidates)
                {
                    var distance = StringDistance.GetLevenshteinDistance(input, text.ToLowerInvariant());
                    var maxDistance = Math.Max(input.Length, text.Length) / 3; // Допускаем до 33% различий
                    
                    if (distance <= maxDistance)
                    {
                        var score = 100 - (distance * 100 / Math.Max(input.Length, text.Length));
                        results.Add(new MatchResult<T>(text, context, score, MatchType.Levenshtein));
                    }
                }
            }

            // Сортируем результаты по типу совпадения и затем по оценке
            return results
                .OrderByDescending(r => r.Type)
                .ThenByDescending(r => r.Score)
                .Take(maxSuggestions)
                .Distinct(new MatchResultComparer<T>());
        }

        /// <summary>
        /// Backward compatibility method that returns just the strings
        /// </summary>
        public static IEnumerable<string> FindSimilar(string input, IEnumerable<string> candidates, int maxSuggestions = 3)
        {
            return FindSimilarWithContext(input, candidates.Select(c => (c, string.Empty)), maxSuggestions)
                .Select(r => r.Text);
        }

        /// <summary>
        /// Represents a match result with its source context
        /// </summary>
        public class MatchResult<T>
        {
            public string Text { get; }
            public T Context { get; }
            public int Score { get; }
            public MatchType Type { get; }

            public MatchResult(string text, T context, int score, MatchType type)
            {
                Text = text;
                Context = context;
                Score = score;
                Type = type;
            }

            public override string ToString() => Text;
        }

        private static int CalculateWordMatchScore(string input, string candidate)
        {
            var inputWords = input.Split(new[] { '.', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var candidateWords = candidate.Split(new[] { '.', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            int score = 0;

            // Проверяем точное совпадение префикса
            if (candidate.StartsWith(input, StringComparison.OrdinalIgnoreCase))
            {
                score += 50; // Высокий приоритет для префиксных совпадений
            }

            // Проверяем вхождение как подстроки
            if (candidate.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                score += 30;
            }

            // Проверяем совпадения по словам
            foreach (var inputWord in inputWords)
            {
                foreach (var candidateWord in candidateWords)
                {
                    // Точное совпадение слова
                    if (string.Equals(inputWord, candidateWord, StringComparison.OrdinalIgnoreCase))
                    {
                        score += inputWord.Length * 15;
                    }
                    // Слово является префиксом
                    else if (candidateWord.StartsWith(inputWord, StringComparison.OrdinalIgnoreCase))
                    {
                        score += inputWord.Length * 10;
                    }
                    // Слово содержится внутри
                    else if (candidateWord.IndexOf(inputWord, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        score += inputWord.Length * 5;
                    }
                }
            }

            return score;
        }

        private static int CalculatePartialMatchScore(string input, string candidate)
        {
            // Разбиваем строки на части по разделителям
            var inputParts = input.Split(new[] { '.', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var candidateParts = candidate.Split(new[] { '.', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            int score = 0;

            // Проверяем общие части слов
            foreach (var inputPart in inputParts)
            {
                foreach (var candidatePart in candidateParts)
                {
                    // Общий префикс
                    var commonPrefix = GetCommonPrefix(inputPart, candidatePart, ignoreCase: true);
                    if (commonPrefix.Length >= 2) // Минимум 2 символа
                    {
                        score += commonPrefix.Length * 4;
                    }

                    // Общий суффикс
                    var commonSuffix = GetCommonSuffix(inputPart, candidatePart, ignoreCase: true);
                    if (commonSuffix.Length >= 2) // Минимум 2 символа
                    {
                        score += commonSuffix.Length * 3;
                    }
                }
            }

            return score;
        }

        private static string GetCommonPrefix(string str1, string str2, bool ignoreCase)
        {
            var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            int minLength = Math.Min(str1.Length, str2.Length);
            int i;
            for (i = 0; i < minLength; i++)
            {
                if (!str1[i].ToString().Equals(str2[i].ToString(), comparison))
                    break;
            }
            return str1.Substring(0, i);
        }

        private static string GetCommonSuffix(string str1, string str2, bool ignoreCase)
        {
            var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            int minLength = Math.Min(str1.Length, str2.Length);
            int i;
            for (i = 0; i < minLength; i++)
            {
                if (!str1[str1.Length - 1 - i].ToString().Equals(str2[str2.Length - 1 - i].ToString(), comparison))
                    break;
            }
            return i > 0 ? str1.Substring(str1.Length - i) : string.Empty;
        }

        public enum MatchType
        {
            WordMatch = 3,    // Точное совпадение слов
            PartialMatch = 2, // Частичное совпадение
            Levenshtein = 1   // Похожие по расстоянию Левенштейна
        }

        private class MatchResultComparer<T> : IEqualityComparer<MatchResult<T>>
        {
            public bool Equals(MatchResult<T> x, MatchResult<T> y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (x is null || y is null) return false;
                return x.ToString() == y.ToString();
            }

            public int GetHashCode(MatchResult<T> obj)
            {
                return obj?.ToString().GetHashCode() ?? 0;
            }
        }
    }
} 