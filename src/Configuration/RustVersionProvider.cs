using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using RustAnalyzer.src.Configuration;
using RustAnalyzer.src.Services;
using System;

namespace RustAnalyzer.Configuration
{
    public static class RustVersionProvider
    {
        private static string _version = "LastVersion";
        private static bool _isInitialized;

        public static bool IsInitialized => _isInitialized;

        public static void Initialize(AnalyzerConfigOptions options)
        {
            if (!options.TryGetValue("build_property.RustVersion", out var version))
            {
                version = "LastVersion";
            }

            Console.WriteLine($"[RustAnalyzer] Found RustVersion: {version}");
            _version = version;
            
            if (!_isInitialized)
            {
                var regularProvider = ProviderDiscovery.CreateRegularHooksProvider(version);
                var deprecatedProvider = ProviderDiscovery.CreateDeprecatedHooksProvider(version);
                var stringPoolProvider = ProviderDiscovery.CreateStringPoolProvider(version);

                var initialized = false;

                if (regularProvider != null)
                {
                    HooksConfiguration.Initialize(regularProvider);
                    initialized = true;
                    Console.WriteLine($"[RustAnalyzer] Initialized regular hooks for version: {_version}");
                }
                else
                {
                    Console.WriteLine($"[RustAnalyzer] Warning: Failed to initialize regular hooks for version '{_version}'");
                }

                if (deprecatedProvider != null)
                {
                    DeprecatedHooksConfiguration.Initialize(deprecatedProvider);
                    initialized = true;
                    Console.WriteLine($"[RustAnalyzer] Initialized deprecated hooks for version: {_version}");
                }
                else
                {
                    Console.WriteLine($"[RustAnalyzer] Warning: Failed to initialize deprecated hooks for version '{_version}'");
                }

                if (stringPoolProvider != null)
                {
                    StringPoolConfiguration.Initialize(stringPoolProvider);
                    initialized = true;
                    Console.WriteLine($"[RustAnalyzer] Initialized string pool for version: {_version}");
                }
                else
                {
                    Console.WriteLine($"[RustAnalyzer] Warning: Failed to initialize string pool for version '{_version}'");
                }

                _isInitialized = initialized;
                
                if (initialized)
                {
                    Console.WriteLine($"[RustAnalyzer] Successfully initialized available providers for version: {_version}");
                }
                else
                {
                    Console.WriteLine($"[RustAnalyzer] Failed to initialize: no providers found for version '{_version}'");
                }
            }
        }

        public static string Version => _version;
    }
}
