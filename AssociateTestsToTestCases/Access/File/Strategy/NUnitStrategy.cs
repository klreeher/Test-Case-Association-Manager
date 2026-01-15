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
            return testAssembly.GetTypes()
                    .Where(type => type.GetCustomAttribute<TestFixtureAttribute>() != null)
                    .SelectMany(type => type.GetMethods()
                        .Where(method => method.GetCustomAttribute<TestAttribute>() != null 
                            || method.GetCustomAttribute<TestCaseAttribute>() != null
                            || method.GetCustomAttribute<TestCaseSourceAttribute>() != null
                            || method.GetCustomAttribute<TheoryAttribute>() != null));
        }
    }
}

