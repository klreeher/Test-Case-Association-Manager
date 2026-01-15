using System;
using System.Linq;
using System.Reflection;
using AssociateTestsToTestCases.Access.File;
using AssociateTestsToTestCases.Access.File.Strategy;
using NUnit.Framework;
using System.Collections.Generic;

namespace TestNUnitDiscovery
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: TestNUnitDiscovery.exe <path-to-dll>");
                Console.WriteLine("Example: TestNUnitDiscovery.exe \"C:\\path\\to\\Ktws.ApiTests.dll\"");
                return;
            }

            string dllPath = args[0];

            try
            {
                Console.WriteLine($"Loading assembly: {dllPath}");
                Console.WriteLine();

                var assemblyHelper = new AssemblyHelper();
                var strategy = new NUnitStrategy();
                
                var assembly = assemblyHelper.LoadFrom(dllPath);
                Console.WriteLine($"Assembly loaded: {assembly.FullName}");
                Console.WriteLine();

                // Handle ReflectionTypeLoadException which occurs when some types can't be loaded
                // due to missing dependencies, but we can still discover test methods from loaded types
                List<MethodInfo> testMethods;
                try
                {
                    testMethods = strategy.RetrieveTestMethods(assembly).ToList();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    Console.WriteLine("Warning: Some types could not be loaded due to missing dependencies.");
                    Console.WriteLine($"This is normal if the DLL has dependencies that aren't in the current directory.");
                    Console.WriteLine($"Attempting to discover test methods from successfully loaded types...");
                    Console.WriteLine();
                    
                    // Try to get test methods from the types that were successfully loaded
                    var loadedTypes = ex.Types.Where(t => t != null);
                    testMethods = new List<MethodInfo>();
                    
                    foreach (var type in loadedTypes)
                    {
                        try
                        {
                            // Check if it's a test fixture
                            var isTestFixture = false;
                            try
                            {
                                isTestFixture = type.GetCustomAttribute<TestFixtureAttribute>() != null;
                            }
                            catch
                            {
                                // Skip this type if we can't check attributes
                                continue;
                            }

                            if (isTestFixture)
                            {
                                var methods = type.GetMethods()
                                    .Where(method =>
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
                                            // Skip methods where we can't check attributes
                                            return false;
                                        }
                                    });
                                testMethods.AddRange(methods);
                            }
                        }
                        catch
                        {
                            // Skip types that cause issues
                            continue;
                        }
                    }
                }

                Console.WriteLine($"Found {testMethods.Count} test method(s):");
                Console.WriteLine();

                if (testMethods.Count == 0)
                {
                    Console.WriteLine("No test methods discovered. Possible reasons:");
                    Console.WriteLine("- No classes with [TestFixture] attribute");
                    Console.WriteLine("- No methods with [Test], [TestCase], [TestCaseSource], or [Theory] attributes");
                    Console.WriteLine("- Assembly doesn't use NUnit framework");
                }
                else
                {
                    var groupedByClass = testMethods
                        .GroupBy(m => m.DeclaringType?.FullName ?? "Unknown")
                        .OrderBy(g => g.Key);

                    foreach (var group in groupedByClass)
                    {
                        Console.WriteLine($"  Class: {group.Key}");
                        foreach (var method in group.OrderBy(m => m.Name))
                        {
                            var attributes = new System.Collections.Generic.List<string>();
                            if (method.GetCustomAttributes<TestAttribute>().Any())
                                attributes.Add("[Test]");
                            if (method.GetCustomAttributes<TestCaseAttribute>().Any())
                                attributes.Add("[TestCase]");
                            if (method.GetCustomAttributes<TestCaseSourceAttribute>().Any())
                                attributes.Add("[TestCaseSource]");
                            if (method.GetCustomAttributes<TheoryAttribute>().Any())
                                attributes.Add("[Theory]");

                            Console.WriteLine($"    - {method.Name} ({string.Join(", ", attributes)})");
                        }
                        Console.WriteLine();
                    }
                }

                Console.WriteLine("Discovery test completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine();
                Console.WriteLine("Stack trace:");
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}
