using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using RustAnalyzer.Models;
using RustAnalyzer.src.Configuration;
using System.Text.Json;

namespace RustAnalyzer.Configuration
{
    public static class RustVersionProvider
    {
        private static bool _isInitialized;
        private static readonly object _initLock = new object();
        private const string ConfigFolder = ".rust-analyzer";

        public static bool IsInitialized => _isInitialized;

        public static void Initialize(AnalyzerConfigOptions options, ImmutableArray<AdditionalText> additionalFiles)
        {
            if (_isInitialized)
                return;

            lock (_initLock)
            {
                if (_isInitialized)
                    return;

                var configHooks = additionalFiles.ReadConfig<List<HookImplementationModel>>(
                    Path.Combine(ConfigFolder, "hooks.json")
                );
                var configDeprecatedHooks = additionalFiles.ReadConfig<Dictionary<string, string>>(
                    Path.Combine(ConfigFolder, "deprecatedHooks.json")
                );
                var configStringPool = additionalFiles.ReadConfig<Dictionary<string, uint>>(
                    Path.Combine(ConfigFolder, "stringPool.json")
                );

                var configHooksPlugin = additionalFiles.ReadConfig<List<PluginHookDefinition>>(
                    Path.Combine(ConfigFolder, "hooksPlugin.json")
                );

                var configApiMethodsPlugin = additionalFiles.ReadConfig<List<PluginHookDefinition>>(
                    Path.Combine(ConfigFolder, "apiMethodsPlugin.json")
                );

                var initialized = false;

                try
                {
                    if (configHooks != null)
                    {
                        HooksConfiguration.Initialize(configHooks.ToHookModels());
                        initialized = true;
                        Console.WriteLine($"[RustAnalyzer] Initialized hooks from configuration file");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RustAnalyzer] Failed to initialize hooks: {ex.Message}");
                }

                try
                {
                    if (configHooksPlugin != null)
                    {
                        PluginHooksConfiguration.Initialize(configHooksPlugin);
                        initialized = true;
                        Console.WriteLine($"[RustAnalyzer] Initialized plugin hooks from configuration file");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RustAnalyzer] Failed to initialize plugin hooks: {ex.Message}");
                }

                try
                {
                    if (configDeprecatedHooks != null)
                    {
                        DeprecatedHooksConfiguration.Initialize(configDeprecatedHooks);
                        initialized = true;
                        Console.WriteLine($"[RustAnalyzer] Initialized deprecated hooks from configuration file");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RustAnalyzer] Failed to initialize deprecated hooks: {ex.Message}");
                }

                try
                {
                    if (configApiMethodsPlugin != null)
                    {
                        PluginMethodsConfiguration.Initialize(configApiMethodsPlugin);
                        initialized = true;
                        Console.WriteLine($"[RustAnalyzer] Initialized plugin methods from configuration file");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RustAnalyzer] Failed to initialize plugin methods: {ex.Message}");
                }

                try
                {
                    if (configStringPool != null)
                    {
                        StringPoolConfiguration.Initialize(configStringPool);
                        initialized = true;
                        Console.WriteLine($"[RustAnalyzer] Initialized string pool from configuration file");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RustAnalyzer] Failed to initialize string pool: {ex.Message}");
                }

                _isInitialized = initialized;

                if (initialized)
                {
                    Console.WriteLine($"[RustAnalyzer] Successfully initialized configuration");
                }
                else
                {
                    Console.WriteLine($"[RustAnalyzer] Failed to initialize any configuration");
                }
            }
        }
    }
}
