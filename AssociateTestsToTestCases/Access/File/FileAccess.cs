using System;
using System.IO;
using Minimatch;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using AssociateTestsToTestCases.Access.File.Strategy;

namespace AssociateTestsToTestCases.Access.File
{
    public class FileAccess : IFileAccess
    {
        private readonly AssemblyHelper _assemblyHelper;
        private readonly ITestFrameworkStrategy _testFrameWorkStrategy;

        public FileAccess(AssemblyHelper assemblyHelper, ITestFrameworkStrategy testFrameWorkStrategy)
        {
            _assemblyHelper = assemblyHelper;
            _testFrameWorkStrategy = testFrameWorkStrategy;
        }

        public MethodInfo[] ListTestMethods(string[] testAssemblyPaths)
        {
            var testMethods = new List<MethodInfo>();

            foreach (var testAssemblyPath in testAssemblyPaths)
            {
                try
                {
                    var testAssembly = _assemblyHelper.LoadFrom(testAssemblyPath);
                    testMethods.AddRange(_testFrameWorkStrategy.RetrieveTestMethods(testAssembly));
                }
                catch (ReflectionTypeLoadException ex)
                {
                    // Some types couldn't be loaded (likely missing dependencies like NUnit framework)
                    // Try to discover tests from successfully loaded types using fallback mechanism
                    var loadedTypes = ex.Types.Where(t => t != null).ToList();
                    
                    // Use the strategy's fallback by manually checking types
                    foreach (var type in loadedTypes)
                    {
                        try
                        {
                            // Check if it's a test fixture using fallback (by attribute name)
                            var isTestFixture = false;
                            try
                            {
                                var attributes = type.GetCustomAttributesData();
                                isTestFixture = attributes.Any(a => 
                                    a.AttributeType.FullName == "NUnit.Framework.TestFixtureAttribute");
                            }
                            catch
                            {
                                // Skip if we can't check attributes
                                continue;
                            }

                            if (isTestFixture)
                            {
                                var methods = type.GetMethods()
                                    .Where(method =>
                                    {
                                        try
                                        {
                                            var methodAttributes = method.GetCustomAttributesData();
                                            return methodAttributes.Any(a => 
                                                a.AttributeType.FullName == "NUnit.Framework.TestAttribute" ||
                                                a.AttributeType.FullName == "NUnit.Framework.TestCaseAttribute" ||
                                                a.AttributeType.FullName == "NUnit.Framework.TestCaseSourceAttribute" ||
                                                a.AttributeType.FullName == "NUnit.Framework.TheoryAttribute");
                                        }
                                        catch
                                        {
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
            }

            return testMethods.ToArray();
        }

        public List<DuplicateTestMethod> ListDuplicateTestMethods(MethodInfo[] testMethods)
        {
            var duplicateTestMethods = new List<DuplicateTestMethod>();

            var duplicates = testMethods.Select(x => x.Name).GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            foreach (var duplicate in duplicates)
            {
                duplicateTestMethods.Add(new DuplicateTestMethod(duplicate, testMethods.Where(y => y.Name.Equals(duplicate)).ToArray()));
            }

            return duplicateTestMethods;
        }

        public string[] ListTestAssemblyPaths(string directory, string[] minimatchPatterns)
        {
            var files = ListAllAccessibleFilesInDirectory(directory);

            var matchingFilesToBeIncluded = new List<string>();
            foreach (var minimatchPattern in minimatchPatterns.Where(minimatchPattern => !minimatchPattern.StartsWith("!")))
            {
                var mm = new Minimatcher(minimatchPattern, new Minimatch.Options() { AllowWindowsPaths = true, IgnoreCase = true });
                matchingFilesToBeIncluded.AddRange(mm.Filter(files));
            }

            var noMatchingFilesFound = matchingFilesToBeIncluded.Count == 0;
            if (noMatchingFilesFound)
            {
                return null;
            }

            var matchingFilesToBeExcluded = new List<string>();
            foreach (var minimatchPattern in minimatchPatterns.Where(minimatchPattern => minimatchPattern.StartsWith("!")))
            {
                var actualMinimatchPattern = minimatchPattern.TrimStart('!');
                var mm = new Minimatcher(actualMinimatchPattern, new Minimatch.Options() { AllowWindowsPaths = true, IgnoreCase = true });
                matchingFilesToBeExcluded.AddRange(mm.Filter(files));
            }

            matchingFilesToBeIncluded.RemoveAll(x => matchingFilesToBeExcluded.Contains(x));

            return matchingFilesToBeIncluded.ToArray();
        }

        private string[] ListAllAccessibleFilesInDirectory(string directory)
        {
            var files = new List<string>(Directory.GetFiles(directory, "*.*", SearchOption.TopDirectoryOnly));

            foreach (var subDir in Directory.GetDirectories(directory))
            {
                try
                {
                    files.AddRange(ListAllAccessibleFilesInDirectory(subDir));
                }
                catch (UnauthorizedAccessException)
                {
                    //Ignored by design, see https://stackoverflow.com/a/19137152. Comment is here to suppress analyzer warnings.
                }
            }

            return files.ToArray();
        }
    }
}
