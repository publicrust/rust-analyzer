using System;
using System.Collections.Generic;
using System.Linq;

namespace RustAnalyzer.Utils
{
    public static class StringSimilarity
    {
        /// <summary>
        /// Represents a match result with its source context
        /// </summary>
        public class MatchResult
        {
            public string Text { get; }
            public string Context { get; }
            public int Score { get; }
            public MatchType Type { get; }

            public MatchResult(string text, string context, int score, MatchType type)
            {
                Text = text;
                Context = context;
                Score = score;
                Type = type;
            }

            public override string ToString() => 
                string.IsNullOrEmpty(Context) ? Text : $"{Text} {Context}";
        }

        /// <summary>
        /// Finds similar strings using multiple strategies:
        /// 1. Exact word matches (e.g. "OnDispenser" matches "OnDispenserBonus")
        /// 2. Word part matches (e.g. "metal.1" matches "metal", "metal1")
        /// 3. Levenshtein distance for typos
        /// </summary>
        public static IEnumerable<MatchResult> FindSimilarWithContext(
            string input,
            IEnumerable<(string text, string context)> candidates,
            int maxSuggestions = 3)
        {
            if (string.IsNullOrEmpty(input) || !candidates.Any())
                return Enumerable.Empty<MatchResult>();

            input = input.ToLowerInvariant();
            var results = new List<MatchResult>();

            // 1. Поиск точных совпадений слов
            foreach (var (text, context) in candidates)
            {
                var score = CalculateWordMatchScore(input, text.ToLowerInvariant());
                if (score > 0)
                {
                    results.Add(new MatchResult(text, context, score, MatchType.WordMatch));
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
                        results.Add(new MatchResult(text, context, score, MatchType.PartialMatch));
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
                        results.Add(new MatchResult(text, context, score, MatchType.Levenshtein));
                    }
                }
            }

            // Сортируем результаты по типу совпадения и затем по оценке
            return results
                .OrderByDescending(r => r.Type)
                .ThenByDescending(r => r.Score)
                .Take(maxSuggestions)
                .DistinctBy(r => r.ToString());
        }

        /// <summary>
        /// Backward compatibility method that returns just the strings
        /// </summary>
        public static IEnumerable<string> FindSimilar(string input, IEnumerable<string> candidates, int maxSuggestions = 3)
        {
            return FindSimilarWithContext(input, candidates.Select(c => (c, string.Empty)), maxSuggestions)
                .Select(r => r.Text);
        }

        private static int CalculateWordMatchScore(string input, string candidate)
        {
            var inputWords = input.Split(new[] { '.', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var candidateWords = candidate.Split(new[] { '.', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            int score = 0;
            foreach (var inputWord in inputWords)
            {
                foreach (var candidateWord in candidateWords)
                {
                    if (candidateWord.Contains(inputWord) || inputWord.Contains(candidateWord))
                    {
                        // Более длинные совпадения получают больший вес
                        score += Math.Min(inputWord.Length, candidateWord.Length) * 10;
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
            foreach (var inputPart in inputParts)
            {
                foreach (var candidatePart in candidateParts)
                {
                    // Проверяем совпадение начала или конца слова
                    if (candidatePart.StartsWith(inputPart) || candidatePart.EndsWith(inputPart))
                    {
                        score += inputPart.Length * 5;
                    }
                    // Проверяем совпадение части слова
                    else if (candidatePart.Contains(inputPart))
                    {
                        score += inputPart.Length * 3;
                    }
                }
            }

            return score;
        }

        private enum MatchType
        {
            WordMatch = 3,    // Точное совпадение слов
            PartialMatch = 2, // Частичное совпадение
            Levenshtein = 1   // Похожие по расстоянию Левенштейна
        }
    }
} 