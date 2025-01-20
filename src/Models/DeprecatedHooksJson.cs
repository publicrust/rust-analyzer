﻿using RustAnalyzer.Models;
using RustAnalyzer.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace RustAnalyzer
{
    internal static class DeprecatedHooksJson
    {
        /// <summary>
        /// Provides access to Rust plugin deprecated hook definitions.
        /// </summary>
        public const string Json = "";
        /// Gets the list of hooks as a strongly-typed collection.
        /// </summary>
        public static List<DeprecatedHookModel> GetHooks()
        {
            try
            {
                using var doc = JsonDocument.Parse(Json);
                var hooks = new List<DeprecatedHookModel>();

                var deprecated = doc.RootElement.GetProperty("deprecated");

                foreach (var property in deprecated.EnumerateObject())
                {
                    var oldHookString = property.Name;
                    var newHookString = property.Value.GetString();

                    // Parse old hook
                    var oldHook = HooksUtils.ParseHookString(oldHookString);

                    // Parse new hook if it exists
                    HookModel newHook = null;
                    if (!string.IsNullOrWhiteSpace(newHookString))
                    {
                        newHook = HooksUtils.ParseHookString(newHookString);
                    }

                    hooks.Add(new DeprecatedHookModel
                    {
                        OldHook = oldHook,
                        NewHook = newHook
                    });
                }

                return hooks;
            }
            catch (Exception ex)
            {
                throw new JsonException("Failed to parse hooks from JSON", ex);
            }
        }
    }
}
