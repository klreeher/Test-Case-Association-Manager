using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using AssociateTestsToTestCases.Manager.File;

namespace AssociateTestsToTestCases.Extensions
{
    public static class MethodInfoExtensions
    {
        private const string NUnitTestCaseAttr = "NUnit.Framework.TestCaseAttribute";
        private const string NUnitTestCaseSourceAttr = "NUnit.Framework.TestCaseSourceAttribute";
        private const string NUnitTestAttr = "NUnit.Framework.TestAttribute";
        private const string NUnitTestCaseDataType = "NUnit.Framework.TestCaseData";

        public static TestMethod[] ToTestMethodArray(this MethodInfo[] methods, bool expandParameterizedTests = false)
        {
            if (methods == null || methods.Length == 0) return Array.Empty<TestMethod>();

            var result = new List<TestMethod>();

            foreach (var method in methods)
            {
                if (method == null) continue;

                if (expandParameterizedTests)
                {
                    // Expand mode: Create one test method per parameterized variation
                    var expanded = ExpandNUnitCases(method).ToList();
                    if (expanded.Count == 0)
                    {
                        // Non-parameterized
                        result.Add(ToTestMethod(method, method.Name));
                    }
                    else
                    {
                        result.AddRange(expanded.Select(name => ToTestMethod(method, name)));
                    }
                }
                else
                {
                    // Consolidate mode: Create one test method per base method (ignore parameters)
                    result.Add(ToTestMethod(method, method.Name));
                }
            }

            return result.ToArray();
        }

        private static TestMethod ToTestMethod(MethodInfo mi, string displayName)
        {
            return new TestMethod(
                name: displayName,
                assemblyName: mi.Module.Name,
                fullClassName: mi.DeclaringType?.FullName ?? "<unknown>",
                id: Guid.NewGuid()
            );
        }

        /// <summary>
        /// Returns 0+ display names for NUnit parameterizations on the method.
        /// If none exist, returns empty list (caller treats as single non-parameterized test).
        /// </summary>
        private static IEnumerable<string> ExpandNUnitCases(MethodInfo method)
        {
            var attrs = method.GetCustomAttributesData();

            // [TestCase(...)] => one row per attribute instance
            var testCases = attrs.Where(a => a.AttributeType.FullName == NUnitTestCaseAttr).ToList();
            foreach (var tc in testCases)
            {
                var testName = TryGetNamedArgString(tc, "TestName");
                var args = tc.ConstructorArguments.Select(a => a.Value).ToArray();

                yield return !string.IsNullOrWhiteSpace(testName)
                    ? testName
                    : BuildName(method.Name, args);
            }

            // [TestCaseSource(...)] => attempt to resolve the source and enumerate rows
            var sources = attrs.Where(a => a.AttributeType.FullName == NUnitTestCaseSourceAttr).ToList();
            foreach (var src in sources)
            {
                foreach (var name in ExpandFromTestCaseSource(method, src))
                    yield return name;
            }
        }

