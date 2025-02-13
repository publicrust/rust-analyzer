using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using RustAnalyzer.Models;
using RustAnalyzer.src.Attributes;
using RustAnalyzer.Utils;

namespace RustAnalyzer.src.Hooks.Interfaces
{
    public abstract class BaseJsonHooksProvider : IHooksProvider
    {
        public virtual string Version =>
            GetType().GetCustomAttribute<VersionAttribute>()?.Version
            ?? throw new InvalidOperationException("Version attribute is required");
        protected abstract string JsonContent { get; }

        public List<HookModel> GetHooks()
        {
            // Общая логика парсинга JSON
            try
            {
                var jsonOptions = new JsonSerializerOptions { AllowTrailingCommas = true };
                var docOptions = new JsonDocumentOptions { AllowTrailingCommas = true };

                using var doc = JsonDocument.Parse(JsonContent, docOptions);
                var hooksJson = doc.RootElement.GetProperty("hooks").GetRawText();
                var hooks = JsonSerializer.Deserialize<List<string>>(hooksJson, jsonOptions);

                return hooks.Select(HooksUtils.ParseHookString).Where(h => h != null).ToList();
            }
            catch (Exception)
            {
                return new List<HookModel>();
            }
        }
    }
}
