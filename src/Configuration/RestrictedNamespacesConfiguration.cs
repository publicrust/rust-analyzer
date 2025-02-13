using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace RustAnalyzer
{
    public class RestrictedNamespacesConfiguration
    {
        private static readonly HashSet<string> DefaultRestrictedNamespaces = new HashSet<string>
        {
            "System.IO",
            "System.Net",
            "System.Reflection",
            "System.Threading",
            "System.Runtime.InteropServices",
            "System.Diagnostics",
            "System.Security",
            "System.Timers",
        };

        private static readonly HashSet<string> DefaultAllowedTypes = new HashSet<string>
        {
            "System.Diagnostics.Stopwatch",
            "System.IO.MemoryStream",
            "System.IO.Stream",
            "System.IO.BinaryReader",
            "System.IO.BinaryWriter",
            "System.Net.Dns",
            "System.Net.IPAddress",
            "System.Net.IPEndPoint",
            "System.Net.NetworkInformation",
            "System.Net.Sockets.SocketFlags",
            "System.Security.Cryptography",
            "System.Threading.Interlocked",
        };

        public HashSet<string> RestrictedNamespaces { get; } =
            new HashSet<string>(DefaultRestrictedNamespaces);
        public HashSet<string> AllowedTypes { get; } = new HashSet<string>(DefaultAllowedTypes);

        public static RestrictedNamespacesConfiguration Create()
        {
            return new RestrictedNamespacesConfiguration();
        }
    }
}
