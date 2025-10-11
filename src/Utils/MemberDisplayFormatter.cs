using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace RustAnalyzer.Utils
{
    internal static class MemberDisplayFormatter
    {
        internal static (string name, string displayName, double score) FormatMember(
            ISymbol member,
            string requestedName)
        {
            if (member is null)
            {
                return (string.Empty, string.Empty, 0.0);
            }

            if (string.IsNullOrEmpty(requestedName))
            {
                return (member.Name, member.Name, 0.0);
            }

            try
            {
                string displayName = FormatMemberDisplayName(member);
                double score = StringSimilarity.ComputeCompositeScore(requestedName, member.Name);
                return (member.Name, displayName, score);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MemberDisplayFormatter.FormatMember] Error formatting member: {ex}");

                string memberName = member?.Name ?? "unknown";
                double score = StringSimilarity.ComputeCompositeScore(requestedName, memberName);
                return (memberName, memberName, score);
            }
        }

        private static string FormatMemberDisplayName(ISymbol member)
        {
            if (member is null)
            {
                return string.Empty;
            }

            return member switch
            {
                IMethodSymbol methodSymbol => FormatMethodDisplayName(methodSymbol),
                IPropertySymbol propertySymbol => $"{member.Name}: {propertySymbol.Type?.ToString() ?? "object"}",
                IFieldSymbol fieldSymbol => $"{member.Name}: {fieldSymbol.Type?.ToString() ?? "object"}",
                _ => member.Name,
            };
        }

        private static string FormatMethodDisplayName(IMethodSymbol methodSymbol)
        {
            if (methodSymbol is null)
            {
                return string.Empty;
            }

            System.Collections.Generic.List<string> parameters = [.. methodSymbol.Parameters
                .Select(static p => $"{p.Name ?? "param"}: {p.Type?.ToString() ?? "object"}")];

            string paramString = string.Join(", ", parameters);
            string returnType = methodSymbol.ReturnType?.ToString() ?? "void";

            string displayName = $"{methodSymbol.Name}({paramString})";
            if (!string.Equals(returnType, "void", StringComparison.Ordinal))
            {
                displayName += $": {returnType}";
            }

            return displayName;
        }
    }
}
