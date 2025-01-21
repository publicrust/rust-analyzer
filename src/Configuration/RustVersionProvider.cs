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

                if (regularProvider != null && deprecatedProvider != null)
                {
                    HooksConfiguration.Initialize(regularProvider);
                    DeprecatedHooksConfiguration.Initialize(deprecatedProvider);
                    _isInitialized = true;
                    Console.WriteLine($"[RustAnalyzer] Successfully initialized with version: {_version}");
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
