using System;
using System.Collections.Generic;
using RustAnalyzer.src.StringPool.Interfaces;
using RustAnalyzer.src.Services;

namespace RustAnalyzer.src.Configuration
{
    public static class StringPoolConfiguration
    {
        private static IStringPoolProvider? _currentProvider;
        private static Dictionary<string, uint> _toNumber = new();

        public static void Initialize(IStringPoolProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            try
            {
                var regularProvider = ProviderDiscovery.CreateStringPoolProvider("Universal");
                if (regularProvider == null)
                {
                    _currentProvider = provider;
                    _toNumber = provider.GetToNumber();
                    return;
                }

                // Создаем словарь для быстрого поиска
                var dictionary = new Dictionary<string, uint>();
                
                // Сначала добавляем все из regularProvider
                foreach (var pair in regularProvider.GetToNumber())
                {
                    dictionary[pair.Key] = pair.Value;
                }
                
                // Добавляем из provider, только если такого ключа еще нет
                foreach (var pair in provider.GetToNumber())
                {
                    if (!dictionary.ContainsKey(pair.Key))
                    {
                        dictionary[pair.Key] = pair.Value;
                    }
                }

                _currentProvider = provider;
                _toNumber = dictionary;
            }
            catch (Exception)
            {
                _currentProvider = null;
                _toNumber = new Dictionary<string, uint>();
            }
        }

        public static string? CurrentVersion => _currentProvider?.Version;

        public static Dictionary<string, uint> ToNumber => _toNumber;
    }
} 