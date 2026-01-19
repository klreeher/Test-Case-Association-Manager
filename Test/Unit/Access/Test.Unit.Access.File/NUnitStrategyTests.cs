using System;
using System.Linq;
using System.Reflection;
using AutoFixture;
using FluentAssertions;
using AssociateTestsToTestCases.Access.File.Strategy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NUnit.Framework;

namespace Test.Unit.Access.File
{
    [TestClass]
    public class NUnitStrategyTests
    {
        private NUnitStrategy _strategy;

        [TestInitialize]
        public void Setup()
        {
            _strategy = new NUnitStrategy();
        }

        [TestMethod]
        public void NUnitStrategy_RetrieveTestMethods_EmptyAssembly()
        {
            // Arrange
            var emptyAssembly = Assembly.GetCallingAssembly();
            var fixture = new Fixture();

            // Act
            var actual = _strategy.RetrieveTestMethods(emptyAssembly);

            // Assert
            actual.Should().BeEmpty();
        }

        [TestMethod]
        public void NUnitStrategy_RetrieveTestMethods_TestFixtureDiscovery()
        {
            // Arrange
            var testAssembly = Assembly.GetExecutingAssembly();

            // Act
            var actual = _strategy.RetrieveTestMethods(testAssembly);

            // Assert
            // Should find test methods from NUnitTestFixture class
            actual.Should().NotBeEmpty();
            var testFixtureTypes = actual.Select(m => m.DeclaringType)
                .Where(t => t != null && t.GetCustomAttribute<TestFixtureAttribute>() != null)
                .Distinct();
            testFixtureTypes.Should().Contain(typeof(NUnitTestFixture));
        }

        [TestMethod]
        public void NUnitStrategy_RetrieveTestMethods_TestAttribute()
        {
            // Arrange
            var testAssembly = Assembly.GetExecutingAssembly();

            // Act
            var actual = _strategy.RetrieveTestMethods(testAssembly);

            // Assert
            var simpleTest = actual.FirstOrDefault(m => m.Name == "SimpleTest");
            simpleTest.Should().NotBeNull();
            simpleTest.GetCustomAttribute<TestAttribute>().Should().NotBeNull();
        }

        [TestMethod]
        public void NUnitStrategy_RetrieveTestMethods_TestCaseAttribute()
        {
            // Arrange
            var testAssembly = Assembly.GetExecutingAssembly();

            // Act
            var actual = _strategy.RetrieveTestMethods(testAssembly);

            // Assert
            var parameterizedTest = actual.FirstOrDefault(m => m.Name == "ParameterizedTest");
            parameterizedTest.Should().NotBeNull();
            parameterizedTest.GetCustomAttributes<TestCaseAttribute>().Should().NotBeEmpty();
        }

        [TestMethod]
        public void NUnitStrategy_RetrieveTestMethods_TestCaseSourceAttribute()
        {
            // Arrange
            var testAssembly = Assembly.GetExecutingAssembly();

            // Act
            var actual = _strategy.RetrieveTestMethods(testAssembly);

            // Assert
            var testCaseSourceTest = actual.FirstOrDefault(m => m.Name == "TestCaseSourceTest");
            testCaseSourceTest.Should().NotBeNull();
            testCaseSourceTest.GetCustomAttribute<TestCaseSourceAttribute>().Should().NotBeNull();
        }

        [TestMethod]
        public void NUnitStrategy_RetrieveTestMethods_TheoryAttribute()
        {
            // Arrange
            var testAssembly = Assembly.GetExecutingAssembly();

            // Act
            var actual = _strategy.RetrieveTestMethods(testAssembly);

            // Assert
            var theoryTest = actual.FirstOrDefault(m => m.Name == "TheoryTest");
            theoryTest.Should().NotBeNull();
            theoryTest.GetCustomAttribute<TheoryAttribute>().Should().NotBeNull();
        }

        [TestMethod]
        public void NUnitStrategy_RetrieveTestMethods_ExcludesMethodsWithoutNUnitAttributes()
        {
            // Arrange
            var testAssembly = Assembly.GetExecutingAssembly();

            // Act
            var actual = _strategy.RetrieveTestMethods(testAssembly).ToList();

            // Assert
            actual.Should().NotBeEmpty("there should be at least one NUnit test method in this assembly");

            actual.Should().OnlyContain(m =>
                m.GetCustomAttributesData().Any(a =>
                    a.AttributeType.FullName != null &&
                    a.AttributeType.FullName.StartsWith("NUnit.Framework.", StringComparison.Ordinal)),
                "only methods with NUnit test attributes should be returned");
        }


        [TestMethod]
        public void NUnitStrategy_RetrieveTestMethods_ImplicitFixtureClassesIncluded()
        {
            // Arrange
            var testAssembly = Assembly.GetExecutingAssembly();

            // Act
            var actual = _strategy.RetrieveTestMethods(testAssembly).ToList();

            // Assert
            var testInNonFixture = actual.FirstOrDefault(m => m.Name == "TestInNonFixture");
            testInNonFixture.Should().NotBeNull("NUnit allows implicit fixtures (classes without [TestFixture]) when they contain test methods");
            testInNonFixture!.DeclaringType!.FullName.Should().Be("Test.Unit.Access.File.NonTestFixtureClass");
        }


        [TestMethod]
        public void NUnitStrategy_RetrieveTestMethods_AllAttributesCombined()
        {
            // Arrange
            var testAssembly = Assembly.GetExecutingAssembly();

            // Act
            var actual = _strategy.RetrieveTestMethods(testAssembly);

            // Assert
            var testFixtureMethods = actual.Where(m => m.DeclaringType == typeof(NUnitTestFixture)).ToList();
            testFixtureMethods.Should().HaveCountGreaterThanOrEqualTo(4); // SimpleTest, ParameterizedTest, TestCaseSourceTest, TheoryTest
            testFixtureMethods.Should().Contain(m => m.Name == "SimpleTest");
            testFixtureMethods.Should().Contain(m => m.Name == "ParameterizedTest");
            testFixtureMethods.Should().Contain(m => m.Name == "TestCaseSourceTest");
            testFixtureMethods.Should().Contain(m => m.Name == "TheoryTest");
        }

        [TestMethod]
        public void NUnitStrategy_TestFrameworkType_IsNUnit()
        {
            // Arrange & Act
            var strategy = new NUnitStrategy();

            // Assert
            strategy.TestFrameworkType.Should().Be(AssociateTestsToTestCases.TestFrameworkType.NUnit);
        }
    }
}
