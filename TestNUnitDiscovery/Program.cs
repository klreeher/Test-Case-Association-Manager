using System;
using System.IO;
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

                // Set up assembly resolution to look in the DLL's directory for dependencies
                var dllDirectory = Path.GetDirectoryName(Path.GetFullPath(dllPath));
                
                // Pre-load NUnit framework if it exists to handle version mismatches
                Assembly nunitAssembly = null;
                var nunitPath = Path.Combine(dllDirectory, "nunit.framework.dll");
                if (File.Exists(nunitPath))
                {
                    try
                    {
                        nunitAssembly = Assembly.LoadFrom(nunitPath);
                        Console.WriteLine($"Pre-loaded NUnit framework from: {nunitPath} (Version: {nunitAssembly.GetName().Version})");
                    }
                    catch (Exception nunitEx)
                    {
                        Console.WriteLine($"Warning: Could not pre-load NUnit framework: {nunitEx.Message}");
                    }
                }
                
                // Register assembly resolver BEFORE loading the test assembly
                AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
                {
                    var assemblyName = new AssemblyName(args.Name);
                    
                    // For NUnit, return the pre-loaded assembly regardless of version
                    if (assemblyName.Name.Equals("nunit.framework", StringComparison.OrdinalIgnoreCase) && nunitAssembly != null)
                    {
                        Console.WriteLine($"Resolving NUnit framework (requested: {assemblyName.Version}, using: {nunitAssembly.GetName().Version})");
                        return nunitAssembly;
                    }
                    
                    // Try exact match first
                    var potentialPath = Path.Combine(dllDirectory, assemblyName.Name + ".dll");
                    if (File.Exists(potentialPath))
                    {
                        return Assembly.LoadFrom(potentialPath);
                    }
                    var potentialPathExe = Path.Combine(dllDirectory, assemblyName.Name + ".exe");
                    if (File.Exists(potentialPathExe))
                    {
                        return Assembly.LoadFrom(potentialPathExe);
                    }
                    
                    return null;
                };

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
                    var loadedTypes = ex.Types.Where(t => t != null).ToList();
                    Console.WriteLine($"Successfully loaded {loadedTypes.Count} type(s) out of {ex.Types.Length} total.");
                    testMethods = new List<MethodInfo>();
                    
                    int testFixtureCount = 0;
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
                                // If that fails, try checking by attribute name
                                try
                                {
                                    var attributes = type.GetCustomAttributesData();
                                    isTestFixture = attributes.Any(a => 
                                        a.AttributeType.FullName == "NUnit.Framework.TestFixtureAttribute");
                                }
                                catch
                                {
                                    // Skip this type if we can't check attributes
                                    continue;
                                }
                            }

                            if (isTestFixture)
                            {
                                testFixtureCount++;
                                var methods = type.GetMethods()
                                    .Where(method =>
                                    {
                                        try
                                        {
                                            // Try using strongly-typed attributes first
                                            return method.GetCustomAttributes<TestAttribute>().Any() 
                                                || method.GetCustomAttributes<TestCaseAttribute>().Any()
                                                || method.GetCustomAttributes<TestCaseSourceAttribute>().Any()
                                                || method.GetCustomAttributes<TheoryAttribute>().Any();
                                        }
                                        catch
                                        {
                                            // If that fails (e.g., NUnit assembly not loaded), try by attribute name
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
                    Console.WriteLine($"Found {testFixtureCount} test fixture(s) in successfully loaded types.");
                }

                Console.WriteLine($"Found {testMethods.Count} test method(s):");
                Console.WriteLine();

                if (testMethods.Count == 0)
                {
                    Console.WriteLine("No test methods discovered. Possible reasons:");
                    Console.WriteLine("- No classes with [TestFixture] attribute");
                    Console.WriteLine("- No methods with [Test], [TestCase], [TestCaseSource], or [Theory] attributes");
                    Console.WriteLine("- Assembly doesn't use NUnit framework");
                    Console.WriteLine();
                    Console.WriteLine("Debug info: Listing all types in assembly...");
                    try
                    {
                        var allTypes = assembly.GetTypes();
                        Console.WriteLine($"Total types in assembly: {allTypes.Length}");
                        foreach (var type in allTypes.Take(20)) // Show first 20 types
                        {
                            Console.WriteLine($"  - {type.FullName}");
                        }
                        if (allTypes.Length > 20)
                        {
                            Console.WriteLine($"  ... and {allTypes.Length - 20} more");
                        }
                    }
                    catch (Exception debugEx)
                    {
                        Console.WriteLine($"Could not enumerate types: {debugEx.Message}");
                    }
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
