using System;
using System.IO;
using System.Linq;
using System.Text;
using AutoFixture;
using FluentAssertions;
using AssociateTestsToTestCases;
using AssociateTestsToTestCases.Manager.File;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test.Unit.AssociateTestsToTestCases
{
    [TestClass]
    public class CsvExporterTests
    {
        private readonly Fixture _fixture = new Fixture();

        [TestMethod]
        public void CsvExporter_Write_NullCsvPath_ThrowsArgumentException()
        {
            // Arrange
            var testMethods = _fixture.CreateMany<TestMethod>(3).ToArray();

            // Act
            Action actual = () => CsvExporter.Write(null, testMethods);

            // Assert
            actual.Should().Throw<ArgumentException>()
                .WithMessage("csvPath is required (Parameter 'csvPath')");
        }

        [TestMethod]
        public void CsvExporter_Write_EmptyCsvPath_ThrowsArgumentException()
        {
            // Arrange
            var testMethods = _fixture.CreateMany<TestMethod>(3).ToArray();

            // Act
            Action actual = () => CsvExporter.Write(string.Empty, testMethods);

            // Assert
            actual.Should().Throw<ArgumentException>()
                .WithMessage("csvPath is required (Parameter 'csvPath')");
        }

        [TestMethod]
        public void CsvExporter_Write_WhitespaceCsvPath_ThrowsArgumentException()
        {
            // Arrange
            var testMethods = _fixture.CreateMany<TestMethod>(3).ToArray();

            // Act
            Action actual = () => CsvExporter.Write("   ", testMethods);

            // Assert
            actual.Should().Throw<ArgumentException>()
                .WithMessage("csvPath is required (Parameter 'csvPath')");
        }

        [TestMethod]
        public void CsvExporter_Write_NullTestMethods_ThrowsArgumentNullException()
        {
            // Arrange
            var csvPath = Path.Combine(Path.GetTempPath(), "test.csv");

            // Act
            Action actual = () => CsvExporter.Write(csvPath, null);

            // Assert
            actual.Should().Throw<ArgumentNullException>()
                .WithMessage("Value cannot be null. (Parameter 'testMethods')");
        }

        [TestMethod]
        public void CsvExporter_Write_ValidTestMethods_CreatesFile()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var csvPath = Path.Combine(tempDir, "test.csv");
            var testMethods = new[]
            {
                new TestMethod("TestMethod1", "TestAssembly.dll", "Namespace.Class1", Guid.NewGuid()),
                new TestMethod("TestMethod2", "TestAssembly.dll", "Namespace.Class2", Guid.NewGuid())
            };

            try
            {
                // Act
                CsvExporter.Write(csvPath, testMethods);

                // Assert
                File.Exists(csvPath).Should().BeTrue();
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestMethod]
        public void CsvExporter_Write_ValidTestMethods_CreatesDirectory()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var subDir = Path.Combine(tempDir, "subdir");
            var csvPath = Path.Combine(subDir, "test.csv");
            var testMethods = new[]
            {
                new TestMethod("TestMethod1", "TestAssembly.dll", "Namespace.Class1", Guid.NewGuid())
            };

            try
            {
                // Act
                CsvExporter.Write(csvPath, testMethods);

                // Assert
                Directory.Exists(subDir).Should().BeTrue();
                File.Exists(csvPath).Should().BeTrue();
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestMethod]
        public void CsvExporter_Write_ValidTestMethods_ContainsHeader()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var csvPath = Path.Combine(tempDir, "test.csv");
            var testMethods = new[]
            {
                new TestMethod("TestMethod1", "TestAssembly.dll", "Namespace.Class1", Guid.NewGuid())
            };

            try
            {
                // Act
                CsvExporter.Write(csvPath, testMethods);

                // Assert
                var content = File.ReadAllText(csvPath);
                content.Should().StartWith("Title,Assembly");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestMethod]
        public void CsvExporter_Write_ValidTestMethods_ContainsTitleAndAssemblyColumns()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var csvPath = Path.Combine(tempDir, "test.csv");
            var testMethods = new[]
            {
                new TestMethod("TestMethod1", "TestAssembly.dll", "Namespace.Class1", Guid.NewGuid()),
                new TestMethod("TestMethod2", "TestAssembly2.dll", "Namespace.Class2", Guid.NewGuid())
            };

            try
            {
                // Act
                CsvExporter.Write(csvPath, testMethods);

                // Assert
                var lines = File.ReadAllLines(csvPath);
                lines.Length.Should().Be(3); // Header + 2 data rows
                lines[0].Should().Be("Title,Assembly");
                lines[1].Should().Contain("TestAssembly.dll");
                lines[2].Should().Contain("TestAssembly2.dll");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestMethod]
        public void CsvExporter_Write_ValidTestMethods_EscapesQuotes()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var csvPath = Path.Combine(tempDir, "test.csv");
            var testMethods = new[]
            {
                new TestMethod("TestMethod\"WithQuotes\"", "Test\"Assembly\".dll", "Namespace.Class1", Guid.NewGuid())
            };

            try
            {
                // Act
                CsvExporter.Write(csvPath, testMethods);

                // Assert
                var content = File.ReadAllText(csvPath);
                content.Should().Contain("\"\"");
                content.Should().NotContain("\"TestMethod\"WithQuotes\"");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestMethod]
        public void CsvExporter_Write_EmptyTestMethods_CreatesFileWithHeaderOnly()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var csvPath = Path.Combine(tempDir, "test.csv");
            var testMethods = Array.Empty<TestMethod>();

            try
            {
                // Act
                CsvExporter.Write(csvPath, testMethods);

                // Assert
                var lines = File.ReadAllLines(csvPath);
                lines.Length.Should().Be(1);
                lines[0].Should().Be("Title,Assembly");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }
    }
}
