using RustAnalyzer.Models;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace RustAnalyzer
{
    public static class StringPoolJson
    {

        public static Dictionary<string, uint> GetToNumber()
        {
            try
            {
                using var doc = JsonDocument.Parse(Json);
                var toNumber = new Dictionary<string, uint>();

                var deprecated = doc.RootElement;

                foreach (var property in deprecated.EnumerateObject())
                {
                    var key = property.Name;
                    var value = property.Value.GetUInt32();

                    toNumber.Add(key, value);
                }

                return toNumber;
            }
            catch (Exception ex)
            {
                throw new JsonException("Failed to parse hooks from JSON", ex);
            }
        }
    }
}