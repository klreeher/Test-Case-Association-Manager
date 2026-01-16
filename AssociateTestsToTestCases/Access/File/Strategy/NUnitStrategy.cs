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
            }
            catch (ReflectionTypeLoadException ex)
            {
                // Some types couldn't be loaded (likely missing dependencies like NUnit framework)
                // Use successfully loaded types only
                types = ex.Types.Where(t => t != null).ToArray();
            }

            return types
                    .Where(type => IsTestFixture(type))
                    .SelectMany(type => type.GetMethods()
                        .Where(method => IsTestMethod(method)));
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

