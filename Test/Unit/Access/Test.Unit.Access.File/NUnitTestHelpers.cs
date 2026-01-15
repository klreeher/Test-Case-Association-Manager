using NUnit.Framework;

namespace Test.Unit.Access.File
{
    [TestFixture]
    public class NUnitTestFixture
    {
        [Test]
        public void SimpleTest() { }

        [TestCase(1, 2)]
        [TestCase(3, 4)]
        public void ParameterizedTest(int a, int b) { }

        [TestCaseSource(nameof(TestData))]
        public void TestCaseSourceTest(int value) { }

        static int[] TestData = { 1, 2, 3 };

        [Theory]
        public void TheoryTest(int x) { }

        public void RegularMethod() { } // Should NOT be discovered
    }

    public class NonTestFixtureClass
    {
        [Test]
        public void TestInNonFixture() { } // Should NOT be discovered
    }
}
