using System;
using System.Collections.Generic;
using System.Linq;
using RustAnalyzer.Utils;

namespace RustAnalyzer.Models
{
    public static class HookModelExtensions
    {
        public static HookModel ToHookModel(this HookImplementationModel implementation)
        {
            if (implementation == null)
                return null;

            var hookSignature = HooksUtils.ParseHookString(implementation.HookSignature);
            if (hookSignature == null)
            {
                Console.WriteLine($"[RustAnalyzer] Warning: Failed to parse hook signature: {implementation.HookSignature}");
                return null;
            }

            var methodSignature = HooksUtils.ParseHookString(implementation.MethodSignature);
            if (methodSignature == null)
            {
                Console.WriteLine($"[RustAnalyzer] Warning: Failed to parse method signature: {implementation.MethodSignature}");
                return null;
            }

            return new HookModel
            {
                Signature = hookSignature,
                HookCallLine = implementation.HookLineInvoke,
                Method = new MethodSourceModel
                {
                    ClassName = implementation.MethodClassName,
                    SourceCode = implementation.MethodSourceCode,
                    Signature = methodSignature
                }
            };
        }

        public static List<HookModel> ToHookModels(this List<HookImplementationModel> implementations)
        {
            if (implementations == null)
                return new List<HookModel>();

            return implementations
                .Select(impl => impl.ToHookModel())
                .Where(model => model != null)
                .ToList();
        }
    }
} 