using EtlKit.Helper;

namespace EtlKit.TestHelper
{
    public class StringExtensionTests
    {
        [Fact]
        public void TestStringReplaceNotExist()
        {
            Assert.Equal("test", "test".ReplaceIgnoreCase("something", "replacement"));
        }

        [Fact]
        public void TestStringReplaceDiffCase()
        {
            Assert.Equal("test other thing", "test some thing".ReplaceIgnoreCase("SOME", "other"));
        }

        [Fact]
        public void TestStringReplaceExactLength()
        {
            Assert.Equal("test this", "test thing".ReplaceIgnoreCase("thing", "this"));
        }
    }
}
