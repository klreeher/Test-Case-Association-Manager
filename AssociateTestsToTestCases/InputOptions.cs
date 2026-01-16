namespace AssociateTestsToTestCases
{
    public class InputOptions
    {
        public bool DebugMode { get; set; }

        public string CsvOut { get; set; } // null = not in CSV mode

        public bool ValidationOnly { get; set; }
        public bool VerboseLogging { get; set; }

        public string TestType { get; set; }
        public string Directory { get; set; }
        public int TestPlanId { get; set; }
        public int TestSuiteId { get; set; }
        public string ProjectName { get; set; }
        public string CollectionUri { get; set; }
        public string PersonalAccessToken { get; set; }

        public string[] MinimatchPatterns { get; set; }

        public string TestFrameworkType { get; set; }

        public string TestNameFormat { get; set; } = "{FullClassName}.{Name}";

        public bool ExpandParameterizedTests { get; set; } = false;
    }
}
