using Moq;
using System;
using System.Linq;
using AutoFixture;
using FluentAssertions;
using AssociateTestsToTestCases;
using System.Collections.Generic;
using AssociateTestsToTestCases.Counter;
using AssociateTestsToTestCases.Message;
using Microsoft.VisualStudio.Services.Common;
using AssociateTestsToTestCases.Access.Output;
using AssociateTestsToTestCases.Access.DevOps;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using TestMethod = AssociateTestsToTestCases.Manager.File.TestMethod;

namespace Test.Unit.Access.DevOps
{
    [TestClass]
    public class AssociateTests
    {
        private const string AutomatedName = "Automated";
        private const string NotAutomatedName = "Not Automated";

        private const string AutomatedTestName = "Microsoft.VSTS.TCM.AutomatedTestName";
        private const string AutomatedTestIdName = "Microsoft.VSTS.TCM.AutomatedTestId";
        private const string AutomationStatusName = "Microsoft.VSTS.TCM.AutomationStatus";
        private const string AutomatedTestStorageName = "Microsoft.VSTS.TCM.AutomatedTestStorage";
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
        public void DevOpsAccess_Associate_TestCaseIsNull()
        {
            // Arrange
            var testManagementHttpClient = new Mock<TestManagementHttpClient>(new Uri("http://dummy.url"), new VssCredentials());
            var workItemTrackingHttpClient = new Mock<WorkItemTrackingHttpClient>(new Uri("http://dummy.url"), new VssCredentials());

            var outputAccess = new Mock<IOutputAccess>();

            var fixture = new Fixture();
            var messages = new Messages();
            var testCases = fixture.Create<TestCase[]>();
            var testMethods = fixture.Create<TestMethod[]>();

            var options = new InputOptions()
            {
                ValidationOnly = true,
                VerboseLogging = true,
                TestNameFormat = "{FullClassName}.{Name}" // Default format
            };
            var counter = new Counter();

            var azureDevOpsHttpClients = new AzureDevOpsHttpClients()
            {
                TestManagementHttpClient = testManagementHttpClient.Object,
                WorkItemTrackingHttpClient = workItemTrackingHttpClient.Object
            };

            var target = new DevOpsAccessFactory(azureDevOpsHttpClients, messages, outputAccess.Object, options, counter).Create();

            // Act
            var errorCount = target.Associate(testMethods, testCases.ToDictionary(v => v.Title, t => t));

            // Assert
            errorCount.Should().Be(testMethods.Length);
            counter.Error.Total.Should().Be(testMethods.Length);
            counter.Error.TestCaseNotFound.Should().Be(testMethods.Length);
            outputAccess.Verify(x => x.WriteToConsole(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(testMethods.Length));
        }

        [TestMethod]
        public void DevOpsAccess_Associate_AlreadyAssociated()
        {
            // Arrange
            var testManagementHttpClient = new Mock<TestManagementHttpClient>(new Uri("http://dummy.url"), new VssCredentials());
            var workItemTrackingHttpClient = new Mock<WorkItemTrackingHttpClient>(new Uri("http://dummy.url"), new VssCredentials());

            var outputAccess = new Mock<IOutputAccess>();

            var fixture = new Fixture();
            var messages = new Messages();
            var testMethods = fixture.Create<TestMethod[]>();
            
            var options = CreateInputOptions();
            var testCases = testMethods.Select(x => new TestCase(fixture.Create<int>(), TestNameFormatter.Format(x, options.TestNameFormat), AutomatedName, TestNameFormatter.Format(x, options.TestNameFormat))).ToArray();
            var counter = new Counter();

            var azureDevOpsHttpClients = new AzureDevOpsHttpClients()
            {
                TestManagementHttpClient = testManagementHttpClient.Object,
                WorkItemTrackingHttpClient = workItemTrackingHttpClient.Object
            };

            var target = new DevOpsAccessFactory(azureDevOpsHttpClients, messages, outputAccess.Object, options, counter).Create();

            // Act
            var errorCount = target.Associate(testMethods, testCases.ToDictionary(v => v.Title, t => t));

            // Assert
            errorCount.Should().Be(0);
            counter.Total.Should().Be(testMethods.Length);
            outputAccess.Verify(x => x.WriteToConsole(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        public void DevOpsAccess_Associate_WrongAssociationWhereOperationSuccessIsFalseAndVerboseLoggingIsFalse()
        {
            // Arrange
            var testManagementHttpClient = new Mock<TestManagementHttpClient>(new Uri("http://dummy.url"), new VssCredentials());
            var workItemTrackingHttpClient = new Mock<WorkItemTrackingHttpClient>(new Uri("http://dummy.url"), new VssCredentials());

            var outputAccess = new Mock<IOutputAccess>();

            var fixture = new Fixture();
            var messages = new Messages();
            var testMethods = fixture.Create<TestMethod[]>();
            var options = CreateInputOptions(verboseLogging: false);
            var testCases = testMethods.Select(x => new TestCase(fixture.Create<int>(), TestNameFormatter.Format(x, options.TestNameFormat), AutomatedName, string.Empty)).ToArray();

            var methodName = fixture.Create<string>();
            var assemblyName = fixture.Create<string>();
            var automatedTestId = fixture.Create<string>();
            var result = new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem
            {
                Fields = new Dictionary<string, object>()
                            {
                                { AutomationStatusName, AutomatedName },
                                { AutomatedTestIdName, automatedTestId },
                                { AutomatedTestStorageName, assemblyName },
                                { AutomatedTestName, methodName }
                            }
            };

            workItemTrackingHttpClient
                .Setup(x => x.UpdateWorkItemAsync(It.IsAny<JsonPatchDocument>(), It.IsAny<int>(), It.IsAny<bool?>(), null, null, null, null, default))
                .ReturnsAsync(result);
            var counter = new Counter();

            var azureDevOpsHttpClients = new AzureDevOpsHttpClients()
            {
                TestManagementHttpClient = testManagementHttpClient.Object,
                WorkItemTrackingHttpClient = workItemTrackingHttpClient.Object
            };

            var target = new DevOpsAccessFactory(azureDevOpsHttpClients, messages, outputAccess.Object, options, counter).Create();

            // Act
            var errorCount = target.Associate(testMethods, testCases.ToDictionary(v => v.Title, t => t));

            // Assert
            errorCount.Should().Be(testMethods.Length);
            counter.Total.Should().Be(0);
            counter.Success.FixedReference.Should().Be(0);
            counter.Error.OperationFailed.Should().Be(testMethods.Length);
            outputAccess.Verify(x => x.WriteToConsole(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(testMethods.Length));
        }

        [TestMethod]
        public void DevOpsAccess_Associate_WrongAssociationWhereOperationSuccessIsFalseAndVerboseLoggingIsTrue()
        {
            // Arrange
            var testManagementHttpClient = new Mock<TestManagementHttpClient>(new Uri("http://dummy.url"), new VssCredentials());
            var workItemTrackingHttpClient = new Mock<WorkItemTrackingHttpClient>(new Uri("http://dummy.url"), new VssCredentials());

            var outputAccess = new Mock<IOutputAccess>();

            var fixture = new Fixture();
            var messages = new Messages();
            var testMethods = fixture.Create<TestMethod[]>();
            var options = CreateInputOptions();
            var testCases = testMethods.Select(x => new TestCase(fixture.Create<int>(), TestNameFormatter.Format(x, options.TestNameFormat), AutomatedName, string.Empty)).ToArray();

            var methodName = fixture.Create<string>();
            var assemblyName = fixture.Create<string>();
            var automatedTestId = fixture.Create<string>();
            var result = new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem
            {
                Fields = new Dictionary<string, object>()
                            {
                                { AutomationStatusName, AutomatedName },
                                { AutomatedTestIdName, automatedTestId },
                                { AutomatedTestStorageName, assemblyName },
                                { AutomatedTestName, methodName }
                            }
            };

            workItemTrackingHttpClient
                .Setup(x => x.UpdateWorkItemAsync(It.IsAny<JsonPatchDocument>(), It.IsAny<int>(), It.IsAny<bool?>(), null, null, null, null, default))
                .ReturnsAsync(result);

            var counter = new Counter();

            var azureDevOpsHttpClients = new AzureDevOpsHttpClients()
            {
                TestManagementHttpClient = testManagementHttpClient.Object,
                WorkItemTrackingHttpClient = workItemTrackingHttpClient.Object
            };

            var target = new DevOpsAccessFactory(azureDevOpsHttpClients, messages, outputAccess.Object, options, counter).Create();

            // Act
            var errorCount = target.Associate(testMethods, testCases.ToDictionary(v => v.Title, t => t));

            // Assert
            errorCount.Should().Be(testMethods.Length);
            counter.Total.Should().Be(0);
            counter.Success.FixedReference.Should().Be(0);
            counter.Error.OperationFailed.Should().Be(testMethods.Length);
            outputAccess.Verify(x => x.WriteToConsole(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(testMethods.Length * 2));
        }

        [TestMethod]
        public void DevOpsAccess_Associate_WrongAssociationWhereOperationSuccessIsTrueAndVerboseLoggingIsFalse()
        {
            // Arrange
            var testManagementHttpClient = new Mock<TestManagementHttpClient>(new Uri("http://dummy.url"), new VssCredentials());
            var workItemTrackingHttpClient = new Mock<WorkItemTrackingHttpClient>(new Uri("http://dummy.url"), new VssCredentials());

            var outputAccess = new Mock<IOutputAccess>();

            var fixture = new Fixture();
            var messages = new Messages();
            var testMethods = fixture.CreateMany<TestMethod>(1).ToArray();
            var options = CreateInputOptions(verboseLogging: false);
            var testCases = testMethods.Select(x => new TestCase(fixture.Create<int>(), TestNameFormatter.Format(x, options.TestNameFormat), AutomatedName, string.Empty)).ToArray();
            var result = new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem
            {
                Fields = new Dictionary<string, object>()
                            {
                                { AutomationStatusName, AutomatedName },
                                { AutomatedTestIdName, testMethods[0].TempId },
                                { AutomatedTestStorageName, testMethods[0].AssemblyName },
                                { AutomatedTestName, TestNameFormatter.Format(testMethods[0], options.TestNameFormat) }
                            }
            };

            workItemTrackingHttpClient
                .Setup(x => x.UpdateWorkItemAsync(It.IsAny<JsonPatchDocument>(), It.IsAny<int>(), It.IsAny<bool?>(), null, null, null, null, default))
                .ReturnsAsync(result);
            var counter = new Counter();

            var azureDevOpsHttpClients = new AzureDevOpsHttpClients()
            {
                TestManagementHttpClient = testManagementHttpClient.Object,
                WorkItemTrackingHttpClient = workItemTrackingHttpClient.Object
            };

            var target = new DevOpsAccessFactory(azureDevOpsHttpClients, messages, outputAccess.Object, options, counter).Create();

            // Act
            var errorCount = target.Associate(testMethods, testCases.ToDictionary(v => v.Title, t => t));

            // Assert
            errorCount.Should().Be(0);
            counter.Total.Should().Be(1);
            counter.Success.FixedReference.Should().Be(1);
            counter.Success.Total.Should().Be(1);
            counter.Error.OperationFailed.Should().Be(0);
            outputAccess.Verify(x => x.WriteToConsole(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        public void DevOpsAccess_Associate_WrongAssociationWhereOperationSuccessIsTrueAndVerboseLoggingIsTrue()
        {
            // Arrange
            var testManagementHttpClient = new Mock<TestManagementHttpClient>(new Uri("http://dummy.url"), new VssCredentials());
            var workItemTrackingHttpClient = new Mock<WorkItemTrackingHttpClient>(new Uri("http://dummy.url"), new VssCredentials());

            var outputAccess = new Mock<IOutputAccess>();

            var fixture = new Fixture();
            var messages = new Messages();
            var testMethods = fixture.CreateMany<TestMethod>(1).ToArray();
            var options = CreateInputOptions();
            var testCases = testMethods.Select(x => new TestCase(fixture.Create<int>(), TestNameFormatter.Format(x, options.TestNameFormat), AutomatedName, string.Empty)).ToArray();
            var result = new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem
            {
                Fields = new Dictionary<string, object>()
                            {
                                { AutomationStatusName, AutomatedName },
                                { AutomatedTestIdName, testMethods[0].TempId },
                                { AutomatedTestStorageName, testMethods[0].AssemblyName },
                                { AutomatedTestName, TestNameFormatter.Format(testMethods[0], options.TestNameFormat) }
                            }
            };

            workItemTrackingHttpClient
                .Setup(x => x.UpdateWorkItemAsync(It.IsAny<JsonPatchDocument>(), It.IsAny<int>(), It.IsAny<bool?>(), null, null, null, null, default))
                .ReturnsAsync(result);
            var counter = new Counter();

            var azureDevOpsHttpClients = new AzureDevOpsHttpClients()
            {
                TestManagementHttpClient = testManagementHttpClient.Object,
                WorkItemTrackingHttpClient = workItemTrackingHttpClient.Object
            };

            var target = new DevOpsAccessFactory(azureDevOpsHttpClients, messages, outputAccess.Object, options, counter).Create();

            // Act
            var errorCount = target.Associate(testMethods, testCases.ToDictionary(v => v.Title, t => t));

            // Assert
            errorCount.Should().Be(0);
            counter.Error.OperationFailed.Should().Be(0);
            counter.Total.Should().Be(testMethods.Length);
            counter.Success.FixedReference.Should().Be(testMethods.Length);
            outputAccess.Verify(x => x.WriteToConsole(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(testMethods.Length * 2));
        }

        [TestMethod]
        public void DevOpsAccess_Associate_OperationSuccessIsFalse()
        {
            // Arrange
            var testManagementHttpClient = new Mock<TestManagementHttpClient>(new Uri("http://dummy.url"), new VssCredentials());
            var workItemTrackingHttpClient = new Mock<WorkItemTrackingHttpClient>(new Uri("http://dummy.url"), new VssCredentials());

            var outputAccess = new Mock<IOutputAccess>();

            var fixture = new Fixture();
            var messages = new Messages();
            var testMethods = fixture.Create<TestMethod[]>();
            var options = CreateInputOptions();
            var testCases = testMethods.Select(x => new TestCase(fixture.Create<int>(), TestNameFormatter.Format(x, options.TestNameFormat), NotAutomatedName, string.Empty)).ToArray();

            var methodName = fixture.Create<string>();
            var assemblyName = fixture.Create<string>();
            var automatedTestId = fixture.Create<string>();
            var result = new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem
            {
                Fields = new Dictionary<string, object>()
                            {
                                { AutomationStatusName, AutomatedName },
                                { AutomatedTestIdName, automatedTestId },
                                { AutomatedTestStorageName, assemblyName },
                                { AutomatedTestName, methodName }
                            }
            };

            workItemTrackingHttpClient
                .Setup(x => x.UpdateWorkItemAsync(It.IsAny<JsonPatchDocument>(), It.IsAny<int>(), It.IsAny<bool?>(), null, null, null, null, default))
                .ReturnsAsync(result);

            var counter = new Counter();

            var azureDevOpsHttpClients = new AzureDevOpsHttpClients()
            {
                TestManagementHttpClient = testManagementHttpClient.Object,
                WorkItemTrackingHttpClient = workItemTrackingHttpClient.Object
            };

            var target = new DevOpsAccessFactory(azureDevOpsHttpClients, messages, outputAccess.Object, options, counter).Create();

            // Act
            var errorCount = target.Associate(testMethods, testCases.ToDictionary(v => v.Title, t => t));

            // Assert
            errorCount.Should().Be(testMethods.Length);
            counter.Total.Should().Be(0);
            counter.Success.FixedReference.Should().Be(0);
            counter.Error.TestCaseNotFound.Should().Be(0);
            counter.Error.OperationFailed.Should().Be(testMethods.Length);
            outputAccess.Verify(x => x.WriteToConsole(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(testMethods.Length));
        }

        [TestMethod]
        public void DevOpsAccess_Associate_OperationSuccessIsTrueWhereVerboseLoggingIsFalse()
        {
            // Arrange
            var testManagementHttpClient = new Mock<TestManagementHttpClient>(new Uri("http://dummy.url"), new VssCredentials());
            var workItemTrackingHttpClient = new Mock<WorkItemTrackingHttpClient>(new Uri("http://dummy.url"), new VssCredentials());

            var outputAccess = new Mock<IOutputAccess>();

            var fixture = new Fixture();
            var messages = new Messages();

            var testMethods = fixture.CreateMany<TestMethod>(1).ToArray();
            var options = CreateInputOptions(verboseLogging: false);
            var testCases = testMethods.Select(x => new TestCase(fixture.Create<int>(), TestNameFormatter.Format(x, options.TestNameFormat), NotAutomatedName, string.Empty)).ToArray();
            var result = new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem
            {
                Fields = new Dictionary<string, object>()
                            {
                                { AutomationStatusName, AutomatedName },
                                { AutomatedTestIdName, testMethods[0].TempId },
                                { AutomatedTestStorageName, testMethods[0].AssemblyName },
                                { AutomatedTestName, TestNameFormatter.Format(testMethods[0], options.TestNameFormat) }
                            }
            };

            workItemTrackingHttpClient
                .Setup(x => x.UpdateWorkItemAsync(It.IsAny<JsonPatchDocument>(), It.IsAny<int>(), It.IsAny<bool?>(), null, null, null, null, default))
                .ReturnsAsync(result);
            var counter = new Counter();

            var azureDevOpsHttpClients = new AzureDevOpsHttpClients()
            {
                TestManagementHttpClient = testManagementHttpClient.Object,
                WorkItemTrackingHttpClient = workItemTrackingHttpClient.Object
            };

            var target = new DevOpsAccessFactory(azureDevOpsHttpClients, messages, outputAccess.Object, options, counter).Create();

            // Act
            var errorCount = target.Associate(testMethods, testCases.ToDictionary(v => v.Title, t => t));

            // Assert
            errorCount.Should().Be(0);
            counter.Total.Should().Be(testMethods.Length);
            counter.Success.FixedReference.Should().Be(0);
            counter.Error.OperationFailed.Should().Be(0);
            counter.Error.TestCaseNotFound.Should().Be(0);
            outputAccess.Verify(x => x.WriteToConsole(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        public void DevOpsAccess_Associate_OperationSuccessIsTrueWhereVerboseLoggingIsTrue()
        {
            // Arrange
            var testManagementHttpClient = new Mock<TestManagementHttpClient>(new Uri("http://dummy.url"), new VssCredentials());
            var workItemTrackingHttpClient = new Mock<WorkItemTrackingHttpClient>(new Uri("http://dummy.url"), new VssCredentials());

            var outputAccess = new Mock<IOutputAccess>();

            var fixture = new Fixture();
            var messages = new Messages();

            var testMethods = fixture.CreateMany<TestMethod>(1).ToArray();
            var options = CreateInputOptions();
            var testCases = testMethods.Select(x => new TestCase(fixture.Create<int>(), TestNameFormatter.Format(x, options.TestNameFormat), NotAutomatedName, string.Empty)).ToArray();
            var result = new Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem
            {
                Fields = new Dictionary<string, object>()
                            {
                                { AutomationStatusName, AutomatedName },
                                { AutomatedTestIdName, testMethods[0].TempId },
                                { AutomatedTestStorageName, testMethods[0].AssemblyName },
                                { AutomatedTestName, TestNameFormatter.Format(testMethods[0], options.TestNameFormat) }
                            }
            };

            workItemTrackingHttpClient
                .Setup(x => x.UpdateWorkItemAsync(It.IsAny<JsonPatchDocument>(), It.IsAny<int>(), It.IsAny<bool?>(), null, null, null, null, default))
                .ReturnsAsync(result);

            var counter = new Counter();

            var azureDevOpsHttpClients = new AzureDevOpsHttpClients()
            {
                TestManagementHttpClient = testManagementHttpClient.Object,
                WorkItemTrackingHttpClient = workItemTrackingHttpClient.Object
            };

            var target = new DevOpsAccessFactory(azureDevOpsHttpClients, messages, outputAccess.Object, options, counter).Create();

            // Act
            var errorCount = target.Associate(testMethods, testCases.ToDictionary(v => v.Title, t => t));

            // Assert
            errorCount.Should().Be(0);
            counter.Success.FixedReference.Should().Be(0);
            counter.Error.OperationFailed.Should().Be(0);
            counter.Error.TestCaseNotFound.Should().Be(0);
            counter.Total.Should().Be(testMethods.Length);
            outputAccess.Verify(x => x.WriteToConsole(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(testMethods.Length));
        }
    }
}
