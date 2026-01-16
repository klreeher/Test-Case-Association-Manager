using Moq;
using System;
using AutoFixture;
using System.Linq;
using FluentAssertions;
using AssociateTestsToTestCases;
using AssociateTestsToTestCases.Counter;
using AssociateTestsToTestCases.Message;
using Microsoft.VisualStudio.Services.Common;
using AssociateTestsToTestCases.Access.Output;
using AssociateTestsToTestCases.Access.DevOps;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using TestMethod = AssociateTestsToTestCases.Manager.File.TestMethod;

namespace Test.Unit.Access.DevOps
{
    [TestClass]
    public class ListTestCasesWithNotAvailableTestMethodsTests
    {
        private const string AutomatedName = "Automated";
        private const string NotAutomatedName = "Not Automated";
        private const string DefaultTestNameFormat = "{FullClassName}.{Name}";

        private InputOptions CreateInputOptions(bool validationOnly = true, bool verboseLogging = true, string testNameFormat = null)
        {
            return new InputOptions()
            {
                ValidationOnly = validationOnly,
                VerboseLogging = verboseLogging,
                TestNameFormat = testNameFormat ?? DefaultTestNameFormat
            };
        }

        [TestMethod]
        public void DevOpsAccess_ListTestCasesWithNotAvailableTestMethods_EmptyListTestCasesWithNotAvailableTestMethodsWhereAutomationStatusIsEqualToAutomatedName()
        {
            // Arrange
            var testManagementHttpClient = new Mock<TestManagementHttpClient>(new Uri("http://dummy.url"), new VssCredentials());
            var workItemTrackingHttpClient = new Mock<WorkItemTrackingHttpClient>(new Uri("http://dummy.url"), new VssCredentials());

            var outputAccess = new Mock<IOutputAccess>();

            var fixture = new Fixture();
            var messages = new Messages();

            var options = CreateInputOptions();
            
            // Create test methods first, then create test cases with titles matching the formatted names
            var testMethods = fixture.Create<TestMethod[]>();
            var testCases = testMethods.Select(x => new TestCase(
                fixture.Create<int>(), 
                TestNameFormatter.Format(x, options.TestNameFormat), 
                AutomatedName, 
                string.Empty)).ToArray();
            var counter = new Counter();

            var azureDevOpsHttpClients = new AzureDevOpsHttpClients()
            {
                TestManagementHttpClient = testManagementHttpClient.Object,
                WorkItemTrackingHttpClient = workItemTrackingHttpClient.Object
            };

            var target = new DevOpsAccessFactory(azureDevOpsHttpClients, messages, outputAccess.Object, options, counter).Create();

            // Act
            var actual = target.ListTestCasesWithNotAvailableTestMethods(testMethods, testCases);

            // Assert
            actual.Count.Should().Be(0);
        }

        [TestMethod]
        public void DevOpsAccess_ListTestCasesWithNotAvailableTestMethods_EmptyListTestCasesWithNotAvailableTestMethodsWhereAutomationStatusIsNotEqualToAutomatedName()
        {
            // Arrange
            var testManagementHttpClient = new Mock<TestManagementHttpClient>(new Uri("http://dummy.url"), new VssCredentials());
            var workItemTrackingHttpClient = new Mock<WorkItemTrackingHttpClient>(new Uri("http://dummy.url"), new VssCredentials());

            var outputAccess = new Mock<IOutputAccess>();

            var fixture = new Fixture();
            var messages = new Messages();

            var options = CreateInputOptions();
            var testMethods = fixture.Create<TestMethod[]>();
            var testCases = fixture.Create<TestCase[]>();
            var counter = new Counter();

            var azureDevOpsHttpClients = new AzureDevOpsHttpClients()
            {
                TestManagementHttpClient = testManagementHttpClient.Object,
                WorkItemTrackingHttpClient = workItemTrackingHttpClient.Object
            };

            var target = new DevOpsAccessFactory(azureDevOpsHttpClients, messages, outputAccess.Object, options, counter).Create();

            // Act
            var actual = target.ListTestCasesWithNotAvailableTestMethods(testMethods, testCases);

            // Assert
            actual.Count.Should().Be(0);
        }

        [TestMethod]
        public void DevOpsAccess_ListTestCasesWithNotAvailableTestMethods_ListTestCasesWithNotAvailableTestMethods()
        {
            // Arrange
            var testManagementHttpClient = new Mock<TestManagementHttpClient>(new Uri("http://dummy.url"), new VssCredentials());
            var workItemTrackingHttpClient = new Mock<WorkItemTrackingHttpClient>(new Uri("http://dummy.url"), new VssCredentials());

            var outputAccess = new Mock<IOutputAccess>();

            var messages = new Messages();
            var fixture = new Fixture();

            var options = CreateInputOptions();
            // Create test cases with automated status, but test methods that don't match
            fixture.Customize<TestCase>(c => c.With(x => x.AutomationStatus, AutomatedName));
            var testCases = fixture.Create<TestCase[]>();
            // Create test methods with empty values so they won't match the test case titles
            var testMethods = testCases.Select(x => new TestMethod(string.Empty, string.Empty, string.Empty, Guid.NewGuid())).ToArray();
            var counter = new Counter();

            var azureDevOpsHttpClients = new AzureDevOpsHttpClients()
            {
                TestManagementHttpClient = testManagementHttpClient.Object,
                WorkItemTrackingHttpClient = workItemTrackingHttpClient.Object
            };

            var target = new DevOpsAccessFactory(azureDevOpsHttpClients, messages, outputAccess.Object, options, counter).Create();

            // Act
            var actual = target.ListTestCasesWithNotAvailableTestMethods(testMethods, testCases);

            // Assert
            actual.Count.Should().Be(3);
        }
    }
}
