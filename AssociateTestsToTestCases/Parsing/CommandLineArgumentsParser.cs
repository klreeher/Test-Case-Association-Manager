using System;
using System.Linq;
using CommandLine;
using AssociateTestsToTestCases.Message;
using AssociateTestsToTestCases.Validation;
using AssociateTestsToTestCases.Access.Output;

namespace AssociateTestsToTestCases.Parsing
{
    public class CommandLineArgumentsParser
    {
        private readonly IOutputAccess _commandLineAccess;
        private readonly Messages _messages;

        public CommandLineArgumentsParser(IOutputAccess commandlineAccess, Messages messages)
        {
            _commandLineAccess = commandlineAccess;
            _messages = messages;
        }

        public InputOptions Parse(string[] args)
        {
            _commandLineAccess.WriteToConsole(_messages.Stages.Argument.Status, _messages.Types.Stage);

            var _inputOptions = new InputOptions();
            
            // Load configuration from file first (command line will override)
            ConfigurationReader.LoadFromConfigFile(_inputOptions);
            
            using (var parser = new Parser(config => config.HelpWriter = null))
            {
                parser.ParseArguments<Options>(args)
                    .WithParsed(o =>
                    {
                        _inputOptions.CsvOut = o.CsvOut;
                        _inputOptions.TestType = o.TestType;
                        _inputOptions.DebugMode = o.DebugMode;
                        _inputOptions.Directory = o.Directory;
                        _inputOptions.ProjectName = o.ProjectName;
                        _inputOptions.CollectionUri = o.CollectionUri;
                        _inputOptions.ValidationOnly = o.ValidationOnly;
                        _inputOptions.VerboseLogging = o.VerboseLogging;
                        _inputOptions.PersonalAccessToken = o.PersonalAccessToken;
                        _inputOptions.MinimatchPatterns = o.MinimatchPatterns
                            .Split(';')
                            .Select(s => s.ToLowerInvariant())
                            .ToArray();
                        _inputOptions.TestFrameworkType = o.TestFrameworkType;
                        _inputOptions.TestNameFormat = string.IsNullOrWhiteSpace(o.TestNameFormat) 
                            ? _inputOptions.TestNameFormat 
                            : o.TestNameFormat;
                        _inputOptions.ExpandParameterizedTests = o.ExpandParameterizedTests;

                        var csvMode = !string.IsNullOrWhiteSpace(o.CsvOut);
                        if (!csvMode)
                        {
                            _inputOptions.TestPlanId = int.Parse(o.TestPlanId);
                            _inputOptions.TestSuiteId = int.Parse(o.TestSuiteId);
                        }
                    });

            }

            var csvMode = !string.IsNullOrWhiteSpace(_inputOptions.CsvOut);
            if (!csvMode)
            {
                ValidateInputOptions(_inputOptions);
            }
            else
            {
                // Minimal validation so CSV mode still fails fast on the basics
                if (string.IsNullOrWhiteSpace(_inputOptions.Directory))
                    throw new InvalidOperationException("Directory is required for CSV export.");
                if (_inputOptions.MinimatchPatterns == null || _inputOptions.MinimatchPatterns.Length == 0)
                    throw new InvalidOperationException("MinimatchPatterns is required for CSV export.");
                if (string.IsNullOrWhiteSpace(_inputOptions.TestFrameworkType))
                    throw new InvalidOperationException("TestFrameworkType is required for CSV export.");
            }


            _commandLineAccess.WriteToConsole(_messages.Stages.Argument.Success, _messages.Types.Success);
            return _inputOptions;
        }

        private void ValidateInputOptions(InputOptions _inputOptions)
        {
            var validationResults = new InputOptionsValidator().Validate(_inputOptions);
            if (validationResults.IsValid)
            {
                return;
            }

            foreach (var error in validationResults.Errors)
            {
                _commandLineAccess.WriteToConsole(error.ErrorMessage, _messages.Types.Failure);
            }

            _commandLineAccess.WriteToConsole(_messages.Stages.Argument.Failure, _messages.Types.Error);

            throw new InvalidOperationException(_messages.Stages.Argument.Failure);
        }
    }
}
