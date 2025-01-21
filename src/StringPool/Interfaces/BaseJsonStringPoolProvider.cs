using System;
using System.Collections.Generic;
using System.Text.Json;
using RustAnalyzer.src.Attributes;
using System.Reflection;

namespace RustAnalyzer.src.StringPool.Interfaces
{
    public abstract class BaseJsonStringPoolProvider : IStringPoolProvider
    {
        public virtual string Version => GetType()
            .GetCustomAttribute<VersionAttribute>()
            ?.Version ?? throw new InvalidOperationException("Version attribute is required");

        protected abstract string JsonContent { get; }
        
        public Dictionary<string, uint> GetToNumber()
        {
            try
            {
                using var doc = JsonDocument.Parse(JsonContent);
                var toNumber = new Dictionary<string, uint>();

                var root = doc.RootElement;
                foreach (var property in root.EnumerateObject())
                {
                    var key = property.Name;
                    var value = property.Value.GetUInt32();
                    toNumber.Add(key, value);
                }

                return toNumber;
            }
            catch (Exception ex)
            {
                throw new JsonException("Failed to parse string pool from JSON", ex);
            }
        }
    }
} 