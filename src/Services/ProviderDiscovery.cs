using System;
using System.Linq;
using System.Reflection;
using RustAnalyzer.src.Attributes;
using RustAnalyzer.src.DeprecatedHooks.Interfaces;
using RustAnalyzer.src.Hooks.Interfaces;
using RustAnalyzer.src.StringPool.Interfaces;

namespace RustAnalyzer.src.Services
{
    public static class ProviderDiscovery
    {
        public static IHooksProvider? CreateRegularHooksProvider(string version)
        {
            return CreateProvider<IHooksProvider>(version);
        }

        public static IDeprecatedHooksProvider? CreateDeprecatedHooksProvider(string version)
        {
            return CreateProvider<IDeprecatedHooksProvider>(version);
        }

        public static IStringPoolProvider? CreateStringPoolProvider(string version)
        {
            return CreateProvider<IStringPoolProvider>(version);
        }

        private static T? CreateProvider<T>(string version)
            where T : class
        {
            var assembly = Assembly.GetExecutingAssembly();

            var providerType = assembly
                .GetTypes()
                .Where(t => typeof(T).IsAssignableFrom(t) && !t.IsAbstract)
                .Where(t => t.GetCustomAttribute<VersionAttribute>()?.Version == version)
                .FirstOrDefault();

            return providerType != null ? (T)Activator.CreateInstance(providerType) : null;
        }
    }
}
