using EtlKit.Common;

namespace EtlKit.TestHelper
{
    public class TableNameDescriptorTests
    {
        [Fact]
        public void SqlServerWithSchema()
        {
            var desc = new ObjectNameDescriptor("dbo.Test", "[", "]");

            Assert.Equal("[dbo]", desc.QuotedSchemaName);
            Assert.Equal("[Test]", desc.QuotedObjectName);
            Assert.Equal("[dbo].[Test]", desc.QuotedFullName);
            Assert.Equal("dbo", desc.UnquotedSchemaName);
            Assert.Equal("Test", desc.UnquotedObjectName);
            Assert.Equal("dbo.Test", desc.UnquotedFullName);
        }

        [Fact]
        public void SqlServerSchemaWithDot()
        {
            var desc = new ObjectNameDescriptor("[Foo.Bar].[Test]", "[", "]");

            Assert.Equal("[Foo.Bar]", desc.QuotedSchemaName);
            Assert.Equal("[Test]", desc.QuotedObjectName);
            Assert.Equal("[Foo.Bar].[Test]", desc.QuotedFullName);
            Assert.Equal("Foo.Bar", desc.UnquotedSchemaName);
            Assert.Equal("Test", desc.UnquotedObjectName);
            Assert.Equal("Foo.Bar.Test", desc.UnquotedFullName);
        }

        [Fact]
        public void SqlServerNoSchema()
        {
            var desc = new ObjectNameDescriptor("Test", "[", "]");

            Assert.Equal("", desc.QuotedSchemaName);
            Assert.Equal("[Test]", desc.QuotedObjectName);
            Assert.Equal("[Test]", desc.QuotedFullName);
            Assert.Equal("Test", desc.UnquotedObjectName);
            Assert.Equal("Test", desc.UnquotedFullName);
        }

        [Fact]
        public void SqlServerNoSchemaWithQuotation()
        {
            var desc = new ObjectNameDescriptor("[Test]", "[", "]");

            Assert.Equal("", desc.QuotedSchemaName);
            Assert.Equal("[Test]", desc.QuotedObjectName);
            Assert.Equal("[Test]", desc.QuotedFullName);
            Assert.Equal("Test", desc.UnquotedObjectName);
            Assert.Equal("Test", desc.UnquotedFullName);
        }

        [Fact]
        public void PostgresWithSchema()
        {
            var desc = new ObjectNameDescriptor("public.Test", @"""", @"""");

            Assert.Equal(@"""public""", desc.QuotedSchemaName);
            Assert.Equal(@"""Test""", desc.QuotedObjectName);
            Assert.Equal(@"""public"".""Test""", desc.QuotedFullName);
            Assert.Equal(@"Test", desc.UnquotedObjectName);
            Assert.Equal(@"public.Test", desc.UnquotedFullName);
        }

        [Fact]
        public void PostgresWithSchemaAndQuotation()
        {
            var desc = new ObjectNameDescriptor(@"""public"".""Test""", @"""", @"""");

            Assert.Equal(@"""public""", desc.QuotedSchemaName);
            Assert.Equal(@"""Test""", desc.QuotedObjectName);
            Assert.Equal(@"""public"".""Test""", desc.QuotedFullName);
            Assert.Equal(@"Test", desc.UnquotedObjectName);
            Assert.Equal(@"public.Test", desc.UnquotedFullName);
        }

        [Fact]
        public void MySqlQuotedTableName()
        {
            var desc = new ObjectNameDescriptor("`Test`", "`", "`");

            Assert.Equal(@"", desc.QuotedSchemaName);
            Assert.Equal(@"`Test`", desc.QuotedObjectName);
            Assert.Equal(@"`Test`", desc.QuotedFullName);
            Assert.Equal(@"Test", desc.UnquotedObjectName);
            Assert.Equal(@"Test", desc.UnquotedFullName);
        }

        [Fact]
        public void MySqlQuotedTableNameAndDots()
        {
            var desc = new ObjectNameDescriptor("`Test.Test`", "`", "`");

            Assert.Equal(@"", desc.QuotedSchemaName);
            Assert.Equal(@"`Test.Test`", desc.QuotedObjectName);
            Assert.Equal(@"`Test.Test`", desc.QuotedFullName);
            Assert.Equal(@"Test.Test", desc.UnquotedObjectName);
            Assert.Equal(@"Test.Test", desc.UnquotedFullName);
        }

        [Fact]
        public void PostgresWithSchemaAndDot()
        {
            var desc = new ObjectNameDescriptor(@"""public.dot"".""Test.Test""", @"""", @"""");

            Assert.Equal(@"""public.dot""", desc.QuotedSchemaName);
            Assert.Equal(@"""Test.Test""", desc.QuotedObjectName);
            Assert.Equal(@"""public.dot"".""Test.Test""", desc.QuotedFullName);
            Assert.Equal(@"Test.Test", desc.UnquotedObjectName);
            Assert.Equal(@"public.dot.Test.Test", desc.UnquotedFullName);
        }
    }
}
