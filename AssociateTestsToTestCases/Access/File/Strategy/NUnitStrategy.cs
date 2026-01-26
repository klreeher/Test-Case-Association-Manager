using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace AssociateTestsToTestCases.Access.File.Strategy
{
    public class NUnitStrategy : ITestFrameworkStrategy
    {
        public TestFrameworkType TestFrameworkType { get; set; } = TestFrameworkType.NUnit;

        public IEnumerable<MethodInfo> RetrieveTestMethods(Assembly testAssembly)
        {
            Type[] types;
            try
            {
                types = testAssembly.GetTypes();
                Console.WriteLine($"[DEBUG] NUnitStrategy: Successfully retrieved {types.Length} type(s) from assembly");
                var typesWithAnyNUnitMethodAttrs = types.Count(t =>
                {
                    try
                    {
                        return t.GetMethods(BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)
                            .SelectMany(m => m.GetCustomAttributesData())
                            .Any(a => a.AttributeType.FullName != null &&
                                      a.AttributeType.FullName.StartsWith("NUnit.Framework.", StringComparison.Ordinal));
                    }
                    catch { return false; }
                });

                Console.WriteLine($"[DEBUG] NUnitStrategy: Types with ANY NUnit method attributes: {typesWithAnyNUnitMethodAttrs}");

            }
            catch (ReflectionTypeLoadException ex)
            {
                // Some types couldn't be loaded (likely missing dependencies like NUnit framework)
                // Use successfully loaded types only
                types = ex.Types.Where(t => t != null).ToArray();
                Console.WriteLine($"[DEBUG] NUnitStrategy: ReflectionTypeLoadException - {types.Length} types loaded out of {ex.Types.Length} total");
                if (ex.LoaderExceptions != null && ex.LoaderExceptions.Length > 0)
                {
                    var nunitErrors = ex.LoaderExceptions.Where(e => e?.Message?.Contains("nunit", StringComparison.OrdinalIgnoreCase) == true).Take(3);
                    foreach (var error in nunitErrors)
                    {
                        Console.WriteLine($"[DEBUG] NUnitStrategy: Loader error: {error?.Message}");
                    }
                }
            }

            Console.WriteLine($"[DEBUG] NUnitStrategy: Checking {types.Length} types for test fixtures...");
            int testFixtureCount = 0;
            var testMethods = new List<MethodInfo>();

            foreach (var type in types)
            {
                if (type == null || type.IsAbstract) continue;

                try
                {
                    // Always scan methods first
                    var methods = type
                        .GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                        .Where(IsTestMethod)
                        .ToList();

                    if (methods.Count > 0)
                    {
                        testFixtureCount++;
                        Console.WriteLine($"[DEBUG] NUnitStrategy: Found test fixture: {type.FullName}");
                        Console.WriteLine($"[DEBUG] NUnitStrategy:   Found {methods.Count} test method(s) in {type.FullName}");
                        testMethods.AddRange(methods);
                    }


                }
                catch (Exception typeEx)
                {
                    Console.WriteLine($"[DEBUG] NUnitStrategy: Error checking type {type.FullName}: {typeEx.Message}");
                }
            }


            Console.WriteLine($"[DEBUG] NUnitStrategy: Total test fixtures: {testFixtureCount}, Total test methods: {testMethods.Count}");
            return testMethods;
        }

        private bool IsTestFixture(Type type)
        {
            try
            {
                return type.GetCustomAttribute<TestFixtureAttribute>() != null;
            }
            catch
            {
                // Fallback: check by attribute name if NUnit assembly not loaded
                try
                {
                    return type.GetCustomAttributesData()
                        .Any(a => a.AttributeType.FullName == "NUnit.Framework.TestFixtureAttribute");
                }
                catch
                {
                    return false;
                }
            }
        }

        private bool IsTestMethod(MethodInfo method)
        {
            try
            {
                return method.GetCustomAttributes<TestAttribute>().Any()
                    || method.GetCustomAttributes<TestCaseAttribute>().Any()
                    || method.GetCustomAttributes<TestCaseSourceAttribute>().Any()
                    || method.GetCustomAttributes<TheoryAttribute>().Any();
            }
            catch
            {
                // Fallback: check by attribute name if NUnit assembly not loaded
                try
                {
                    var attributes = method.GetCustomAttributesData();
                    return attributes.Any(a =>
                        a.AttributeType.FullName == "NUnit.Framework.TestAttribute" ||
                        a.AttributeType.FullName == "NUnit.Framework.TestCaseAttribute" ||
                        a.AttributeType.FullName == "NUnit.Framework.TestCaseSourceAttribute" ||
                        a.AttributeType.FullName == "NUnit.Framework.TheoryAttribute");
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}

