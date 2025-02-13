using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using RustAnalyzer.Models;
using RustAnalyzer.src.Attributes;
using RustAnalyzer.Utils;

namespace RustAnalyzer.src.DeprecatedHooks.Interfaces
{
    public abstract class BaseDeprecatedJsonHooksProvider : IDeprecatedHooksProvider
    {
        public virtual string Version =>
            GetType().GetCustomAttribute<VersionAttribute>()?.Version
            ?? throw new InvalidOperationException("Version attribute is required");
        protected abstract string JsonContent { get; }

        public List<DeprecatedHookModel> GetHooks()
        {
            try
            {
                using var doc = JsonDocument.Parse(JsonContent);
                var hooks = new List<DeprecatedHookModel>();
                var deprecated = doc.RootElement.GetProperty("deprecated");

                foreach (var property in deprecated.EnumerateObject())
                {
                    var oldHookString = property.Name;
                    var newHookString = property.Value.GetString();

                    // Parse old hook
                    var oldHook = HooksUtils.ParseHookString(oldHookString);
                    if (oldHook == null)
                        continue;

                    // Parse new hook if it exists
                    HookModel? newHook = null;
                    if (!string.IsNullOrWhiteSpace(newHookString))
                    {
                        newHook = HooksUtils.ParseHookString(newHookString);
                    }

                    hooks.Add(new DeprecatedHookModel { OldHook = oldHook, NewHook = newHook });
                }

                return hooks;
            }
            catch (Exception)
            {
                return new List<DeprecatedHookModel>();
            }
        }
    }
}
