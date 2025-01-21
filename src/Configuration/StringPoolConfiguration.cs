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
                _currentProvider = provider;
                _toNumber = provider.GetToNumber();
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