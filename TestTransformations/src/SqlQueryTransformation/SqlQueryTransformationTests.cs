using EtlKit.DataFlow;
using EtlKit.TestTransformations.Fixtures;

namespace EtlKit.TestTransformations.SqlQueryTransformation
{
    [Collection("Transformations")]
    public class SqlQueryTransformationTests : TransformationsTestBase
    {
        public SqlQueryTransformationTests(TransformationsDatabaseFixture fixture)
            : base(fixture) { }

        [Fact]
        public void ShouldProcessSqlParameterWithGenericsTest()
        {
            //Arrange
            var obj = new TestSourceRecord { LastId = 123 };

            var settings = new MemorySource<TestSourceRecord>(new[] { obj });

            var query = new SqlQueryTransformation<TestSourceRecord, ExpandoObject>
            {
                ConnectionManager = SqlConnection,
                SqlTemplate = "select {{lastId}} as LastId",
            };

            var dest = new MemoryDestination<ExpandoObject>();

            //Act
            settings.LinkTo(query);
            query.LinkTo(dest);
            settings.Execute();
            dest.Wait();

            //Assert

            Assert.Single(dest.Data);
            var data = dest.Data.First() as IDictionary<string, object>;
            Assert.Equal(obj.LastId, data["LastId"]);
        }

        private record TestSourceRecord
        {
            public int LastId { get; init; }
        }

        [Fact]
        public void ShouldProcessSqlParameterWithExpandoObjectTest()
        {
            //Arrange
            dynamic obj = new ExpandoObject();
            obj.LastId = 123;

            var settings = new MemorySource<ExpandoObject>(new[] { (ExpandoObject)obj });

            var query = new EtlKit.DataFlow.SqlQueryTransformation
            {
                ConnectionManager = SqlConnection,
                SqlTemplate = "select {{lastId}} as LastId",
            };

            var dest = new MemoryDestination<ExpandoObject>();

            //Act
            settings.LinkTo(query);
            query.LinkTo(dest);
            settings.Execute();
            dest.Wait();

            //Assert

            Assert.Single(dest.Data);
            var data = dest.Data.First() as IDictionary<string, object>;
            Assert.Equal(obj.LastId, data["LastId"]);
        }

        [Fact]
        public void ShouldProcessSqlQueryWithCteTest()
        {
            //Arrange
            dynamic obj = new ExpandoObject();
            obj.value = "aaa";

            var settings = new MemorySource<ExpandoObject>(new[] { (ExpandoObject)obj });

            var query = new EtlKit.DataFlow.SqlQueryTransformation
            {
                ConnectionManager = SqlConnection,
                SqlTemplate =
                    @"
                with data as (
                    select 1 as id, '{{value}}' as value
                )
                select * from data",
                SourceTableDefinition = new EtlKit.TableDefinition(
                    "source",
                    new List<EtlKit.TableColumn> { new("id", "int"), new("value", "varchar(16)") }
                ),
            };

            var dest = new MemoryDestination<ExpandoObject>();

            //Act
            settings.LinkTo(query);
            query.LinkTo(dest);
            settings.Execute();
            dest.Wait();

            //Assert

            Assert.Single(dest.Data);
            var data = dest.Data.First() as IDictionary<string, object>;
            Assert.Equal(1, data["id"]);
            Assert.Equal((object)obj.value, data["value"]);
        }
    }
}
