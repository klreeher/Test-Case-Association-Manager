using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AssociateTestsToTestCases.Manager.File;

namespace AssociateTestsToTestCases
{
    public static class CsvExporter
    {
        public static void Write(string csvPath, IEnumerable<TestMethod> testMethods, string testNameFormat = null)
        {
            if (string.IsNullOrWhiteSpace(csvPath))
                throw new ArgumentException("csvPath is required", nameof(csvPath));

            var testCaseList = testMethods?.ToList()
                               ?? throw new ArgumentNullException(nameof(testMethods));

            var dir = Path.GetDirectoryName(csvPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            using var writer = new StreamWriter(
                csvPath,
                false,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            // Header
            writer.WriteLine("Title,Assembly,TestClass");

            foreach (var tm in testCaseList)
            {
                var title = TestNameFormatter.Format(tm, testNameFormat);
                var assembly = tm.AssemblyName;
                var testClass = tm.FullClassName;

                writer.WriteLine(
                    $"\"{Escape(title)}\",\"{Escape(assembly)}\",\"{Escape(testClass)}\"");
            }
        }

        private static string Escape(string value) => (value ?? string.Empty).Replace("\"", "\"\"");
    }
}