        private static IEnumerable<string> ExpandFromTestCaseSource(MethodInfo testMethod, CustomAttributeData srcAttr)
        {
            var results = new List<string>();

            // NUnit TestCaseSourceAttribute common ctor patterns:
            // - (string sourceName)
            // - (Type sourceType, string sourceName)
            // - (string sourceName, object[] methodParams)   (we'll ignore methodParams for now)
            // - (Type sourceType, string sourceName, object[] methodParams) (ignore params)
            //
            // We'll try to interpret constructor args accordingly.

            var ctorArgs = srcAttr.ConstructorArguments.Select(a => a.Value).ToArray();

            Type? sourceType = null;
            string? sourceName = null;

            if (ctorArgs.Length >= 1 && ctorArgs[0] is string s0)
            {
                // (string sourceName) or (string sourceName, object[] ...)
                sourceType = testMethod.DeclaringType;
                sourceName = s0;
            }
            else if (ctorArgs.Length >= 2 && ctorArgs[0] is Type t0 && ctorArgs[1] is string s1)
            {
                // (Type sourceType, string sourceName) ...
                sourceType = t0;
                sourceName = s1;
            }

            if (sourceType == null || string.IsNullOrWhiteSpace(sourceName))
            {
                // Can't interpret => emit a single “marker” row
                results.Add($"{testMethod.Name} [TestCaseSource:unresolved]");
                return results;
            }

            object? enumerable;
            try
            {
                enumerable = GetSourceEnumerable(sourceType, sourceName);
            }
            catch (Exception ex)
            {
                results.Add($"{testMethod.Name} [TestCaseSource:error:{ex.GetType().Name}]");
                return results;
            }

            if (enumerable is not IEnumerable seq)
            {
                results.Add($"{testMethod.Name} [TestCaseSource:not-enumerable]");
                return results;
            }

            var any = false;
            foreach (var item in seq)
            {
                any = true;

                // Item can be:
                // - object[]
                // - single object
                // - NUnit.Framework.TestCaseData
                // - (rare) something else enumerable
                if (item == null)
                {
                    results.Add(BuildName(testMethod.Name, new object?[] { null }));
                    continue;
                }

                if (item is object[] arr)
                {
                    results.Add(BuildName(testMethod.Name, arr));
                    continue;
                }

                var itemType = item.GetType();
                if (itemType.FullName == NUnitTestCaseDataType)
                {
                    results.Add(ExtractNameFromTestCaseData(testMethod.Name, item));
                    continue;
                }

                // Single value case
                results.Add(BuildName(testMethod.Name, new object?[] { item }));
            }

            if (!any)
            {
                results.Add($"{testMethod.Name} [TestCaseSource:empty]");
            }

            return results;
        }

        private static object? GetSourceEnumerable(Type sourceType, string sourceName)
        {
            const BindingFlags flags =
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Static |
                BindingFlags.FlattenHierarchy;

            // Field
            var field = sourceType.GetField(sourceName, flags);
            if (field != null)
                return field.GetValue(null);

            // Property
            var prop = sourceType.GetProperty(sourceName, flags);
            if (prop != null)
                return prop.GetValue(null);

            // Method (parameterless)
            var method = sourceType.GetMethod(
                sourceName,
                flags,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);

            if (method != null)
                return method.Invoke(null, null);

            throw new MissingMemberException(sourceType.FullName, sourceName);
        }


        private static string ExtractNameFromTestCaseData(string methodName, object testCaseData)
        {
            // Try to read TestName and Arguments via reflection (no compile-time dependency on TestCaseData API surface)
            try
            {
                var t = testCaseData.GetType();

                // In NUnit, TestCaseData may carry a name; property might be "TestName" or something similar.
                // We'll try a couple of likely options.
                var testNameProp = t.GetProperty("TestName") ?? t.GetProperty("Name");
                var testName = testNameProp?.GetValue(testCaseData) as string;

                // Arguments often appear as "Arguments" (object[])
                var argsProp = t.GetProperty("Arguments");
                var args = argsProp?.GetValue(testCaseData) as object[];

                if (!string.IsNullOrWhiteSpace(testName))
                    return testName;

                if (args != null)
                    return BuildName(methodName, args);
            }
            catch
            {
                // ignore and fall back
            }

            return $"{methodName} [TestCaseData]";
        }

        private static string? TryGetNamedArgString(CustomAttributeData attr, string namedArg)
        {
            try
            {
                foreach (var na in attr.NamedArguments)
                {
                    if (string.Equals(na.MemberName, namedArg, StringComparison.OrdinalIgnoreCase))
                        return na.TypedValue.Value as string;
                }
            }
            catch
            {
                /* ignore */
            }

            return null;
        }

        private static string BuildName(string methodName, object?[] args)
        {
            // Produces: MethodName("pdf", 123, null)
            var rendered = args.Select(FormatArg);
            return $"{methodName}({string.Join(", ", rendered)})";
        }

        private static string FormatArg(object? arg)
        {
            if (arg == null) return "null";

            // Strings quoted for readability
            if (arg is string s) return "\"" + s.Replace("\"", "\\\"") + "\"";

            // Use invariant for numeric-ish
            if (arg is IFormattable f) return f.ToString(null, CultureInfo.InvariantCulture);

            return arg.ToString() ?? "<unknown>";
        }
    }
}
