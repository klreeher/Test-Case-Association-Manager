using System;
using System.IO;
using System.Text.Json;

namespace AssociateTestsToTestCases
{
    public static class ConfigurationReader
    {
        private const string ConfigFileName = "config.json";
        private const string AppSettingsFileName = "appsettings.json";

        public static void LoadFromConfigFile(InputOptions inputOptions, string workingDirectory = null)
        {
            if (inputOptions == null)
                throw new ArgumentNullException(nameof(inputOptions));

            var directory = string.IsNullOrWhiteSpace(workingDirectory) 
                ? Directory.GetCurrentDirectory() 
                : workingDirectory;

            // Try appsettings.json first, then config.json
            var configPath = Path.Combine(directory, AppSettingsFileName);
            if (!File.Exists(configPath))
            {
                configPath = Path.Combine(directory, ConfigFileName);
            }

            if (!File.Exists(configPath))
            {
                // No config file found - use defaults
                return;
            }

            try
            {
                var jsonContent = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<ConfigurationFile>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (config != null)
                {
                    // Config file values override defaults (command line will override these later)
                    if (!string.IsNullOrWhiteSpace(config.TestNameFormat))
                    {
                        inputOptions.TestNameFormat = config.TestNameFormat;
                    }

                    if (config.ExpandParameterizedTests.HasValue)
                    {
                        inputOptions.ExpandParameterizedTests = config.ExpandParameterizedTests.Value;
                    }
                }
            }
            catch (JsonException)
            {
                // Invalid JSON - silently ignore and use defaults
            }
            catch (Exception)
            {
                // Any other error - silently ignore and use defaults
            }
        }

        private class ConfigurationFile
        {
            public string TestNameFormat { get; set; }
            public bool? ExpandParameterizedTests { get; set; }
        }
    }
}
