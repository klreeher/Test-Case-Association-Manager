using System;
using System.Text;
using AssociateTestsToTestCases.Manager.File;

namespace AssociateTestsToTestCases
{
    public static class TestNameFormatter
    {
        private const string DefaultFormat = "{FullClassName}.{Name}";
        private const string FullClassNamePlaceholder = "{FullClassName}";
        private const string NamePlaceholder = "{Name}";
        private const string AssemblyNamePlaceholder = "{AssemblyName}";

        /// <summary>
        /// Formats a test method name using the provided template string.
        /// </summary>
        /// <param name="testMethod">The test method to format</param>
        /// <param name="template">Template string with placeholders: {FullClassName}, {Name}, {AssemblyName}</param>
        /// <returns>Formatted test method name</returns>
        public static string Format(TestMethod testMethod, string template = null)
        {
            if (testMethod == null)
                throw new ArgumentNullException(nameof(testMethod));

            var format = string.IsNullOrWhiteSpace(template) ? DefaultFormat : template;

            var result = new StringBuilder(format);
            
            result.Replace(FullClassNamePlaceholder, testMethod.FullClassName ?? string.Empty);
            result.Replace(NamePlaceholder, testMethod.Name ?? string.Empty);
            result.Replace(AssemblyNamePlaceholder, testMethod.AssemblyName ?? string.Empty);

            return result.ToString();
        }
    }
}
