using System.Reflection;
using AssociateTestsToTestCases.Access.DevOps;

namespace AssociateTestsToTestCases.Manager.File
{
    public interface IFileManager
    {
        bool TestMethodAssembliesContainNoTestMethods(string[] testAssemblyPaths);
        TestMethod[] GetTestMethods(string[] testAssemblyPaths,  bool allowDuplicates);
        string[] GetTestAssemblyPaths(string directory, string[] minimatchPatterns);
    }
}
