using System;
using System.Linq;
using System.Text.Json;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace RustAnalyzer.Configuration
{
    public static class JsonConfigReader
    {
        public static T? ReadConfig<T>(this ImmutableArray<AdditionalText> additionalFiles, string configFileName)
            where T : class
        {
            try
            {
                var configFile = additionalFiles
                    .FirstOrDefault(f => f.Path.EndsWith(configFileName, StringComparison.OrdinalIgnoreCase));

                if (configFile == null)
                {
                    Console.WriteLine($"[RustAnalyzer] Configuration file not found: {configFileName}");
                    return null;
                }

                var content = configFile.GetText()?.ToString();
                if (string.IsNullOrEmpty(content))
                {
                    Console.WriteLine("[RustAnalyzer] Configuration file is empty");
                    return null;
                }

                return JsonSerializer.Deserialize<T>(content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RustAnalyzer] Failed to read configuration: {ex.Message}");
                Console.WriteLine($"[RustAnalyzer] Stack trace: {ex.StackTrace}");
                return null;
            }
        }
    }
}
