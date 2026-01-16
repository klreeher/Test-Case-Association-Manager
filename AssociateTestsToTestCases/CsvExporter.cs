using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AssociateTestsToTestCases.Export
{
    public static class CsvExporter
    {
        public static void Write(string csvPath, IEnumerable<AssociateTestsToTestCases.Manager.File.TestMethod> testMethods)
        {
            if (string.IsNullOrWhiteSpace(csvPath))
                throw new ArgumentException("csvPath is required", nameof(csvPath));

            var list = testMethods?.ToList() ?? throw new ArgumentNullException(nameof(testMethods));

            var dir = Path.GetDirectoryName(csvPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            using var writer = new StreamWriter(csvPath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.WriteLine("Title");

            foreach (var tm in list)
            {
                var title = $"{tm.FullClassName}.{tm.Name}";
                writer.WriteLine($"\"{Escape(title)}\"");
            }
        }

        private static string Escape(string value) => (value ?? string.Empty).Replace("\"", "\"\"");
    }
}
